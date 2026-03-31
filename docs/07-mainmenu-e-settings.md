# 07 · MainMenu e Settings

[← Voltar ao índice](../README.md)

> Tudo implementado na sessão de 31/03/2026: fluxo de intro do Bootstrap, SceneLoader, MainMenuController, sistema de Settings completo, AudioMixer, sistema de rebind de controles.

---

## Scripts Implementados

| Script | Namespace | Localização |
|---|---|---|
| `SceneLoader.cs` | `EchoesInTheDark.Core` | `Scripts/Core/` |
| `SettingsManager.cs` | `EchoesInTheDark.Core` | `Scripts/Core/` |
| `InputManager.cs` | `EchoesInTheDark.Core` | `Scripts/Core/` |
| `MainMenuController.cs` | `EchoesInTheDark.UI` | `Scripts/UI/Menus/` |
| `SettingsController.cs` | `EchoesInTheDark.UI` | `Scripts/UI/Menus/` |
| `VolumeStepControl.cs` | `EchoesInTheDark.UI` | `Scripts/UI/Components/` |
| `RebindButton.cs` | `EchoesInTheDark.UI` | `Scripts/UI/Components/` |

Assets criados:
- `Assets/_EchoesInTheDark/Audio/GameAudioMixer` — AudioMixer com 4 grupos
- `Assets/_EchoesInTheDark/Input/EchoesInputActions` — InputActionAsset com 9 actions

---

## SceneLoader.cs

Único ponto de navegação entre cenas. Nenhum outro script chama `SceneManager` diretamente.

```csharp
// Uso
SceneLoader.Instance.GoToLobby();
SceneLoader.Instance.GoToMainMenu();
SceneLoader.Instance.GoToMatch();
SceneLoader.Instance.QuitGame();
```

**Por que centralizar?** Permite adicionar loading screens, fade transitions ou validações de estado sem alterar os callers. Atualmente a transição é direta — em sessões futuras pode receber uma loading screen antes de `LoadScene`.

---

## MainMenuController.cs

Assina 4 botões em `OnEnable`, desassina em `OnDisable`. Sem lógica de negócio — apenas delegação.

```
ButtonJogar        → SceneLoader.Instance.GoToLobby()
ButtonConfiguracoes → SettingsController.Abrir()
ButtonShopping     → Debug.Log (placeholder — sessão futura)
ButtonSair         → SceneLoader.Instance.QuitGame()
```

---

## SettingsManager.cs

Singleton `DontDestroyOnLoad`. Persiste todas as configurações em `PlayerPrefs` e aplica no `Awake` via `ApplyAll()`.

### Grupos de configuração

| Grupo | Configurações | Aplicação |
|---|---|---|
| Geral | Idioma | TODO: LocalizationManager |
| Gráficos | Resolução, Qualidade, Display, ShowFPS | `Screen.SetResolution`, `QualitySettings` |
| Som | VolGeral, VolMusica, VolSFX, VolChat | `AudioMixer.SetFloat()` em dB |
| Controles | Sensibilidade | Aplicada no PlayerMovement (futuro) |

### Conversão de volume

O `AudioMixer` trabalha em dB. A conversão de linear (0–1) para dB:

```csharp
private static float LinearToDecibel(float linear)
    => linear > 0.001f ? Mathf.Log10(linear) * 20f : -80f;
```

Valor 0 → -80 dB (silêncio absoluto). Valor 1 → 0 dB (volume máximo).

### Resoluções disponíveis

```csharp
public static readonly (int w, int h)[] Resolucoes =
{
    (1024, 576), (1280, 720), (1366, 768),
    (1600, 900), (1920, 1080), (2560, 1080)
};
```

Padrão: índice 4 = 1920×1080.

---

## AudioMixer — GameAudioMixer

**Localização:** `Assets/_EchoesInTheDark/Audio/GameAudioMixer`

```
Master (VolGeral)
├── Musica  (VolMusica)
├── SFX     (VolSFX)
└── Chat    (VolChat)
```

Os 4 parâmetros de volume estão expostos via **Exposed Parameters** do AudioMixer, com os nomes exatos `VolGeral`, `VolMusica`, `VolSFX`, `VolChat`. O `SettingsManager` os acessa via `_audioMixer.SetFloat(nome, valorEmDb)`.

---

## SettingsController.cs

Controla a UI do painel de Settings. Responsabilidades:
- Mostrar/esconder abas via `MostrarAba()`
- `SincronizarUI()` — ao abrir o painel, lê estado atual do `SettingsManager` e popula todos os controles com `SetValueWithoutNotify()` (sem disparar eventos)
- Delega todas as mudanças para `SettingsManager`
- Salva via `SettingsManager.SaveAll()` ao fechar

### Estrutura da UI na MainMenu.unity

