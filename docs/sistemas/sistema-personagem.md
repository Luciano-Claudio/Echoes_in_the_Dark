# Sistema · Personagem

[← Voltar ao índice](../../README.md)

> Arquitetura do sistema visual do marshmallow: montagem de cor + partes + acessórios, visibilidade na escuridão e sincronização em rede.

---

## Visão Geral

O personagem de cada jogador e NPC é montado em runtime a partir de 3 camadas independentes:

```
Personagem Final
 ├── Cor (1 de 13) — sorteada para todos as partes
 ├── Sprites base de cada parte (Head, Body, Hands, Feet)
 └── Acessório de cada parte (pode ser nenhum ou um)
```

Player e NPC usam **exatamente o mesmo sistema**. A diferença está no controlador (`PlayerInputHandler` vs `NPCAIBrain`), não no visual.

---

## Estrutura de Sprites

```
Art/Characters/Variants/
└── [Cor]/
    ├── Head/
    │   ├── [cor]_head_default.png
    │   ├── [cor]_head_hat01.png
    │   ├── [cor]_head_hat02.png
    │   ├── [cor]_head_crown.png
    │   └── ...
    ├── Body/
    │   ├── [cor]_body_default.png
    │   ├── [cor]_body_cape.png
    │   └── ...
    ├── Hands/
    │   ├── [cor]_hands_default.png
    │   ├── [cor]_hands_gloves.png
    │   └── ...
    └── Feet/
        ├── [cor]_feet_default.png
        ├── [cor]_feet_boots.png
        └── ...

Art/Characters/Eyes/
└── eyes_default.png   ← único sprite de olhos, compartilhado por todas as cores

Art/Characters/Guard/
├── Default/           ← skin padrão do Guarda (não segue o sistema de cores)
└── Premium/           ← skins compradas
```

**Convenção de nomenclatura:**  
`[cor]_[parte]_[variante].png` → ex: `red_head_hat01.png`, `cyan_body_cape.png`

---

## CharacterColorData (ScriptableObject)

Mapeia cada cor para os arrays de sprites disponíveis por parte.

```csharp
// ScriptableObjects/Characters/CharacterColorData.cs
[CreateAssetMenu(menuName = "EitD/Character/Color Data")]
public class CharacterColorData : ScriptableObject
{
    public CharacterColor color;
    
    public Sprite[] headVariants;   // todos os sprites de cabeça desta cor
    public Sprite[] bodyVariants;
    public Sprite[] handsVariants;
    public Sprite[] feetVariants;
}
```

**Uso:** Um array de `CharacterColorData[]` no `CharacterVisuals` mapeia cada enum `CharacterColor` para o SO correto. Acesso por índice = acesso O(1).

---

## CharacterVisuals.cs

Responsável por montar o personagem visualmente. Não tem lógica de rede.

```csharp
public class CharacterVisuals : MonoBehaviour
{
    [Header("Sprite Renderers")]
    [SerializeField] private SpriteRenderer _headRenderer;
    [SerializeField] private SpriteRenderer _bodyRenderer;
    [SerializeField] private SpriteRenderer _handsRenderer;
    [SerializeField] private SpriteRenderer _feetRenderer;
    [SerializeField] private SpriteRenderer _eyesRenderer;  // sempre visível no escuro

    [Header("Data")]
    [SerializeField] private CharacterColorData[] _allColorData; // índice = CharacterColor enum
    [SerializeField] private Sprite _eyesSprite;

    // Chamado quando NetworkVariables do owner mudam
    public void ApplyVariant(CharacterColor color, int headIdx, int bodyIdx, int handsIdx, int feetIdx)
    {
        var data = _allColorData[(int)color];

        _headRenderer.sprite   = data.headVariants[headIdx];
        _bodyRenderer.sprite   = data.bodyVariants[bodyIdx];
        _handsRenderer.sprite  = data.handsVariants[handsIdx];
        _feetRenderer.sprite   = data.feetVariants[feetIdx];
    }

    // Alterna visibilidade baseado em iluminação (chamado pelo PlayerVisibilityController)
    public void SetInDarkness(bool inDarkness)
    {
        _headRenderer.enabled  = !inDarkness;
        _bodyRenderer.enabled  = !inDarkness;
        _handsRenderer.enabled = !inDarkness;
        _feetRenderer.enabled  = !inDarkness;
        _eyesRenderer.enabled  = true; // olhos sempre visíveis
    }
}
```

---

## CharacterNetworkSync.cs

Sincroniza as variáveis visuais via rede. Liga a rede ao `CharacterVisuals`.

