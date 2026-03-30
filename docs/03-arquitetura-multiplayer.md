# 03 · Arquitetura Multiplayer

[← Voltar ao índice](../README.md)

> Como o multiplayer funciona no Echoes in the Dark: autoridade, sincronização, RPCs e o fluxo completo de cada ação de gameplay.

---

## Princípio Fundamental

> **O Host é a fonte da verdade. Clients solicitam — Host valida — todos recebem o resultado.**

Nenhum client decide se uma ação é válida. Toda decisão de gameplay passa pelo Host.

---

## Autoridade por Sistema

| Sistema | Quem tem autoridade | Como clients participam |
|---|---|---|
| Sorteio de papéis | Host (exclusivo) | Recebem seu papel via `[ClientRpc]` direto |
| Movimento do player | **Client** (próprio jogador) | Posição sincronizada via `NetworkTransform` |
| Interação com missão | Host valida | Client envia `[ServerRpc]`, Host confirma |
| Morte por Vampiro | Host valida proximidade | Client envia pedido, Host aceita ou rejeita |
| Apagar tocha | Host valida | Client envia pedido, Host muda `NetworkVariable` |
| IA dos NPCs | Host (exclusivo) | Clients recebem posição via `NetworkVariable` |
| Votação | Host contabiliza | Clients enviam voto via `[ServerRpc]` |
| Resultado da votação | Host calcula | Resultado distribuído via `[ClientRpc]` |
| Estado da partida | Host controla | State machine sincronizada via `NetworkVariable` |

---

## Estrutura do NetworkManager

O `NetworkManager` é configurado no prefab `Prefabs/Network/NetworkManager.prefab`:

```
NetworkManager (GameObject)
 ├── NetworkManager (component)
 │    ├── Player Prefab: PlayerNetworkObject.prefab
 │    ├── Network Prefabs List: todos os prefabs de rede registrados
 │    └── Network Transport: UnityTransport
 └── UnityTransport (component)
      └── Protocol Type: Unity Relay
```

**Regra:** Todo prefab que será spawnado pela rede deve estar na `Network Prefabs List`.

---

## Fluxo de Ação Multiplayer (padrão)

Todo sistema de gameplay segue este fluxo:

```
Client
  │
  ├─ Input do jogador (teclado/mouse)
  │
  ├─ PlayerInteraction.cs detecta ação possível
  │
  └─ Envia [ServerRpc] com parâmetros mínimos
        │
        Host
          │
          ├─ Valida se a ação é legal (proximidade, cooldown, estado atual)
          │
          ├─ [SE VÁLIDA] Executa a ação no estado do servidor
          │    └─ Atualiza NetworkVariable OU envia [ClientRpc]
          │
          └─ [SE INVÁLIDA] Ignora silenciosamente (ou loga para debug)
                │
                Todos os Clients
                  └─ Recebem atualização via NetworkVariable ou ClientRpc
                       └─ Atualizam visual local
```

---

## Spawn de Players

O spawn de até 15 players é coordenado pelo `PlayerSpawnCoordinator` no Host:

```csharp
void OnClientConnected(ulong clientId)
{
    Vector3 spawnPoint = GetAvailableSpawnPoint();
    var playerObj = Instantiate(playerPrefab, spawnPoint, Quaternion.identity);
    playerObj.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
}
```

**Pontos de spawn:** Distribuídos pela Praça Central. Nenhum player spawna em cima de outro.

---

## NetworkVariables do Projeto

### No Player (`PlayerNetworkSync.cs`)

```csharp
public NetworkVariable<PlayerRole> AssignedRole;
public NetworkVariable<bool> IsAlive;
public NetworkVariable<bool> IsCarryingTorch;
public NetworkVariable<CharacterColor> BodyColor;
public NetworkVariable<int> HeadVariantIndex;
public NetworkVariable<int> BodyVariantIndex;
public NetworkVariable<int> HandsVariantIndex;
public NetworkVariable<int> FeetVariantIndex;
public NetworkVariable<int> TasksCompleted; // Owner only
```

### No NPC (`NPCNetworkSync.cs`)

```csharp
public NetworkVariable<Vector2> Position;
public NetworkVariable<NPCBehaviorState> CurrentState;
public NetworkVariable<CharacterColor> BodyColor;
public NetworkVariable<int> HeadVariantIndex;
public NetworkVariable<int> BodyVariantIndex;
public NetworkVariable<int> HandsVariantIndex;
public NetworkVariable<int> FeetVariantIndex;
```

### No Match (`MatchNetworkState.cs`)

```csharp
public NetworkVariable<MatchState> CurrentState;
public NetworkVariable<int> TotalTasksCompleted;
public NetworkVariable<int> TotalTasksRequired;
public NetworkVariable<int> InnocentsAlive;
public NetworkVariable<int> VampiresAlive;
```

### Na Tocha (`TorchBehavior.cs`)

```csharp
public NetworkVariable<bool> IsLit; // TUDO que vai pela rede para a tocha
```

---

## RPCs do Projeto

### ServerRpcs (Client → Host)

