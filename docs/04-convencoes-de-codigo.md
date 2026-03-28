# 04 · Convenções de Código

[← Voltar ao índice](../README.md)

> Padrões obrigatórios do projeto. Toda contribuição deve seguir estas regras para manter o código coeso e escalável.

---

## Nomenclatura

### Classes e Interfaces

```csharp
// Classes: PascalCase
public class PlayerMovement { }
public class VampireRole { }
public class MatchStateMachine { }

// Interfaces: I + PascalCase
public interface ITask { }
public interface IRoleAbility { }
public interface INetworkSync { }

// Abstratas: mesma regra de classe
public abstract class TaskBase { }
public abstract class RoleBase { }
```

### Métodos

```csharp
// PascalCase — verbos descritivos
public void StartMatch() { }
public void ApplyColorVariant(CharacterColor color) { }
private void ValidateKillRequest(ulong targetId) { }

// ServerRpcs: verbo Request + ação + ServerRpc
[ServerRpc] void RequestKillTargetServerRpc() { }
[ServerRpc] void RequestBlowTorchServerRpc() { }

// ClientRpcs: On + evento + ClientRpc
[ClientRpc] void OnPlayerKilledClientRpc() { }
[ClientRpc] void OnMeetingStartedClientRpc() { }
```

### Variáveis e Campos

```csharp
// Campos públicos: PascalCase
public NetworkVariable<bool> IsLit;
public PlayerRole AssignedRole;

// Campos privados: _camelCase com underscore
private float _killCooldownTimer;
private bool _hasVoted;
private CharacterVisuals _visuals;

// Parâmetros e variáveis locais: camelCase
void ApplyColor(CharacterColor targetColor)
{
    var spriteRenderer = GetComponent<SpriteRenderer>();
    float blendFactor = 0.5f;
}

// Constantes: SCREAMING_SNAKE_CASE
private const float KILL_RANGE = 1.5f;
private const int MAX_GUARD_ERRORS = 2;
private const int DEFAULT_TASK_COUNT = 6;
```

### Enums

```csharp
// Nome do enum: PascalCase singular
public enum PlayerRole { Innocent, Vampire, Guard }
public enum MatchState { Lobby, Loading, Playing, Meeting, Voting, Resolution, Ended }
public enum CharacterColor { White, Red, Orange, Purple, Green, Blue, DarkRed, Yellow, LightGreen, Cyan, Pink, Brown, Magenta }
public enum NPCBehaviorState { Idle, WalkingToTask, DoingTask, AtTorch, WalkingToTorch }
```

---

## Separação de Responsabilidades

### Regra das 3 camadas

Todo sistema é separado em 3 camadas que nunca se misturam:

```
┌─────────────────────────────────────────┐
│  REDE (NetworkBehaviour, RPC, NV)       │  ← NetworkSync, HostManager
├─────────────────────────────────────────┤
│  LÓGICA (regras, validação, estado)     │  ← RoleManager, TaskManager
├─────────────────────────────────────────┤
│  APRESENTAÇÃO (visual, UI, animação)    │  ← CharacterVisuals, HUD
└─────────────────────────────────────────┘
```

**Regra:** Uma camada nunca chama diretamente a camada que está 2 níveis acima ou abaixo.  
- `CharacterVisuals` não chama `NetworkManager`  
- `RoleManager` não chama `SpriteRenderer`  
- `HUD` não chama `NetworkVariable`

### Exemplo correto: morte de jogador

```csharp
// ❌ ERRADO — misturando tudo num lugar só
[ServerRpc]
void KillPlayerServerRpc(ulong targetId)
{
    // validação
    // muda sprite diretamente
    // toca som
    // atualiza UI
    // tudo junto = acoplamento total
}

// ✅ CORRETO — cada camada na sua responsabilidade
// REDE — recebe pedido e valida
[ServerRpc]
void RequestKillTargetServerRpc(ulong targetId, ServerRpcParams p = default)
{
    if (!ValidateKill(p.Receive.SenderClientId, targetId)) return;
    
    // Atualiza estado de rede
    GetPlayer(targetId).IsAlive.Value = false;
    
    // Notifica todos via evento de rede
    OnPlayerKilledClientRpc(targetId);
}

// LÓGICA — reage ao evento, sem saber de rede
void OnIsAliveChanged(bool prev, bool current)
{
    if (!current)
        GameEvents.OnPlayerDied.Invoke(this.OwnerClientId);
}

// APRESENTAÇÃO — reage ao evento, sem saber de rede ou regras
void Awake()
{
    GameEvents.OnPlayerDied += HandlePlayerDied;
}

void HandlePlayerDied(ulong clientId)
{
    if (clientId == this.OwnerClientId)
        PlayDeathAnimation();
}
```