```csharp
public class CharacterNetworkSync : NetworkBehaviour
{
    // Dados visuais sincronizados
    public NetworkVariable<CharacterColor> BodyColor = new(CharacterColor.White, 
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public NetworkVariable<int> HeadVariantIndex = new(0,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public NetworkVariable<int> BodyVariantIndex  = new(0, ...);
    public NetworkVariable<int> HandsVariantIndex = new(0, ...);
    public NetworkVariable<int> FeetVariantIndex  = new(0, ...);

    private CharacterVisuals _visuals;

    public override void OnNetworkSpawn()
    {
        _visuals = GetComponent<CharacterVisuals>();
        
        // Assina mudanças para reagir visualmente
        BodyColor.OnValueChanged        += OnVisualsChanged;
        HeadVariantIndex.OnValueChanged += OnVisualsChanged;
        // ...
        
        // Aplica estado atual imediatamente (para clients que entram depois)
        ApplyCurrentVisuals();
    }

    private void OnVisualsChanged(/* parâmetros */)
        => ApplyCurrentVisuals();

    private void ApplyCurrentVisuals()
    {
        _visuals.ApplyVariant(
            BodyColor.Value,
            HeadVariantIndex.Value,
            BodyVariantIndex.Value,
            HandsVariantIndex.Value,
            FeetVariantIndex.Value
        );
    }
}
```

---

## Sorteio de Visual (Host)

O Host sorteia cor e variantes de cada parte no início da partida:

```csharp
// Em RoleManager.cs ou PlayerSpawnCoordinator.cs — só no Host
void AssignCharacterVisuals(ulong clientId)
{
    var sync = GetPlayerNetworkSync(clientId);
    
    // Sorteia cor aleatória
    sync.BodyColor.Value = (CharacterColor)Random.Range(0, 13);
    
    // Sorteia variante independente para cada parte
    var data = GetColorData(sync.BodyColor.Value);
    sync.HeadVariantIndex.Value  = Random.Range(0, data.headVariants.Length);
    sync.BodyVariantIndex.Value  = Random.Range(0, data.bodyVariants.Length);
    sync.HandsVariantIndex.Value = Random.Range(0, data.handsVariants.Length);
    sync.FeetVariantIndex.Value  = Random.Range(0, data.feetVariants.Length);
    
    // NetworkVariables propagam automaticamente para todos os clients
}
```

---

## Visibilidade na Escuridão

O sistema de visibilidade é **completamente local** — não há sincronização de "quem está na luz" ou "quem está no escuro".

```
Para cada personagem em cena (a cada frame ou quando luz muda):
  1. Verifica se a posição do personagem está dentro de alguma fonte de luz ativa
  2. Se sim → ShowFullVisual()
  3. Se não → ShowOnlyEyes()
```

```csharp
// PlayerVisibilityController.cs — roda em cada client, localmente
public class PlayerVisibilityController : MonoBehaviour
{
    [SerializeField] private CharacterVisuals _visuals;
    [SerializeField] private float _checkInterval = 0.1f; // não precisa checar todo frame

    private void Start()
        => InvokeRepeating(nameof(UpdateVisibility), 0f, _checkInterval);

    private void UpdateVisibility()
    {
        bool inLight = LightingManager.Instance.IsPositionInLight(transform.position);
        _visuals.SetInDarkness(!inLight);
    }
}
```

**Por que local e não sincronizado?**  
Cada jogador vê o mundo de forma ligeiramente diferente baseado em sua posição relativa às fontes de luz. Sincronizar visibilidade adicionaria complexidade sem benefício — o servidor não precisa saber o que cada client está vendo.

---

## Guarda — Exceção Visual

O Guarda não usa o sistema de cor aleatória. Usa uma skin dedicada:

```csharp
public void ApplyGuardSkin(GuardSkinType skinType = GuardSkinType.Default)
{
    var guardData = _guardSkinData[(int)skinType];
    _headRenderer.sprite  = guardData.head;
    _bodyRenderer.sprite  = guardData.body;
    _handsRenderer.sprite = guardData.hands;
    _feetRenderer.sprite  = guardData.feet;
}
```

A skin do Guarda **não revela o papel** imediatamente — outros jogadores precisam estar numa área iluminada para ver a skin e inferir que é o Guarda.

---

## Estado de Morte

Ao morrer, o personagem muda para o sprite de corpo no chão:

```csharp
public void SetDeadState()
{
    // Muda animação para "morto" (corpo no chão)
    GetComponent<CharacterAnimator>().PlayDeathAnimation();
    
    // Desativa colisão (outros passam por cima)
    GetComponent<Collider2D>().enabled = false;
    
    // Olhos ficam fechados/apagados
    _eyesRenderer.enabled = false;
}
```

---

*[← Voltar ao índice](../../README.md)*