```csharp
[ServerRpc(RequireOwnership = true)]
void RequestInteractTaskServerRpc(int taskId, ServerRpcParams rpcParams = default);

[ServerRpc(RequireOwnership = true)]
void RequestKillTargetServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default);

[ServerRpc(RequireOwnership = true)]
void RequestBlowTorchServerRpc(ulong torchNetworkObjectId, ServerRpcParams rpcParams = default);

[ServerRpc(RequireOwnership = true)]
void RequestShootTargetServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default);

[ServerRpc(RequireOwnership = true)]
void RequestReportBodyServerRpc(ulong bodyClientId, ServerRpcParams rpcParams = default);

[ServerRpc(RequireOwnership = true)]
void RequestEmergencyMeetingServerRpc(ServerRpcParams rpcParams = default);

[ServerRpc(RequireOwnership = true)]
void SubmitVoteServerRpc(ulong votedForClientId, ServerRpcParams rpcParams = default);
```

### ClientRpcs (Host → Todos)

```csharp
[ClientRpc] void OnMeetingStartedClientRpc(ulong reporterId, MeetingType type);
[ClientRpc] void OnVoteResultClientRpc(ulong bannedClientId, bool wasVampire);
[ClientRpc] void OnMatchEndedClientRpc(WinCondition result);
[ClientRpc] void ReceiveRoleAssignmentClientRpc(PlayerRole role, ClientRpcParams clientRpcParams);
```

---

## Validações de Segurança no Host

```csharp
[ServerRpc(RequireOwnership = true)]
void RequestKillTargetServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default)
{
    ulong senderId = rpcParams.Receive.SenderClientId;

    if (GetPlayerRole(senderId) != PlayerRole.Vampire) return;
    if (!vampireCooldownTracker.CanAttack(senderId)) return;

    var target = GetPlayerById(targetClientId);
    if (!target.IsAlive.Value) return;

    float distance = Vector2.Distance(
        GetPlayerPosition(senderId),
        GetPlayerPosition(targetClientId)
    );
    if (distance > KILL_RANGE) return;

    target.IsAlive.Value = false;
    vampireCooldownTracker.ResetCooldown(senderId);
    OnPlayerKilledClientRpc(targetClientId);
}
```

**Regra:** O sender ID real vem de `rpcParams.Receive.SenderClientId` — nunca de parâmetro passado pelo client.

---

## Sincronização de Iluminação

```csharp
// ❌ ERRADO — networkando renderização
[ClientRpc]
void UpdateLightIntensityClientRpc(float intensity, Color color, float radius) { }

// ✅ CORRETO — apenas estado booleano
public NetworkVariable<bool> IsLit = new NetworkVariable<bool>(true);

void OnIsLitChanged(bool previous, bool current)
{
    torchLight.enabled = current;
    torchAnimator.SetBool("isLit", current);
    torchParticles.gameObject.SetActive(current);
}
```

---

## Auto-Connect no Multiplayer Play Mode

> ⚠️ **Atenção:** O código abaixo é a implementação **real e funcional** do projeto.  
> A API `CurrentPlayer.ReadOnlyTags()` foi descontinuada no MPPM 2.x e **não funciona**.

### Solução adotada — argumentos de linha de comando

O MPPM injeta `-mppmTag <valor>` nos argumentos de cada Virtual Player. Isso funciona em todas as versões (1.x e 2.x) e não depende de nenhum namespace externo.

```csharp
// Bootstrap.cs — AutoConnectInEditor()
private void AutoConnectInEditor()
{
#if UNITY_EDITOR
    // Sobrescreve o Relay para IP direto no editor
    // (Relay só é usado em produção, via fluxo de Lobby)
    Unity.Netcode.Transports.UTP.UnityTransport transport =
        NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
    transport.SetConnectionData("127.0.0.1", 7777);

    if (IsVirtualPlayer())
    {
        Debug.Log("[Bootstrap] Virtual Player → StartClient (IP direto)");
        NetworkManager.Singleton.StartClient();
    }
    else
    {
        Debug.Log("[Bootstrap] Main Editor → StartHost (IP direto)");
        NetworkManager.Singleton.StartHost();
    }
#endif
}

private static bool IsVirtualPlayer()
{
    string[] args = System.Environment.GetCommandLineArgs();
    for (int i = 0; i < args.Length; i++)
    {
        if (args[i] == "-mppmTag" && i + 1 < args.Length)
        {
            string tag = args[i + 1].ToLower();
            return tag == "vampire" || tag == "innocent" || tag == "guard";
        }
    }
    return false; // sem tag = Main Editor = Host
}
```

### Configuração das tags no Multiplayer Play Mode

Em `Project Settings → Multiplayer → Playmode → Player Tags`:

| Instância | Tag configurada | Resultado |
|---|---|---|
| Main Editor | (nenhuma) | `IsVirtualPlayer() = false` → StartHost |
| Player 2 | `vampire` | `IsVirtualPlayer() = true` → StartClient |
| Player 3 | `innocent` | `IsVirtualPlayer() = true` → StartClient |
| Player 4 | `guard` | `IsVirtualPlayer() = true` → StartClient |

### Por que IP direto no editor e não Relay?

O Relay exige uma **alocação real** no Unity Cloud antes de `StartHost/Client`. No editor com MPPM, não há fluxo de Lobby para fazer essa alocação. A solução correta é:

- **Editor (MPPM):** IP direto `127.0.0.1:7777` — sem custo, sem internet, imediato
- **Produção:** Relay real — fluxo completo de Lobby → Relay allocation → `SetRelayServerData()` → Start

O prefab do `NetworkManager` permanece configurado como `Relay Unity Transport`. O `Bootstrap.cs` sobrescreve o protocolo apenas no editor via `SetConnectionData()`.

---

*[← Voltar ao índice](../README.md)*