```
Canvas
├── MenuPanel          ← botões principais (sempre visível)
└── SettingsPanel      ← inativo por padrão; sobrepõe tudo
    ├── Background     ← Image preta semitransparente (bloqueia cliques)
    └── Janela         ← painel central
        ├── Header     ← título + botão X (fechar)
        ├── Abas       ← HorizontalLayoutGroup com 4 botões
        │   ├── BotaoGeral
        │   ├── BotaoGraficos
        │   ├── BotaoSom
        │   └── BotaoControles
        └── Conteudo
            ├── PainelGeral      ← ativo por padrão ao abrir
            ├── PainelGraficos   ← inativo
            ├── PainelSom        ← inativo
            └── PainelControles  ← inativo
```

---

## VolumeStepControl.cs

Componente reutilizável para controle de volume em 5 degraus visuais.

**Visual:** 5 `Image` em formato de escada (bar chart), com botões `<` e `>` nas laterais.

**Lógica:**
- Cada clique em `>` aumenta 0.2 (de 0.0 a 1.0)
- Cada clique em `<` diminui 0.2
- Cor ativa: `RGB(255, 255, 255)` — degrau aceso
- Cor inativa: `RGB(214, 214, 214)` — degrau apagado
- Botões nos extremos ficam `interactable = false` (feedback visual)

**Ordem das images no array `_degraus`:** Image(4), Image(3), Image(2), Image(1), Image — da menor para a maior barra.

**Integração:**
```csharp
// SettingsController assina o evento
_volumeGeral.OnValueChanged += SettingsManager.Instance.SetVolGeral;

// Sincronização sem disparar evento
_volumeGeral.SetValueWithoutNotify(s.VolGeral);
```

---

## InputManager.cs

Singleton `DontDestroyOnLoad` que mantém o `EchoesInputActions` carregado e habilitado durante toda a sessão.

**Action Map:** `Gameplay`

| Action | Tipo | Binding padrão |
|---|---|---|
| Mover | Value / Vector2 | WASD (2D Vector Composite) |
| Interagir | Button | E |
| Chat | Button | T |
| Info | Button | I |
| Spray | Button | X |
| Provocar | Button | C |
| Ping | Button | Middle Button (Mouse) |
| Mapa | Button | Space |
| Habilidade | Button | Left Shift |

**Persistência de overrides:** Salva/carrega todo o `InputActionAsset` serializado como JSON em `PlayerPrefs["InputBindingOverrides"]`.

---

## RebindButton.cs

Componente adicionado em cada botão de tecla no `PainelControles`.

### Binding Index — WASD (Composite)

```
Mover
├── WASD (Composite)  → índice 0 ← NÃO é rebindável (isComposite = true)
│   ├── Up: W         → índice 1
│   ├── Down: S       → índice 2
│   ├── Left: A       → índice 3
│   └── Right: D      → índice 4
```

### Fluxo de rebind

```
Jogador clica no botão da tecla
  │
  ├── isComposite? → BLOQUEIA (log de warning)
  │
  ├── OnRebindStarted.Invoke() → SettingsController abre PainelRebindEspera
  │
  ├── PerformInteractiveRebinding()
  │     ├── WithControlsExcluding("<Mouse>/position|delta")
  │     ├── WithControlsExcluding("<Keyboard>/escape")  ← ESC nunca pode ser atribuído
  │     └── WithCancelingThrough("<Keyboard>/escape")   ← ESC cancela e fecha painel
  │
  ├── OnComplete → ConcluirRebind(cancelado: false)
  │     ├── EncontrarConflito(novoPath)
  │     │     ├── Conflito? → RemoveBindingOverride + OnRebindConflito.Invoke(nomeAction)
  │     │     └── Sem conflito? → SalvarOverride() (PlayerPrefs JSON por action+index)
  │     └── OnRebindComplete.Invoke() → SettingsController fecha PainelRebindEspera
  │
  └── OnCancel → ConcluirRebind(cancelado: true)
        └── Reverte sem salvar + fecha painel
```

### Detecção de conflito

Antes de confirmar qualquer rebind, o script percorre **todos** os bindings de **todos** os action maps do `InputActionAsset` comparando o `effectivePath`. Se outra action já usa aquela tecla, o rebind é cancelado e `OnRebindConflito` é disparado com o nome da action conflitante.

### Exibição do texto da tecla

```csharp
string displayString = InputControlPath.ToHumanReadableString(
    _action.bindings[_bindingIndex].effectivePath,
    InputControlPath.HumanReadableStringOptions.OmitDevice
);
```

`OmitDevice` garante que só aparece `E`, `W`, `SPACE` — sem prefixos como "HOLD" ou "[Keyboard]".

---

## Localização — Scaffold

O `SettingsManager.SetIdioma()` salva o índice em `PlayerPrefs` mas ainda não aplica nada. O comentário `// TODO: LocalizationManager.Instance.SetLanguage(value)` marca o ponto de integração.

**Plano de localização:**
- Pasta: `Assets/_EchoesInTheDark/Resources/Localization/`
- Arquivos: `pt-BR.json`, `en-US.json`
- API: `LocalizationManager.Get("chave")` — interface idêntica ao pacote oficial da Unity para migração futura sem retrabalho

---

*[← Voltar ao índice](../README.md)*
