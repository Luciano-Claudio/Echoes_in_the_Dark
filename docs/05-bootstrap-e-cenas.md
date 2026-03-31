# 05 · Bootstrap e Cenas

[← Voltar ao índice](../README.md)

> Histórico completo de implementação do Bootstrap: da versão inicial (sessão 1) até o fluxo de intro completo (sessão atual).  
> Última atualização: 31/03/2026.

---

## Cenas do Projeto

| Cena | Índice | Status | Função |
|---|---|---|---|
| `Bootstrap.unity` | 0 | ✅ Implementada | Entry point — nunca descarregada |
| `MainMenu.unity` | 1 | ✅ Implementada | Tela inicial com Settings |
| `Lobby.unity` | 2 | 🔄 Em andamento | Criação e entrada de sala |
| `Match.unity` | 3 | ⏳ Futuro | Gameplay da partida |

> **Regra:** `Bootstrap.unity` DEVE ser índice 0 no Build Settings. O Unity carrega a primeira cena automaticamente ao iniciar a build.

---

## NetworkManager Prefab

**Localização:** `Assets/_EchoesInTheDark/Prefabs/Network/NetworkManager.prefab`

| Componente | Campo | Valor |
|---|---|---|
| Network Manager | Enable Scene Management | ✅ |
| Network Manager | Load Scene Time Out | 120 |
| Network Manager | Default Player Prefab | None |
| Unity Transport | Protocol Type | **Relay Unity Transport** |

> O prefab permanece configurado como Relay permanentemente. Em produção, o `LobbyController` chama `SetRelayServerData()` antes de `StartHost/Client`. No editor, o NGO é inicializado limpo (sem auto-connect) e o Lobby cuida de tudo.

---

## Hierarquia da Cena Bootstrap

```
Bootstrap.unity
├── Bootstrap          ← Bootstrap.cs (DontDestroyOnLoad)
├── NetworkManager     ← Network Manager + Unity Transport (DontDestroyOnLoad)
├── SceneLoader        ← SceneLoader.cs (DontDestroyOnLoad)
├── SettingsManager    ← SettingsManager.cs (DontDestroyOnLoad)
├── InputManager       ← InputManager.cs (DontDestroyOnLoad)
└── Canvas             ← UI de intro (destruída ao carregar MainMenu)
    ├── PanelLogo      ← logo do estúdio (ativo por padrão)
    ├── PanelIntro     ← animação/vídeo de intro (inativo)
    └── PanelLoading   ← barra de progresso + status (inativo)
```

---

## Fluxo do Bootstrap.cs

### Versão atual — fluxo completo com intro

```
Awake()
  │
  ├── DontDestroyOnLoad(gameObject)
  │
  ├── InitializeServicesAsync()  ← roda EM PARALELO com as telas de intro
  │     ├── InitializationOptions.SetProfile($"Player_{PID}")
  │     │     └── Isola sessão de autenticação por processo (MPPM safe)
  │     ├── UnityServices.InitializeAsync(options)
  │     ├── AuthenticationService.Instance.SignInAnonymouslyAsync()
  │     └── _servicesReady = true  (ou _servicesFailed = true em erro)
  │
  ├── ShowLogoAsync()
  │     ├── Ativa PanelLogo
  │     ├── Aguarda _logoDuration segundos (serializável no Inspector)
  │     ├── Skip: Keyboard.current.anyKey.wasPressedThisFrame (polling)
  │     └── Desativa PanelLogo + 2 frames de folga (evita input vazar)
  │
  ├── ShowIntroAsync()
  │     ├── Ativa PanelIntro
  │     ├── _videoPlayer.Prepare() → aguarda isPrepared
  │     ├── Subscreve _videoPlayer.loopPointReached via TaskCompletionSource
  │     ├── _videoPlayer.Play()
  │     ├── 3 frames de folga pós-Play (estabilização)
  │     ├── Loop: aguarda videoFinished.Task OU qualquer tecla
  │     └── _videoPlayer.Stop() + Desativa PanelIntro
  │
  ├── ShowLoadingAsync()
  │     ├── Ativa PanelLoading
  │     ├── Loop: aguarda _servicesReady com timeout de 15s
  │     │     └── _progressBar.value sobe até 90% durante espera
  │     ├── SE timeout → _servicesFailed = true
  │     ├── SE falha → exibe _textErro (jogo não avança)
  │     └── SE sucesso → _progressBar = 1.0, aguarda 500ms, avança
  │
  └── LoadMainMenu()
        └── SceneManager.LoadScene("MainMenu", Single)
```

### Por que inicializar serviços em paralelo com a intro?

