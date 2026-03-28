# Sistema · Iluminação

[← Voltar ao índice](../../README.md)

> Arquitetura da mecânica central de luz e escuridão: tochas, visibilidade, sincronização e a separação entre estado de rede e efeito visual.

---

## Princípio Central

> **Pela rede vai apenas: acesa (true) ou apagada (false).  
> Tudo visual é renderizado localmente por cada client.**

Este é o erro mais comum em jogos com iluminação multiplayer: tentar sincronizar dados de renderização (intensidade, raio, cor da luz) via rede. Isso é caro, desnecessário e cria dependência entre gameplay e visual.

---

## Tipos de Fonte de Luz

| Tipo | Onde | Pode apagar? | Sincronizado? |
|---|---|---|---|
| Luz de construção/missão | Edifícios e zonas de missão | Nunca | Não (sempre ativa) |
| Tocha de caminho | Caminhos da vila | Sim (Vampiro) | Sim (`NetworkVariable<bool>`) |
| Tocha portátil do jogador | Carregada pelo Inocente | Sim (Vampiro) | Sim (parte do `PlayerNetworkSync`) |

---

## TorchBehavior.cs

Componente em cada tocha do mapa. É um `NetworkBehaviour`.

```csharp
public class TorchBehavior : NetworkBehaviour
{
    // ─── O único dado sincronizado ─────────────────────────────
    public NetworkVariable<bool> IsLit = new NetworkVariable<bool>(
        defaultValue: true,
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server  // só Host escreve
    );

    // ─── Componentes visuais (locais, não sincronizados) ───────
    [SerializeField] private Light2D _torchLight;
    [SerializeField] private ParticleSystem _flameParticles;
    [SerializeField] private Animator _torchAnimator;
    [SerializeField] private AudioSource _torchAudio;

    public override void OnNetworkSpawn()
    {
        IsLit.OnValueChanged += OnLitStateChanged;
        ApplyLitState(IsLit.Value); // estado inicial
    }

    public override void OnNetworkDespawn()
    {
        IsLit.OnValueChanged -= OnLitStateChanged;
    }

    private void OnLitStateChanged(bool previous, bool current)
        => ApplyLitState(current);

    // Tudo visual — roda em cada client independentemente
    private void ApplyLitState(bool isLit)
    {
        _torchLight.enabled = isLit;
        _flameParticles.gameObject.SetActive(isLit);
        _torchAnimator.SetBool("isLit", isLit);
        _torchAudio.mute = !isLit;
    }

    // Chamado pelo Host ao validar pedido de apagar
    [Server]
    public void Extinguish() => IsLit.Value = false;

    [Server]
    public void Light()      => IsLit.Value = true;
}
```

---

## Fluxo: Vampiro Apaga Tocha

```
Vampiro (client)
  │
  ├─ Fica próximo à tocha
  ├─ Pressiona E (interação)
  └─ PlayerInteraction.cs detecta TorchBehavior no range
       └─ RequestBlowTorchServerRpc(torchNetworkObjectId)
              │
              Host
                ├─ Valida: sender é Vampiro?
                ├─ Valida: tocha está acesa?
                ├─ Valida: Vampiro está próximo? (posição real no servidor)
                └─ SE VÁLIDO: torch.Extinguish() → IsLit.Value = false
                                    │
                                    Todos os Clients
                                      └─ OnLitStateChanged(false) → ApplyLitState(false)
                                           └─ Light apagada, partículas desativadas, animação muda
```

---

## LightingManager.cs

Singleton local (não de rede) que mantém referências a todas as fontes de luz ativas. Usado pelo `PlayerVisibilityController` para checar se uma posição está iluminada.

```csharp
public class LightingManager : MonoBehaviour
{
    public static LightingManager Instance { get; private set; }

    // Todas as fontes de luz registradas (construções + tochas)
    private readonly List<ILightSource> _activeLightSources = new();

    public void RegisterLightSource(ILightSource source)
        => _activeLightSources.Add(source);

    public void UnregisterLightSource(ILightSource source)
        => _activeLightSources.Remove(source);

    /// Retorna true se a posição está dentro do raio de qualquer fonte de luz ativa
    public bool IsPositionInLight(Vector2 position)
    {
        foreach (var source in _activeLightSources)
        {
            if (!source.IsActive) continue;
            float dist = Vector2.Distance(position, source.Position);
            if (dist <= source.Radius) return true;
        }
        return false;
    }
}

public interface ILightSource
{
    bool IsActive { get; }
    Vector2 Position { get; }
    float Radius { get; }
}
```

---

## PlayerVisibilityController.cs

Roda em cada client. Atualiza a visibilidade de **todos** os personagens em cena baseado na posição deles em relação às luzes ativas.

```csharp
public class PlayerVisibilityController : MonoBehaviour
{
    [SerializeField] private CharacterVisuals _visuals;
    [SerializeField] private float _checkInterval = 0.1f;

    private void OnEnable()
        => InvokeRepeating(nameof(UpdateVisibility), 0f, _checkInterval);

    private void OnDisable()
        => CancelInvoke(nameof(UpdateVisibility));

    private void UpdateVisibility()
    {
        bool inLight = LightingManager.Instance.IsPositionInLight(transform.position);
        _visuals.SetInDarkness(!inLight);
    }
}
```

**Por que `InvokeRepeating` e não `Update`?**  
Checar colisão com todas as fontes de luz a cada frame (60x/s) para 15+ players é caro. A cada 100ms é imperceptível para o jogador e muito mais eficiente.

---

## Tocha Portátil do Jogador

O Inocente pode carregar uma tocha que se move com ele. O estado é parte do `PlayerNetworkSync`:

```csharp
// Em PlayerNetworkSync.cs
public NetworkVariable<bool> IsCarryingTorch = new NetworkVariable<bool>(false, ...);
```

Quando `IsCarryingTorch = true`, a tocha portátil é ativada e registrada no `LightingManager` como fonte de luz na posição do jogador, atualizando a posição a cada frame.

```csharp
// TorchCarrier.cs — componente no Player
public class TorchCarrier : MonoBehaviour
{
    [SerializeField] private GameObject _torchVisual;
    [SerializeField] private PortableLightSource _portableLightSource;

    private PlayerNetworkSync _sync;

    void Awake() => _sync = GetComponent<PlayerNetworkSync>();

    void OnEnable()
        => _sync.IsCarryingTorch.OnValueChanged += OnCarryingTorchChanged;

    void OnDisable()
        => _sync.IsCarryingTorch.OnValueChanged -= OnCarryingTorchChanged;

    void OnCarryingTorchChanged(bool prev, bool current)
    {
        _torchVisual.SetActive(current);
        
        if (current)
            LightingManager.Instance.RegisterLightSource(_portableLightSource);
        else
            LightingManager.Instance.UnregisterLightSource(_portableLightSource);
    }
}
```

---

## Escuridão Global (DarknessManager.cs)

Uma camada de escuridão cobre todo o mapa por padrão. As fontes de luz "recortam" essa camada usando o sistema de iluminação 2D do Unity (URP + Light2D).

```
Setup do URP:
  ├── Global Light 2D (intensidade muito baixa ≈ 0.05) → iluminação ambiente mínima
  ├── Spot Light 2D em cada tocha → círculo de luz
  └── Shadow Caster 2D em paredes/árvores → sombras dinâmicas
```

**Regra:** `DarknessManager` apenas controla a intensidade da `Global Light`. Não precisa de sincronização de rede — todos os clients aplicam localmente baseado no estado das `NetworkVariable` das tochas.

---

*[← Voltar ao índice](../../README.md)*
