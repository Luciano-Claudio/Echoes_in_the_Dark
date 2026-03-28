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
// Fluxo de spawn
void OnClientConnected(ulong clientId)
{
    // 1. Escolhe ponto de spawn disponível
    Vector3 spawnPoint = GetAvailableSpawnPoint();
    
    // 2. Spawna o PlayerNetworkObject com ownership do client
    var playerObj = Instantiate(playerPrefab, spawnPoint, Quaternion.identity);
    playerObj.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    
    // 3. PlayerNetworkObject inicializa com valores padrão
}
```

**Pontos de spawn:** Distribuídos pela Praça Central. Nenhum player spawna em cima de outro.

---

## NetworkVariables do Projeto

Lista completa de `NetworkVariable` planejadas:

### No Player (`PlayerNetworkSync.cs`)

```csharp
// Visíveis por todos — usadas para gameplay
public NetworkVariable<PlayerRole> AssignedRole;      // papel (revelado só na reunião/morte)
public NetworkVariable<bool> IsAlive;                 // vivo ou morto
public NetworkVariable<bool> IsCarryingTorch;         // carregando tocha
public NetworkVariable<CharacterColor> BodyColor;     // cor do personagem
public NetworkVariable<int> HeadVariantIndex;         // índice do sprite de cabeça
public NetworkVariable<int> BodyVariantIndex;         // índice do sprite de corpo
public NetworkVariable<int> HandsVariantIndex;        // índice do sprite de mãos
public NetworkVariable<int> FeetVariantIndex;         // índice do sprite de pés

// Visível apenas para o próprio client (Owner)
public NetworkVariable<int> TasksCompleted;
```

### No NPC (`NPCNetworkSync.cs`)

```csharp
public NetworkVariable<Vector2> Position;             // posição (Host atualiza, clients leem)
public NetworkVariable<NPCBehaviorState> CurrentState; // idle, walkingToTask, doingTask, atTorch
public NetworkVariable<CharacterColor> BodyColor;
public NetworkVariable<int> HeadVariantIndex;
public NetworkVariable<int> BodyVariantIndex;
public NetworkVariable<int> HandsVariantIndex;
public NetworkVariable<int> FeetVariantIndex;
```

### No Match (`MatchNetworkState.cs`)

```csharp
public NetworkVariable<MatchState> CurrentState;      // estado atual da partida
public NetworkVariable<int> TotalTasksCompleted;      // progresso global de missões
public NetworkVariable<int> TotalTasksRequired;       // total necessário para vitória
public NetworkVariable<int> InnocentsAlive;           // contagem de inocentes vivos
public NetworkVariable<int> VampiresAlive;            // contagem de vampiros vivos
```

### Na Tocha (`TorchBehavior.cs`)

```csharp
public NetworkVariable<bool> IsLit;                   // acesa ou apagada — TUDO que vai pela rede
```

---

## RPCs do Projeto

### ServerRpcs (Client → Host)

```csharp
// PlayerInteraction.cs
[ServerRpc(RequireOwnership = true)]
void RequestInteractTaskServerRpc(int taskId, ServerRpcParams rpcParams = default);

// VampireRole.cs
[ServerRpc(RequireOwnership = true)]
void RequestKillTargetServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default);

// VampireRole.cs
[ServerRpc(RequireOwnership = true)]
void RequestBlowTorchServerRpc(ulong torchNetworkObjectId, ServerRpcParams rpcParams = default);

// GuardRole.cs
[ServerRpc(RequireOwnership = true)]
void RequestShootTargetServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default);

// PlayerInteraction.cs
[ServerRpc(RequireOwnership = true)]
void RequestReportBodyServerRpc(ulong bodyClientId, ServerRpcParams rpcParams = default);

// MeetingManager.cs
[ServerRpc(RequireOwnership = true)]
void RequestEmergencyMeetingServerRpc(ServerRpcParams rpcParams = default);

// VotingSystem.cs
[ServerRpc(RequireOwnership = true)]
void SubmitVoteServerRpc(ulong votedForClientId, ServerRpcParams rpcParams = default);
```

### ClientRpcs (Host → Todos)

```csharp
// MeetingManager.cs
[ClientRpc]
void OnMeetingStartedClientRpc(ulong reporterId, MeetingType type);

