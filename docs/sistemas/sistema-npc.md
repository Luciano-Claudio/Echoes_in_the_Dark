# Sistema · NPC

[← Voltar ao índice](../../README.md)

> Arquitetura dos NPCs: IA com A* Pathfinder Pro, comportamentos, sincronização em rede e integração com os outros sistemas.

---

## Princípio Fundamental

> **Toda lógica de IA roda exclusivamente no Host.  
> Clients recebem apenas posição e estado de animação.**

Nenhum client executa pathfinding ou toma decisões sobre NPCs. Isso elimina dessincronização e cheating.

---

## Arquitetura de Componentes

```
NPC (GameObject)
 ├── NetworkObject                  ← presença em rede
 ├── NPCController.cs               ← coordena todos os componentes
 ├── NPCAIBrain.cs                  ← decide o que fazer (Host only)
 ├── NPCTaskExecutor.cs             ← executa missão fake (Host only)
 ├── NPCLightInteraction.cs         ← comportamento de tocha (Host only)
 ├── NPCNetworkSync.cs              ← sincroniza posição e estado (NetworkBehaviour)
 ├── CharacterVisuals.cs            ← visual do marshmallow (compartilhado com Player)
 ├── CharacterNetworkSync.cs        ← sincroniza cor e variantes
 ├── PlayerVisibilityController.cs  ← visibilidade na escuridão (local em cada client)
 └── CharacterAnimator.cs           ← animações locais
```

---

## NPCNetworkSync.cs

Único NetworkBehaviour com responsabilidade de rede nos NPCs.

```csharp
public class NPCNetworkSync : NetworkBehaviour
{
    // Posição — Host escreve, todos leem
    public NetworkVariable<Vector2> Position = new NetworkVariable<Vector2>(
        Vector2.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Estado de comportamento — para animação correta em todos os clients
    public NetworkVariable<NPCBehaviorState> BehaviorState = new NetworkVariable<NPCBehaviorState>(
        NPCBehaviorState.Idle,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private CharacterAnimator _animator;

    public override void OnNetworkSpawn()
    {
        _animator = GetComponent<CharacterAnimator>();

        Position.OnValueChanged      += OnPositionChanged;
        BehaviorState.OnValueChanged += OnBehaviorStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        Position.OnValueChanged      -= OnPositionChanged;
        BehaviorState.OnValueChanged -= OnBehaviorStateChanged;
    }

    private void OnPositionChanged(Vector2 prev, Vector2 current)
    {
        // Clients movem o transform para a posição recebida
        // Interpolação suave para evitar teleporte
        transform.position = Vector2.Lerp(transform.position, current, Time.deltaTime * 10f);
    }

    private void OnBehaviorStateChanged(NPCBehaviorState prev, NPCBehaviorState current)
    {
        // Atualiza animação baseado no estado
        _animator.SetState(current);
    }

    // Chamado pelo NPCController no Host a cada intervalo
    [Server]
    public void SyncPosition()
    {
        Position.Value = transform.position;
    }
}
```

---

## NPCAIBrain.cs

Cérebro do NPC. Decide o próximo objetivo. **Só executa se `IsServer`.**

```csharp
public class NPCAIBrain : MonoBehaviour
{
    private NPCController _controller;
    private NPCBehaviorState _currentState = NPCBehaviorState.Idle;

    // Pesos de decisão (ajustáveis por ScriptableObject)
    [SerializeField] private float _taskWeight    = 0.5f;  // probabilidade de ir a missão
    [SerializeField] private float _torchWeight   = 0.25f; // probabilidade de parar na tocha
    [SerializeField] private float _wanderWeight  = 0.25f; // probabilidade de deambular

    private void Update()
    {
        // IA nunca roda em clients
        if (!NetworkManager.Singleton.IsServer) return;

        if (_currentState == NPCBehaviorState.Idle)
            DecideNextBehavior();
    }

    private void DecideNextBehavior()
    {
        float roll = Random.value;

        if (roll < _taskWeight)
            StartBehavior(NPCBehaviorState.WalkingToTask);
        else if (roll < _taskWeight + _torchWeight)
            StartBehavior(NPCBehaviorState.WalkingToTorch);
        else
            StartBehavior(NPCBehaviorState.Wandering);
    }

    private void StartBehavior(NPCBehaviorState newState)
    {
        _currentState = newState;
        _controller.ExecuteBehavior(newState);
    }

    // Chamado quando o comportamento atual termina
    public void OnBehaviorComplete()
    {
        _currentState = NPCBehaviorState.Idle;
        // Pequena pausa antes da próxima decisão
        StartCoroutine(IdleForSeconds(Random.Range(1f, 3f)));
    }

    private IEnumerator IdleForSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        DecideNextBehavior();
    }
}
```

---

## NPCController.cs

Coordena os componentes e executa o comportamento decidido pelo `NPCAIBrain`.

