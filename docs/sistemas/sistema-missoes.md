# Sistema · Missões (Tasks)

[← Voltar ao índice](../../README.md)

> Banco de 30+ missões, arquitetura extensível, validação no Host e progresso individual + global.

---

## Visão Geral

```
Banco de Missões (30+ TaskDefinitionSO)
  │
  ├─ Host sorteia missões ativas para a partida
  ├─ Host distribui 6 missões por Inocente (padrão, configurável)
  │
  └─ Durante a partida:
       ├─ Client chega ao local → aperta E → PlayerInteraction detecta TaskSpot
       ├─ Client envia RequestInteractTaskServerRpc
       ├─ Host valida (jogador certo? no range? missão não completada?)
       ├─ Host marca missão como em progresso → animação inicia
       ├─ Timer do servidor conta o tempo da missão
       ├─ Ao completar: ConfirmTaskCompletedClientRpc → progresso atualizado
       └─ CheckWinConditions
```

---

## TaskDefinition (ScriptableObject)

Dados imutáveis de uma missão. Cada missão do banco é um asset.

```csharp
[CreateAssetMenu(menuName = "EitD/Tasks/Task Definition")]
public class TaskDefinition : ScriptableObject
{
    [Header("Identificação")]
    public int taskId;              // ID único no banco
    public string taskName;
    [TextArea] public string description;

    [Header("Gameplay")]
    public float completionTime;    // segundos para completar (com animação)
    public TaskCategory category;   // Repair, Collect, Ritual, Observation, Cleanup
    public string animationTrigger; // nome do trigger no Animator do personagem

    [Header("Visual")]
    public Sprite taskIcon;
    public string locationHint;     // dica visual do local no mapa
}

public enum TaskCategory
{
    Repair,      // Reparos e Manutenção
    Collect,     // Coleta e Entrega
    Ritual,      // Rituais e Ativações
    Observation, // Observação e Registro
    Cleanup      // Limpeza e Organização
}
```

---

## TaskSpot.cs

Componente no objeto do cenário que representa um ponto de missão.

```csharp
public class TaskSpot : MonoBehaviour
{
    [SerializeField] private TaskDefinition _taskDefinition;
    [SerializeField] private Transform _interactPoint; // posição exata onde o player para
    [SerializeField] private GameObject _interactPrompt; // "Pressione E"

    public TaskDefinition Definition => _taskDefinition;
    public int TaskId => _taskDefinition.taskId;
    public Vector2 InteractPosition => _interactPoint.position;

    private bool _isOccupied = false; // evita dois players na mesma missão

    public bool CanInteract(ulong requestingPlayerId)
        => !_isOccupied && !_completed;

    public void SetOccupied(bool occupied) => _isOccupied = occupied;
}
```

---

## TaskBase.cs (Classe Abstrata)

Base para comportamentos específicos de missão (se uma missão precisar de lógica especial).

```csharp
public abstract class TaskBase : MonoBehaviour
{
    protected TaskDefinition _definition;
    protected bool _isCompleted = false;
    protected bool _isInProgress = false;
    protected ulong _performerClientId;

    // Chamado pelo TaskManager quando o Host valida o início
    public virtual void BeginTask(ulong performerClientId)
    {
        _isInProgress = true;
        _performerClientId = performerClientId;
    }

    // Chamado quando o timer de completion termina
    public virtual void CompleteTask()
    {
        _isCompleted = true;
        _isInProgress = false;
    }

    // Interrompido (player saiu do range, morreu, etc.)
    public virtual void InterruptTask()
    {
        _isInProgress = false;
        _performerClientId = 0;
    }
}
```

---

## TaskManager.cs

Singleton autoritativo no Host. Gerencia todas as missões da partida.