// VotingSystem.cs
[ClientRpc]
void OnVoteResultClientRpc(ulong bannedClientId, bool wasVampire);

// MatchManager.cs
[ClientRpc]
void OnMatchEndedClientRpc(WinCondition result);

// RoleManager.cs — enviado apenas para o owner (TargetClientRpc)
[ClientRpc]
void ReceiveRoleAssignmentClientRpc(PlayerRole role, ClientRpcParams clientRpcParams);
```

---

## Validações de Segurança no Host

Toda `[ServerRpc]` que envolve ação de gameplay deve validar:

```csharp
[ServerRpc(RequireOwnership = true)]
void RequestKillTargetServerRpc(ulong targetClientId, ServerRpcParams rpcParams = default)
{
    ulong senderId = rpcParams.Receive.SenderClientId;
    
    // 1. O sender tem papel de Vampiro?
    if (GetPlayerRole(senderId) != PlayerRole.Vampire) return;
    
    // 2. O cooldown passou?
    if (!vampireCooldownTracker.CanAttack(senderId)) return;
    
    // 3. O alvo está vivo?
    var target = GetPlayerById(targetClientId);
    if (!target.IsAlive.Value) return;
    
    // 4. O alvo está próximo o suficiente?
    float distance = Vector2.Distance(
        GetPlayerPosition(senderId),
        GetPlayerPosition(targetClientId)
    );
    if (distance > KILL_RANGE) return;
    
    // 5. Tudo válido — executa
    target.IsAlive.Value = false;
    vampireCooldownTracker.ResetCooldown(senderId);
    OnPlayerKilledClientRpc(targetClientId);
}
```

**Regra:** Nunca confiar em dados vindos do client além dos parâmetros da RPC. O sender ID real vem de `rpcParams.Receive.SenderClientId`, não de um parâmetro passado pelo client.

---

## Sincronização de Iluminação

A iluminação é o sistema mais sensível a erros de arquitetura de rede.

### O que NÃO fazer:
```csharp
// ❌ ERRADO — networkando renderização
[ClientRpc]
void UpdateLightIntensityClientRpc(float intensity, Color color, float radius) { ... }
```

### O que fazer:
```csharp
// ✅ CORRETO — apenas estado booleano
public NetworkVariable<bool> IsLit = new NetworkVariable<bool>(true);

// Cada client reage à mudança e renderiza localmente
void OnIsLitChanged(bool previous, bool current)
{
    torchLight.enabled = current;                    // renderização local
    torchAnimator.SetBool("isLit", current);         // animação local
    torchParticles.gameObject.SetActive(current);    // partículas locais
}
```

---

## Papel do Guarda — Visibilidade

O papel do Guarda tem uma regra especial de visibilidade: **outros jogadores veem a skin de Guarda** (não aleatória), mas **não sabem que é o Guarda** até que a skin seja revelada numa área iluminada.

```csharp
// No CharacterVisuals.cs
void UpdateCharacterVisuals()
{
    bool isInLight = PlayerVisibilityController.IsInLight(this.transform.position);
    
    if (!isInLight)
    {
        // Escuridão: apenas olhos visíveis para outros jogadores
        ShowOnlyEyes();
        return;
    }
    
    // Luz: mostra visual completo
    if (assignedRole.Value == PlayerRole.Guard)
        ApplyGuardSkin();   // skin exclusiva do Guarda
    else
        ApplyColoredSkin(bodyColor.Value, headVariant.Value, ...);
}
```

---

## Auto-Connect no Multiplayer Play Mode

Para evitar clicar "Start Client" manualmente em cada virtual player durante testes:

```csharp
// Bootstrap.cs
void Start()
{
#if UNITY_EDITOR
    using Unity.Multiplayer.Playmode;
    var tags = CurrentPlayer.ReadOnlyTags();
    
    bool isVirtualPlayer = tags.Contains("innocent") || 
                           tags.Contains("vampire") || 
                           tags.Contains("guard");
    
    if (isVirtualPlayer)
        StartAsClient();
    else
        StartAsHost();
#endif
}
```

---

*[← Voltar ao índice](../README.md)*