---

## Padrões Utilizados

### Event Bus (GameEvents)

Comunicação entre sistemas sem acoplamento direto. Eventos globais ficam em `Scripts/Core/GameEvents.cs`.

```csharp
// GameEvents.cs
public static class GameEvents
{
    public static Action<ulong> OnPlayerDied;
    public static Action<ulong> OnMeetingStarted;
    public static Action<MatchState> OnMatchStateChanged;
    public static Action<int, int> OnTaskProgressUpdated; // completadas, total
}

// Publicar um evento (em qualquer sistema)
GameEvents.OnPlayerDied?.Invoke(clientId);

// Assinar um evento (em qualquer listener)
void OnEnable()  => GameEvents.OnPlayerDied += HandlePlayerDied;
void OnDisable() => GameEvents.OnPlayerDied -= HandlePlayerDied;
```

**Regra:** Sempre desassinar (`-=`) no `OnDisable` ou `OnDestroy` para evitar memory leaks.

---

### Service Layer (Services/)

SDKs externos são sempre abstraídos por um serviço local. O resto do jogo nunca chama o SDK diretamente.

```csharp
// ❌ ERRADO — SDK chamado direto do gameplay
await LobbyService.Instance.CreateLobbyAsync(...);

// ✅ CORRETO — abstraído pelo serviço local
await _lobbyService.CreateLobbyAsync(lobbyName, maxPlayers, settings);

// O serviço local encapsula o SDK
public class LobbyService
{
    public async Task<bool> CreateLobbyAsync(string name, int maxPlayers, MatchConfig config)
    {
        try
        {
            var options = new CreateLobbyOptions { ... };
            _currentLobby = await Unity.Services.Lobbies.LobbyService.Instance
                .CreateLobbyAsync(name, maxPlayers, options);
            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyService] {e.Message}");
            return false;
        }
    }
}
```

---

### Strategy Pattern (Habilidades de Role)

Cada papel implementa `IRoleAbility` para que `RoleManager` não precise saber os detalhes de cada papel.

```csharp
public interface IRoleAbility
{
    void Activate(ulong ownerClientId);
    bool CanActivate(ulong ownerClientId);
    float GetCooldownRemaining(ulong ownerClientId);
}

public class VampireBiteAbility : IRoleAbility
{
    public bool CanActivate(ulong ownerId) 
        => _cooldownTracker.CanAct(ownerId);
    
    public void Activate(ulong ownerId)
        => RequestKillTargetServerRpc(GetNearestTarget(ownerId));
}

public class GuardShootAbility : IRoleAbility { ... }
public class TorchBlowAbility : IRoleAbility { ... }
```

---

### Singleton de Rede

Para managers que precisam persistir entre cenas e ser acessíveis globalmente:

```csharp
// Scripts/Core/SingletonNetwork.cs
public class SingletonNetwork<T> : NetworkBehaviour where T : NetworkBehaviour
{
    public static T Instance { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this as T;
        DontDestroyOnLoad(gameObject);
    }
}

// Uso
public class MatchManager : SingletonNetwork<MatchManager> { ... }
public class RoleManager : SingletonNetwork<RoleManager> { ... }
```

---

## Regras para NetworkBehaviour

```csharp
public class PlayerNetworkSync : NetworkBehaviour
{
    // ✅ Inicializar NetworkVariables inline
    public NetworkVariable<bool> IsAlive = new NetworkVariable<bool>(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // ✅ Usar OnNetworkSpawn, não Awake/Start para lógica de rede
    public override void OnNetworkSpawn()
    {
        IsAlive.OnValueChanged += OnIsAliveChanged;

        // Só o dono do objeto configura input
        if (IsOwner)
            GetComponent<PlayerInputHandler>().enabled = true;
        
        // Só o host inicializa lógica de servidor
        if (IsServer)
            InitializeServerSide();
    }

    public override void OnNetworkDespawn()
    {
        // ✅ Sempre desassinar
        IsAlive.OnValueChanged -= OnIsAliveChanged;
    }

    private void OnIsAliveChanged(bool previous, bool current)
    {
        // ✅ Reage à mudança de estado — não chama rede daqui
        GameEvents.OnPlayerDied?.Invoke(OwnerClientId);
    }
}
```

