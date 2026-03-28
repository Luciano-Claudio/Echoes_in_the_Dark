# Sistema · Papéis (Roles)

[← Voltar ao índice](../../README.md)

> Sorteio, distribuição, dados e comportamentos dos três papéis: Inocente, Vampiro e Guarda.

---

## Papéis Disponíveis

| Papel | Objetivo | Skin |
|---|---|---|
| **Inocente** | Completar 6 missões | Aleatória (cor + acessórios) |
| **Vampiro** | Eliminar Inocentes | Aleatória (igual ao Inocente) |
| **Guarda** | Eliminar Vampiros sem matar inocentes | Exclusiva (não aleatória) |

---

## Fluxo de Sorteio (Host)

O sorteio é completamente autoritativo no Host. Nenhum client sabe o papel dos outros (exceto Vampiros, que veem os outros Vampiros).

```
Host
  │
  ├─ Recebe lista de todos os clientIds conectados
  ├─ Lê MatchConfig (num vampires, num guards)
  ├─ Monta pool de papéis: [Guard, Vampire, Vampire, Innocent, Innocent, ...]
  ├─ Embaralha a pool (Fisher-Yates)
  └─ Para cada clientId:
       ├─ Atribui papel da pool
       ├─ Salva em _playerRoles[clientId]
       ├─ Envia papel APENAS para o client dono (TargetClientRpc)
       └─ Se Vampiro: envia lista de outros Vampiros para ele
```

```csharp
public class RoleManager : SingletonNetwork<RoleManager>
{
    private Dictionary<ulong, PlayerRole> _playerRoles = new();

    // Host only
    public void AssignRoles(List<ulong> clientIds, MatchConfig config)
    {
        if (!IsServer) return;

        var pool = BuildRolePool(clientIds.Count, config);
        Shuffle(pool);

        var vampireIds = new List<ulong>();

        for (int i = 0; i < clientIds.Count; i++)
        {
            var clientId = clientIds[i];
            var role = pool[i];
            
            _playerRoles[clientId] = role;

            // Envia papel apenas para o dono
            var targetParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
            ReceiveRoleClientRpc(role, targetParams);

            if (role == PlayerRole.Vampire)
                vampireIds.Add(clientId);
        }

        // Envia lista de vampiros para cada vampiro
        foreach (var vampId in vampireIds)
        {
            var targetParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { vampId } }
            };
            ReceiveAlliesClientRpc(vampireIds.ToArray(), targetParams);
        }
    }

    public PlayerRole GetRole(ulong clientId)
    {
        if (!IsServer) throw new Exception("GetRole só pode ser chamado no Host");
        return _playerRoles.TryGetValue(clientId, out var role) ? role : PlayerRole.Innocent;
    }

    [ClientRpc]
    private void ReceiveRoleClientRpc(PlayerRole role, ClientRpcParams p = default)
    {
        // Cada client recebe apenas o seu papel
        LocalPlayerData.Instance.MyRole = role;
        GameEvents.OnRoleAssigned?.Invoke(role);
    }

    [ClientRpc]
    private void ReceiveAlliesClientRpc(ulong[] vampireIds, ClientRpcParams p = default)
    {
        // Vampiro recebe lista dos aliados
        LocalPlayerData.Instance.AllyIds = new HashSet<ulong>(vampireIds);
    }
}
```

---

## RoleDefinition (ScriptableObject)

Dados imutáveis de cada papel. Não contém comportamento.

```csharp
[CreateAssetMenu(menuName = "EitD/Roles/Role Definition")]
public class RoleDefinition : ScriptableObject
{
    public PlayerRole roleType;
    public string displayName;
    [TextArea] public string description;
    public Sprite roleIcon;
    public Color hudColor;
}
```

---

## IRoleAbility (Interface)

Contrato para todas as habilidades de papel.

```csharp
public interface IRoleAbility
{
    void Activate(ulong ownerClientId);
    bool CanActivate(ulong ownerClientId);
    float GetCooldownRemaining(ulong ownerClientId);
    void OnCooldownReset(ulong ownerClientId);
}
```

---

## Inocente

Sem habilidades de ataque. Suas "habilidades" são interações com o mundo.

```csharp
public class InnocentRole : MonoBehaviour
{
    // Inocentes não precisam de IRoleAbility especial
    // Interações (missão, reportar corpo, reunião) ficam no PlayerInteraction.cs
    // filtradas por papel
}
```

**Restrições implementadas em `PlayerInteraction.cs`:**
- Não pode usar "Apagar Tocha" (só Vampiro)
- Não pode usar "Atirar" (só Guarda)
- Pode "Reportar Corpo" e "Convocar Reunião"

---

## Vampiro