O `SignInAnonymouslyAsync()` + `UnityServices.InitializeAsync()` demora 1–3 segundos dependendo da conexão. Em vez de exibir uma tela de loading imediatamente, aproveitamos esse tempo para mostrar a logo e o vídeo de intro. Quando o jogador chega na tela de loading, os serviços geralmente já estão prontos — a barra de progresso serve como fallback visual se ainda não terminou.

---

## Perfil por PID — Isolamento MPPM

**Problema:** Sem perfis separados, todas as instâncias MPPM compartilham o mesmo cache de autenticação. O Relay rejeita `JoinAllocationAsync` com "Not Found" porque a sessão do Virtual Player está contaminada pela sessão do Main Editor.

**Solução:**

```csharp
var options = new InitializationOptions();
#if UNITY_EDITOR
string profile = $"Player_{System.Diagnostics.Process.GetCurrentProcess().Id}";
options.SetProfile(profile);
#endif
await UnityServices.InitializeAsync(options);
```

Cada instância MPPM é um processo separado com PID único. O perfil garante que cada instância tem sua própria sessão de autenticação, PlayerID e cache de Relay — sem contaminação cruzada.

**Resultado esperado no console:**
```
[Bootstrap] Perfil: Player_10196   ← Main Editor
[Bootstrap] Perfil: Player_27520   ← Virtual Player (PID diferente)
```

---

## Auto-Connect — Decisão de Remoção

Na sessão inicial, `Bootstrap.cs` continha `AutoConnectInEditor()` que chamava `StartHost()` ou `StartClient()` direto, via detecção de `-mppmTag` nos args de linha de comando.

**Por que foi removido:**
- O Lobby agora gerencia toda a conexão (Host via Relay, Client via código)
- O auto-connect conflitava com o fluxo do Lobby — o NGO já estava como Host quando o usuário clicava "Criar Sala", causando o erro `Cannot start Host while an instance is already running`
- A solução foi mover toda a responsabilidade de conexão para o `LobbyController`
- O `Bootstrap` agora inicializa serviços e carrega o MainMenu — apenas isso

---

## Singletons DontDestroyOnLoad

Todos os singletons que persistem entre cenas são instanciados na `Bootstrap.unity`:

| Singleton | Script | Responsabilidade |
|---|---|---|
| `Bootstrap` | `Bootstrap.cs` | Entry point, fluxo de intro |
| `NetworkManager` | (Unity) | Conexão NGO |
| `SceneLoader` | `SceneLoader.cs` | Navegação entre cenas |
| `SettingsManager` | `SettingsManager.cs` | Configurações + PlayerPrefs |
| `InputManager` | `InputManager.cs` | InputActionAsset + overrides |

---

## Problemas Resolvidos (histórico completo)

| Problema | Causa | Solução |
|---|---|---|
| `Unity.Multiplayer.Playmode` não encontrado | MPPM 2.0 migrou para engine | `Environment.GetCommandLineArgs()` |
| `SetRelayServerData()` obrigatório | Transport Relay exige dados antes de Start | Auto-connect removido; Lobby gerencia |
| `Cannot start Host while running` | Bootstrap chamava StartHost + Lobby também | Remover AutoConnectInEditor |
| `Not Found: join code not found` | Perfis MPPM compartilhados; sessão contaminada | `SetProfile($"Player_{PID}")` |
| `NetworkPrefab cannot be null` | Entrada vazia na lista de prefabs | Remover entrada vazia → Apply All |
| `[Vivox] server is null or empty` | Vivox instalado sem configuração | Remover pacote Vivox |
| VideoPlayer pula intro | `VideoPlayer.length` retorna 0; `CallOnce` vaza | `loopPointReached` + `TaskCompletionSource` |
| "HOLD E" no texto de rebind | `GetBindingDisplayString` inclui modificadores | `InputControlPath.ToHumanReadableString` com `OmitDevice` |
| Rebind em Composite | `PerformInteractiveRebinding` rejeita index 0 (WASD) | Verificar `isComposite` antes de iniciar |

---

## Console esperado (Play Mode — estado atual)

```
✅ [Bootstrap] Perfil: Player_XXXXX
✅ [Bootstrap] Unity Services prontos.
✅ [Bootstrap] Autenticado. PlayerID: XXXXXXXXXXXX
✅ [Bootstrap] Vídeo iniciado. Aguardando término ou skip...
✅ [Bootstrap] Intro encerrada. Skip: False
✅ [Bootstrap] Pronto!
✅ [Bootstrap] Carregando MainMenu...
✅ [SettingsManager] Configurações carregadas e aplicadas.
```

---

*[← Voltar ao índice](../README.md)*