```csharp
public class NPCController : NetworkBehaviour
{
    [SerializeField] private NPCAIBrain      _brain;
    [SerializeField] private NPCTaskExecutor _taskExecutor;
    [SerializeField] private NPCLightInteraction _lightInteraction;
    [SerializeField] private NPCNetworkSync  _networkSync;

    // A* Pathfinder Pro
    private IAstarAI _aiPath;

    // Sincroniza posição a cada X frames para economizar bandwidth
    private int _syncFrameInterval = 3;
    private int _frameCount = 0;

    private void Awake()
        => _aiPath = GetComponent<IAstarAI>();

    private void Update()
    {
        if (!IsServer) return;

        // Sincroniza posição periodicamente, não todo frame
        _frameCount++;
        if (_frameCount >= _syncFrameInterval)
        {
            _networkSync.SyncPosition();
            _frameCount = 0;
        }
    }

    // Chamado pelo NPCAIBrain
    public void ExecuteBehavior(NPCBehaviorState state)
    {
        switch (state)
        {
            case NPCBehaviorState.WalkingToTask:
                var taskSpot = FindRandomAvailableTaskSpot();
                if (taskSpot != null)
                    _taskExecutor.WalkAndExecuteTask(taskSpot);
                else
                    _brain.OnBehaviorComplete(); // nenhuma missão disponível, decide outra coisa
                break;

            case NPCBehaviorState.WalkingToTorch:
                var torch = FindRandomActiveTorch();
                if (torch != null)
                    _lightInteraction.WalkToTorch(torch);
                else
                    _brain.OnBehaviorComplete();
                break;

            case NPCBehaviorState.Wandering:
                WalkToRandomPoint();
                break;
        }

        // Atualiza estado sincronizado
        _networkSync.BehaviorState.Value = state;
    }

    private void WalkToRandomPoint()
    {
        var destination = GetRandomPointOnGraph();
        _aiPath.destination = destination;
        
        StartCoroutine(WaitForArrivalThenComplete(Random.Range(3f, 8f)));
    }

    private IEnumerator WaitForArrivalThenComplete(float maxWait)
    {
        float elapsed = 0f;
        while (!_aiPath.reachedDestination && elapsed < maxWait)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        _brain.OnBehaviorComplete();
    }
}
```

---

## NPCTaskExecutor.cs

```csharp
public class NPCTaskExecutor : MonoBehaviour
{
    private IAstarAI _aiPath;
    private NPCController _controller;

    public void WalkAndExecuteTask(TaskSpot spot)
    {
        _aiPath.destination = spot.InteractPosition;
        StartCoroutine(WalkThenExecute(spot));
    }

    private IEnumerator WalkThenExecute(TaskSpot spot)
    {
        // Espera chegar
        while (!_aiPath.reachedDestination)
            yield return null;

        // Chegou — fica parado e anima
        _aiPath.isStopped = true;
        
        float duration = spot.Definition.completionTime + Random.Range(-1f, 2f); // pequena variação
        
        // Envia animação para clients via ClientRpc (apenas visual)
        PlayFakeTaskAnimationClientRpc(spot.TaskId, spot.Definition.animationTrigger, duration);
        
        yield return new WaitForSeconds(duration);
        
        _aiPath.isStopped = false;
        _controller.ExecuteBehavior(NPCBehaviorState.Idle);
    }

    [ClientRpc]
    private void PlayFakeTaskAnimationClientRpc(int taskId, string animTrigger, float duration)
    {
        // Toca animação no NPC em todos os clients — indistinguível da missão real
        GetComponent<Animator>().SetTrigger(animTrigger);
        // Barra de progresso visual opcional (sem enviar dado ao servidor)
    }
}
```

---

## NPCLightInteraction.cs

Simula o comportamento de parar próximo a tochas — comportamento que confunde Guardas.

```csharp
public class NPCLightInteraction : MonoBehaviour
{
    [SerializeField] private float _minStayTime = 5f;
    [SerializeField] private float _maxStayTime = 15f;

    private IAstarAI _aiPath;
    private NPCController _controller;

    public void WalkToTorch(TorchBehavior torch)
    {
        _aiPath.destination = torch.transform.position;
        StartCoroutine(WalkThenStay(torch));
    }

    private IEnumerator WalkThenStay(TorchBehavior torch)
    {
        while (!_aiPath.reachedDestination)
            yield return null;

        // Chegou — fica parado por tempo aleatório
        _aiPath.isStopped = true;
        _controller.NetworkSync.BehaviorState.Value = NPCBehaviorState.AtTorch;

        float stayTime = Random.Range(_minStayTime, _maxStayTime);
        yield return new WaitForSeconds(stayTime);

        // Se a tocha foi apagada, vai embora mais cedo
        if (!torch.IsLit.Value)
        {
            _aiPath.isStopped = false;
            _controller.Brain.OnBehaviorComplete();
            yield break;
        }

        _aiPath.isStopped = false;
        _controller.Brain.OnBehaviorComplete();
    }
}
```

---

## Integração com A* Pathfinder Pro

```csharp
// Configuração no projeto:
// - Grid Graph ou Point Graph sobre o mapa da vila
// - NPCs usam o componente AIPath ou RichAI do A*
// - Obstacle avoidance via LocalAvoidance (opcional, para grupos de NPCs)

// Acesso via interface IAstarAI (abstrai AIPath e RichAI)
private IAstarAI _aiPath;

void Awake()
{
    _aiPath = GetComponent<IAstarAI>();
}

// Mover para destino
_aiPath.destination = targetPosition;

// Verificar chegada
if (_aiPath.reachedDestination) { ... }

// Parar movimento
_aiPath.isStopped = true;
```

**Regra:** A* nunca é chamado em clients. Toda chamada a `_aiPath` está dentro de `if (!IsServer) return;` ou em classes que já garantem execução apenas no Host.

---

## Morte de NPC

NPCs podem ser mortos por Vampiros com o mesmo cooldown que matar jogadores:

```csharp
// No RequestKillTargetServerRpc do VampireRole.cs
// target pode ser Player ou NPC — mesma lógica

if (target.TryGetComponent<NPCController>(out var npc))
{
    // É um NPC — mata sem contar para condição de vitória
    npc.NetworkSync.BehaviorState.Value = NPCBehaviorState.Dead;
    OnNPCKilledClientRpc(target.NetworkObjectId);
    // Cooldown do Vampiro ainda é aplicado
}
else
{
    // É um jogador — conta para condição de vitória
    target.IsAlive.Value = false;
    OnPlayerKilledClientRpc(targetId);
}
```

---

*[← Voltar ao índice](../../README.md)*