```csharp
public class VampireRole : MonoBehaviour, IRoleAbility
{
    // Cooldown configurável (padrão: 30s)
    private float _killCooldown;
    private float _cooldownTimer;

    public bool CanActivate(ulong ownerId)
        => _cooldownTimer <= 0f;

    public float GetCooldownRemaining(ulong ownerId)
        => Mathf.Max(0f, _cooldownTimer);

    public void Activate(ulong ownerId)
    {
        // Verifica range localmente antes de enviar RPC (early out)
        var target = FindNearestTargetInRange();
        if (target == null) return;

        // Envia pedido ao Host para validação final
        RequestKillTargetServerRpc(target.OwnerClientId);
    }

    [ServerRpc(RequireOwnership = true)]
    private void RequestKillTargetServerRpc(ulong targetId, ServerRpcParams p = default)
    {
        var senderId = p.Receive.SenderClientId;
        
        // Validações no Host
        if (RoleManager.Instance.GetRole(senderId) != PlayerRole.Vampire) return;
        if (!_cooldownTracker.CanAttack(senderId)) return;
        
        var target = GetPlayerById(targetId);
        if (target == null || !target.IsAlive.Value) return;
        
        // Guarda tem proteção de alho — não pode ser alvo
        if (RoleManager.Instance.GetRole(targetId) == PlayerRole.Guard) 
        {
            // Toca animação de rejeição no Vampiro
            TriggerGarlicRejectionClientRpc(senderId);
            return;
        }
        
        // Distância real no servidor
        float dist = Vector2.Distance(
            GetPlayerPosition(senderId),
            GetPlayerPosition(targetId)
        );
        if (dist > GameConstants.KILL_RANGE) return;
        
        // Executa morte
        target.IsAlive.Value = false;
        _cooldownTracker.ResetCooldown(senderId);
        OnPlayerKilledClientRpc(targetId);
    }
}
```

### Proteção de Alho (Guarda)

```
Vampiro aperta E próximo ao Guarda
  │
  Host verifica: target é Guarda?
  │
  ├─ SIM, proteção infinita → rejeita ataque, TriggerGarlicRejectionClientRpc(vampireId)
  │     └─ Mini animação de rejeição no Vampiro (client-side, visível para todos próximos)
  │
  ├─ SIM, proteção com limite → decrementa usos, se ainda tem → rejeita; se zerou → mata
  │
  └─ NÃO → prossegue com a validação normal de morte
```

---

## Guarda

```csharp
public class GuardRole : MonoBehaviour, IRoleAbility
{
    private int _innocentKillCount = 0;
    private int _maxInnocentKills; // vem do MatchConfig

    public bool CanActivate(ulong ownerId) => true; // balas infinitas, sem cooldown

    public void Activate(ulong ownerId)
    {
        var target = FindNearestTargetInRange();
        if (target == null) return;
        RequestShootTargetServerRpc(target.OwnerClientId);
    }

    [ServerRpc(RequireOwnership = true)]
    private void RequestShootTargetServerRpc(ulong targetId, ServerRpcParams p = default)
    {
        var senderId = p.Receive.SenderClientId;

        if (RoleManager.Instance.GetRole(senderId) != PlayerRole.Guard) return;

        var target = GetPlayerById(targetId);
        if (target == null || !target.IsAlive.Value) return;

        var targetRole = RoleManager.Instance.GetRole(targetId);
        
        target.IsAlive.Value = false;

        if (targetRole == PlayerRole.Innocent || targetRole == PlayerRole.Guard)
        {
            // Erro do Guarda
            _innocentKillCount++;
            
            if (_innocentKillCount >= _maxInnocentKills)
            {
                // Guarda é preso — vira espectador
                ImprisonGuardClientRpc(senderId);
            }
        }

        OnPlayerKilledClientRpc(targetId);
    }

    [ClientRpc]
    private void ImprisonGuardClientRpc(ulong guardClientId, ClientRpcParams p = default)
    {
        // Guarda vira espectador
        if (guardClientId == NetworkManager.Singleton.LocalClientId)
        {
            // Este é o Guarda preso — muda para modo espectador
            GameEvents.OnLocalPlayerImprisoned?.Invoke();
        }
    }
}
```

---

## Verificação de Condições de Vitória

Checada pelo `MatchManager` no Host após cada morte ou missão completada:

```csharp
// MatchManager.cs — Host only
void CheckWinConditions()
{
    int innocentsAlive = CountAlivePlayers(PlayerRole.Innocent);
    int vampiresAlive  = CountAlivePlayers(PlayerRole.Vampire);
    int totalTasks     = GetTotalRequiredTasks();
    int doneTasks      = GetCompletedTasks();

    // Vitória dos Vampiros: todos os inocentes mortos
    if (innocentsAlive == 0)
    {
        EndMatch(WinCondition.VampiresWin);
        return;
    }

    // Vitória dos Guardas/Inocentes: todos os vampiros eliminados
    if (vampiresAlive == 0)
    {
        EndMatch(WinCondition.InnocentsWin);
        return;
    }

    // Vitória dos Inocentes: todas as missões completas
    if (doneTasks >= totalTasks)
    {
        EndMatch(WinCondition.InnocentsWin);
        return;
    }
}
```

---

*[← Voltar ao índice](../../README.md)*
