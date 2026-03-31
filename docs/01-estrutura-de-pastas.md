# 01 · Estrutura de Pastas

[← Voltar ao índice](../README.md)

> Referência completa de todas as pastas do projeto Unity, com regras de uso e o que deve ser evitado em cada uma.

---

## Raiz do Projeto

```
Assets/
├── _EchoesInTheDark/   ← todo o código e assets do jogo
├── Plugins/            ← SDKs de terceiros (read-only)
└── Tests/              ← testes automatizados
```

**Por que o underscore em `_EchoesInTheDark`?**  
Força a pasta a aparecer no topo do Project Window, separando o projeto dos pacotes externos.

---

## `Scripts/`

```
Scripts/
├── Core/
├── Network/
├── Gameplay/
│   ├── Character/
│   ├── Player/
│   ├── NPC/
│   ├── Roles/
│   ├── Tasks/
│   ├── Lighting/
│   ├── Meeting/
│   └── Match/
├── UI/
│   ├── Components/
│   ├── HUD/
│   ├── Lobby/
│   ├── Meeting/
│   └── Menus/
└── Services/
```

---

### `Scripts/Core/`

Sistemas que existem durante **toda a vida do jogo** via `DontDestroyOnLoad`. Não dependem de nenhum outro módulo.

| Arquivo | Status | Responsabilidade |
|---|---|---|
| `Bootstrap.cs` | ✅ Implementado | Entry point — Logo → Intro → Loading → MainMenu |
| `SceneLoader.cs` | ✅ Implementado | Centraliza toda navegação entre cenas |
| `SettingsManager.cs` | ✅ Implementado | Persiste e aplica configurações via PlayerPrefs |
| `InputManager.cs` | ✅ Implementado | Mantém InputActionAsset + overrides de bindings |
| `GameEvents.cs` | ⏳ Futuro | Event bus global para comunicação desacoplada |
| `SingletonNetwork.cs` | ⏳ Futuro | Classe base para singletons NetworkBehaviour |
| `AppState.cs` | ⏳ Futuro | Estado global da aplicação |

**✅ O que entra:** Scripts de inicialização, carregamento, eventos globais, singletons de sessão.  
**🚫 O que evitar:** Lógica de roles, tasks, UI ou referências diretas ao NetworkManager.

---

### `Scripts/Network/`

Gerencia o **ciclo de vida da conexão**. Não contém lógica de jogo.

| Arquivo | Status | Responsabilidade |
|---|---|---|
| `NetworkConnectionManager.cs` | ⏳ Futuro | StartHost, StartClient, Shutdown |
| `NetworkSpawnManager.cs` | ⏳ Futuro | Spawn de NetworkObjects |
| `HostManager.cs` | ⏳ Futuro | Lógica exclusiva do Host |
| `ClientManager.cs` | ⏳ Futuro | Lógica exclusiva do Client |
| `NetworkEventRelay.cs` | ⏳ Futuro | Ponte entre eventos de rede e GameEvents |

---

### `Scripts/Services/`

Integração com Unity Services. **Abstrai os SDKs externos** — o resto do jogo nunca chama o SDK diretamente.

| Arquivo | Status | Responsabilidade |
|---|---|---|
| `RelayNetworkService.cs` | ✅ Implementado | Aloca servidor Relay, configura UnityTransport |
| `LobbyNetworkService.cs` | ✅ Implementado | Cria/entra em salas, heartbeat, refresh de players |
| `SessionService.cs` | ⏳ Futuro | Estado persistente da sessão |

> **Por que `LobbyNetworkService` e não `LobbyService`?**  
> Evitar conflito de nome com `Unity.Services.Lobbies.LobbyService` (SDK da Unity).

> **Por que `RelayNetworkService` e não `RelayService`?**  
> Evitar conflito de nome com `Unity.Services.Relay.RelayService` (SDK da Unity).

---

### `Scripts/UI/Menus/`

| Arquivo | Status | Responsabilidade |
|---|---|---|
| `MainMenuController.cs` | ✅ Implementado | Botões do MainMenu → delega para SceneLoader e SettingsController |
| `SettingsController.cs` | ✅ Implementado | 4 abas de configurações — coordena SettingsManager |

### `Scripts/UI/Lobby/`

| Arquivo | Status | Responsabilidade |
|---|---|---|
| `LobbyController.cs` | 🔄 Em andamento | Coordena UI do Lobby com RelayNetworkService e LobbyNetworkService |

### `Scripts/UI/Components/`

Componentes reutilizáveis de UI, independentes de contexto.

| Arquivo | Status | Responsabilidade |
|---|---|---|
| `VolumeStepControl.cs` | ✅ Implementado | Controle de volume em 5 degraus visuais (< barras >) |
| `RebindButton.cs` | ✅ Implementado | Botão de rebind de tecla com detecção de conflito |

---

### `Scripts/Gameplay/Character/`

Compartilhado entre Player e NPC. Montagem visual do marshmallow.

| Arquivo | Responsabilidade |
|---|---|
| `CharacterVisuals.cs` | Aplica cor + sprites + acessórios |
| `CharacterAnimator.cs` | Controla animações |
| `CharacterNetworkSync.cs` | Sincroniza cor e variantes via NetworkVariable |
| `CharacterState.cs` | Estado atual: vivo, morto, fazendo missão |
| `CharacterColorData.cs` | ScriptableObject — mapeia enum de cor para sprites |

---

### `Scripts/Gameplay/Player/`

| Arquivo | Responsabilidade |
|---|---|
| `PlayerInputHandler.cs` | Lê input do New Input System |
| `PlayerMovement.cs` | Aplica movimento ao Rigidbody2D |
| `PlayerNetworkSync.cs` | NetworkBehaviour — sincroniza posição e estado |
| `PlayerState.cs` | Estado: papel, alive, carregando tocha |
| `PlayerInteraction.cs` | Detecta e executa interações |