```csharp
public class TaskManager : SingletonNetwork<TaskManager>
{
    // Progresso global (visível para todos)
    public NetworkVariable<int> TotalTasksCompleted = new NetworkVariable<int>(0, ...);
    public NetworkVariable<int> TotalTasksRequired  = new NetworkVariable<int>(0, ...);

    // Missões atribuídas por jogador (Host only)
    private Dictionary<ulong, List<int>> _playerTaskIds = new();

    // Estado das missões (Host only)
    private Dictionary<int, TaskState> _taskStates = new();

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        // Inicializa quando a partida começa
        GameEvents.OnMatchStarted += InitializeTasks;
    }

    // Host only — sorteia e distribui missões
    public void InitializeTasks(List<ulong> innocentClientIds, MatchConfig config)
    {
        if (!IsServer) return;

        var allTasks = Resources.LoadAll<TaskDefinition>("ScriptableObjects/Tasks");
        int tasksPerPlayer = config.tasksPerPlayer;

        int totalRequired = 0;
        
        foreach (var clientId in innocentClientIds)
        {
            // Sorteia missões únicas para este jogador
            var shuffled = allTasks.OrderBy(_ => Random.value).Take(tasksPerPlayer).ToList();
            _playerTaskIds[clientId] = shuffled.Select(t => t.taskId).ToList();

            // Envia lista de missões para o jogador (só ele recebe)
            var targetParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
            ReceiveTaskAssignmentsClientRpc(
                _playerTaskIds[clientId].ToArray(), 
                targetParams
            );

            totalRequired += tasksPerPlayer;
        }

        TotalTasksRequired.Value = totalRequired;
    }

    // ServerRpc — client solicita iniciar uma missão
    [ServerRpc(RequireOwnership = true)]
    public void RequestInteractTaskServerRpc(int taskId, ServerRpcParams p = default)
    {
        var senderId = p.Receive.SenderClientId;

        // 1. Sender é Inocente?
        if (RoleManager.Instance.GetRole(senderId) != PlayerRole.Innocent) return;

        // 2. Missão pertence ao jogador?
        if (!_playerTaskIds[senderId].Contains(taskId)) return;

        // 3. Missão não foi completada ainda?
        if (_taskStates[taskId] == TaskState.Completed) return;

        // 4. Missão não está sendo feita por outro?
        if (_taskStates[taskId] == TaskState.InProgress) return;

        // 5. Jogador está no range do TaskSpot?
        var spot = GetTaskSpot(taskId);
        float dist = Vector2.Distance(
            GetPlayerPosition(senderId),
            spot.InteractPosition
        );
        if (dist > GameConstants.TASK_INTERACT_RANGE) return;

        // Tudo válido — inicia
        _taskStates[taskId] = TaskState.InProgress;
        spot.SetOccupied(true);

        // Notifica o performer para iniciar animação
        var targetParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } }
        };
        BeginTaskAnimationClientRpc(taskId, spot.Definition.animationTrigger, spot.Definition.completionTime, targetParams);

        // Host agenda o completion
        StartCoroutine(CompleteTaskAfterDelay(taskId, senderId, spot.Definition.completionTime));
    }

    private IEnumerator CompleteTaskAfterDelay(int taskId, ulong performerId, float delay)
    {
        yield return new WaitForSeconds(delay);

        // Verifica se o jogador ainda está vivo e no range
        if (!GetPlayerById(performerId).IsAlive.Value)
        {
            CancelTask(taskId);
            yield break;
        }

        // Confirma completion
        _taskStates[taskId] = TaskState.Completed;
        TotalTasksCompleted.Value++;

        // Notifica o jogador
        var targetParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { performerId } }
        };
        ConfirmTaskCompletedClientRpc(taskId, targetParams);

        // Checa condição de vitória
        MatchManager.Instance.CheckWinConditions();
    }

    [ClientRpc]
    private void ReceiveTaskAssignmentsClientRpc(int[] taskIds, ClientRpcParams p = default)
    {
        // Client recebe suas missões — atualiza UI local
        GameEvents.OnTasksAssigned?.Invoke(taskIds);
    }

    [ClientRpc]
    private void BeginTaskAnimationClientRpc(int taskId, string animTrigger, float duration, ClientRpcParams p = default)
    {
        // Apenas o performer recebe — toca animação e mostra barra de progresso
        GameEvents.OnTaskBegin?.Invoke(taskId, animTrigger, duration);
    }

    [ClientRpc]
    private void ConfirmTaskCompletedClientRpc(int taskId, ClientRpcParams p = default)
    {
        GameEvents.OnTaskCompleted?.Invoke(taskId);
    }
}
```

---

## Progresso: Individual vs Global

| Dado | Onde fica | Como é acessado |
|---|---|---|
| Quais missões são minhas | `LocalPlayerData.MyTaskIds[]` | Apenas local, enviado via TargetClientRpc |
| Progresso individual | `TaskProgressTracker` local | HUD do jogador lê direto |
| Progresso global | `NetworkVariable<int> TotalTasksCompleted` | Visível para todos no HUD |
| Estado de cada missão | `_taskStates` no Host | Host only — clients não sabem o estado das missões dos outros |

---

## NPCs e Missões

NPCs realizam "missões fake" usando o mesmo sistema visual, mas sem enviar RPCs ao servidor:

```csharp
// NPCTaskExecutor.cs — Host only
public void ExecuteFakeTask(TaskSpot spot, float duration)
{
    // Move NPC até o spot (via A*)
    _aiPath.destination = spot.InteractPosition;
    
    // Quando chega, toca animação por X segundos
    StartCoroutine(PlayFakeTaskAnimation(spot, duration));
}

private IEnumerator PlayFakeTaskAnimation(TaskSpot spot, float duration)
{
    // Envia animação para todos via ClientRpc (é visual, não gameplay)
    PlayNPCTaskAnimationClientRpc(OwnerClientId, spot.TaskId, spot.Definition.animationTrigger);
    
    yield return new WaitForSeconds(duration);
    
    // NPC "termina" e vai para outro destino
    _aiBrain.OnFakeTaskComplete();
}
```

Do ponto de vista visual, um NPC fazendo missão fake é **indistinguível** de um Inocente fazendo missão real.

---

*[← Voltar ao índice](../../README.md)*