---

## O que Nunca Fazer

### Nunca misturar lógica de UI com regra de negócio

```csharp
// ❌ ERRADO
public class VotingPanelUI : MonoBehaviour
{
    void OnVoteButtonClicked(ulong targetId)
    {
        // nunca validar regra aqui
        if (votesRemaining <= 0) return;
        votesRemaining--;
        NetworkManager.Singleton...  // nunca chamar NetworkManager da UI
    }
}

// ✅ CORRETO
public class VotingPanelUI : MonoBehaviour
{
    void OnVoteButtonClicked(ulong targetId)
    {
        // UI apenas dispara a ação — quem valida é o VotingSystem
        VotingSystem.Instance.RequestVote(targetId);
    }
}
```

### Nunca fazer lógica de gameplay em Update de NetworkBehaviour

```csharp
// ❌ ERRADO — pesado e propenso a dessincronização
void Update()
{
    if (IsServer)
        CheckAllPlayersProximityForKill();
}

// ✅ CORRETO — validação só quando solicitada
[ServerRpc]
void RequestKillTargetServerRpc(ulong targetId, ServerRpcParams p = default)
{
    // validação acontece aqui, sob demanda
}
```

### Nunca confiar no ClientId passado como parâmetro de ServerRpc

```csharp
// ❌ ERRADO — client pode mentir sobre seu próprio ID
[ServerRpc]
void RequestActionServerRpc(ulong myClientId) // ← client passa seu próprio ID
{
    // myClientId pode ser qualquer valor
}

// ✅ CORRETO — lê o ID real do parâmetro da RPC
[ServerRpc]
void RequestActionServerRpc(ServerRpcParams rpcParams = default)
{
    ulong realSenderId = rpcParams.Receive.SenderClientId; // ← ID real, não manipulável
}
```

---

## Estrutura de Arquivo Padrão

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
// outros usings em ordem alfabética

namespace EchoesInTheDark.Gameplay.Roles
{
    /// <summary>
    /// Gerencia o sorteio e distribuição de papéis para todos os jogadores.
    /// Roda apenas no Host — clients recebem apenas seu próprio papel.
    /// </summary>
    public class RoleManager : SingletonNetwork<RoleManager>
    {
        // ─── Constantes ───────────────────────────────────────────
        private const int MIN_PLAYERS_TO_START = 3;

        // ─── Campos Serializados (Inspector) ──────────────────────
        [SerializeField] private RoleDefinition _innocentDefinition;
        [SerializeField] private RoleDefinition _vampireDefinition;
        [SerializeField] private RoleDefinition _guardDefinition;

        // ─── NetworkVariables ─────────────────────────────────────
        // (se houver)

        // ─── Campos Privados ──────────────────────────────────────
        private Dictionary<ulong, PlayerRole> _playerRoles = new();

        // ─── Unity Lifecycle ──────────────────────────────────────
        public override void OnNetworkSpawn() { ... }
        public override void OnNetworkDespawn() { ... }

        // ─── API Pública ──────────────────────────────────────────
        public PlayerRole GetRole(ulong clientId) { ... }
        public void AssignRoles(List<ulong> connectedClients) { ... }

        // ─── ServerRpcs ───────────────────────────────────────────
        // (se houver)

        // ─── ClientRpcs ───────────────────────────────────────────
        [ClientRpc]
        private void ReceiveRoleAssignmentClientRpc(PlayerRole role, ClientRpcParams p) { ... }

        // ─── Privados / Implementação ─────────────────────────────
        private List<PlayerRole> BuildRolePool(int playerCount, MatchConfig config) { ... }
    }
}
```

---

## Namespaces

```
EchoesInTheDark.Core
EchoesInTheDark.Network
EchoesInTheDark.Gameplay.Character
EchoesInTheDark.Gameplay.Player
EchoesInTheDark.Gameplay.NPC
EchoesInTheDark.Gameplay.Roles
EchoesInTheDark.Gameplay.Tasks
EchoesInTheDark.Gameplay.Lighting
EchoesInTheDark.Gameplay.Meeting
EchoesInTheDark.Gameplay.Match
EchoesInTheDark.UI
EchoesInTheDark.Services
```

---

*[← Voltar ao índice](../README.md)*