---

### `Scripts/Gameplay/NPC/`

IA exclusivamente no Host.

| Arquivo | Responsabilidade |
|---|---|
| `NPCController.cs` | Coordena todos os componentes do NPC |
| `NPCAIBrain.cs` | Decide próximo objetivo (Host only) |
| `NPCTaskExecutor.cs` | Executa missão fake |
| `NPCLightInteraction.cs` | Comportamento de parada em tochas |

---

### `Scripts/Gameplay/Roles/`

| Arquivo | Responsabilidade |
|---|---|
| `RoleManager.cs` | Sorteia e distribui papéis (Host only) |
| `RoleDefinition.cs` | ScriptableObject — dados do papel |
| `IRoleAbility.cs` | Interface para habilidades |
| `InnocentRole.cs` | Habilidades do Inocente |
| `VampireRole.cs` | Habilidades do Vampiro |
| `GuardRole.cs` | Habilidades do Guarda |

---

### `Scripts/Gameplay/Tasks/`

| Arquivo | Responsabilidade |
|---|---|
| `TaskBase.cs` | Classe abstrata base |
| `ITask.cs` | Interface de contrato |
| `TaskManager.cs` | Gerencia missões ativas (Host) |
| `TaskProgressTracker.cs` | Progresso individual |
| `TaskDefinition.cs` | ScriptableObject — dados de uma missão |

---

### `Scripts/Gameplay/Lighting/`

| Arquivo | Responsabilidade |
|---|---|
| `TorchBehavior.cs` | NetworkBehaviour — `NetworkVariable<bool> IsLit` |
| `LightSourceState.cs` | Interface `ILightSource` |
| `PlayerVisibilityController.cs` | Visibilidade local (não sincronizado) |
| `DarknessManager.cs` | Camada de escuridão global |

---

### `Scripts/Gameplay/Meeting/`

| Arquivo | Responsabilidade |
|---|---|
| `MeetingManager.cs` | Ciclo de vida da reunião |
| `VotingSystem.cs` | Coleta e contabiliza votos |
| `MeetingResultResolver.cs` | Calcula resultado e executa banimento |
| `BodyReportHandler.cs` | Valida reporte de corpo |

---

### `Scripts/Gameplay/Match/`

| Arquivo | Responsabilidade |
|---|---|
| `MatchStateMachine.cs` | Estados: Lobby → Playing → Meeting → Ended |
| `MatchManager.cs` | Coordena todos os sistemas |
| `MatchConfig.cs` | Configurações da partida |
| `MatchResultHandler.cs` | Calcula vencedores e XP |
| `PlayerSpawnCoordinator.cs` | Spawn coordenado de até 15 players |

---

### `Scripts/UI/`

**Regra absoluta:** Scripts de UI apenas leem eventos e exibem dados. Nunca chamam `NetworkManager`, `RoleManager` ou sistemas de gameplay diretamente.

```
UI/
├── Components/ → VolumeStepControl, RebindButton (reutilizáveis)
├── HUD/        → papel, missões, cooldowns, alertas, FPSDisplay
├── Lobby/      → LobbyController, PlayerListItem
├── Meeting/    → avatares, votos, timer
└── Menus/      → MainMenuController, SettingsController
```

---

## `Prefabs/`

```
Prefabs/
├── Network/
│   ├── NetworkManager.prefab         ✅ criado e configurado
│   ├── PlayerNetworkObject.prefab    ⏳ próximas sessões
│   └── MatchNetworkState.prefab      ⏳ futuro
├── UI/
│   └── PlayerListItem.prefab         🔄 próxima sessão (lista do Lobby)
├── Players/
└── Environment/
```

---

## `Audio/`

```
Audio/
└── GameAudioMixer.mixer    ✅ criado
    ├── Master   → VolGeral  (parâmetro exposto)
    ├── Musica   → VolMusica (parâmetro exposto)
    ├── SFX      → VolSFX   (parâmetro exposto)
    └── Chat     → VolChat   (parâmetro exposto)
```

---

## `Input/`

```
Input/
└── EchoesInputActions.inputactions    ✅ criado
    └── Action Map: Gameplay
        ├── Mover (Vector2 — WASD Composite)
        ├── Interagir (E)
        ├── Chat (T)
        ├── Info (I)
        ├── Spray (X)
        ├── Provocar (C)
        ├── Ping (Middle Mouse)
        ├── Mapa (Space)
        └── Habilidade (Left Shift)
```

---

## `Scenes/`

| Cena | Índice | Status | Função |
|---|---|---|---|
| `Bootstrap.unity` | 0 | ✅ Implementada | Entry point, nunca descarregada |
| `MainMenu.unity` | 1 | ✅ Implementada | Tela inicial + Settings completo |
| `Lobby.unity` | 2 | 🔄 Em andamento | Criação/entrada de sala |
| `Match.unity` | 3 | ⏳ Futuro | Gameplay |

---

## `ScriptableObjects/`

```
ScriptableObjects/
├── Roles/          ← RoleDefinitionSO por papel
├── Tasks/          ← TaskDefinitionSO para 30+ missões
├── MatchSettings/  ← valores padrão do lobby
└── Audio/          ← AudioConfigSO
```

---

## `Plugins/`

```
Plugins/
└── AstarPathfinder/    ← A* Pathfinder Pro — NUNCA modificar
```

---

## `Resources/`

```
Resources/
└── Localization/       ← JSONs de localização (scaffold — implementação futura)
    ├── pt-BR.json
    └── en-US.json
```

---

*[← Voltar ao índice](../README.md)*
