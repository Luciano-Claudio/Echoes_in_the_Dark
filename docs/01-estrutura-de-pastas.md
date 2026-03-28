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
O underscore força a pasta a aparecer no topo do Project Window do Unity, separando visualmente o projeto dos pacotes externos e assets do editor.

---

## `_EchoesInTheDark/`

Pasta raiz do jogo. Tudo que pertence ao Echoes in the Dark vive aqui.

```
_EchoesInTheDark/
├── Scripts/
├── Art/
├── Audio/
├── Prefabs/
├── Scenes/
├── ScriptableObjects/
└── Settings/
```

---

## `Scripts/`

Todo o código C# do projeto. Cada subpasta é um módulo com responsabilidade única.

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
│   ├── HUD/
│   ├── Lobby/
│   ├── Meeting/
│   └── Menus/
└── Services/
```

---

### `Scripts/Core/`

Sistemas que existem durante **toda a vida do jogo**. Não dependem de nenhum outro módulo.

| Arquivo | Responsabilidade |
|---|---|
| `Bootstrap.cs` | Entry point do jogo, inicializa todos os sistemas em ordem |
| `SceneLoader.cs` | Carregamento de cenas com loading screen |
| `GameEvents.cs` | Event bus global para comunicação desacoplada |
| `SingletonNetwork.cs` | Classe base para singletons que sobrevivem entre cenas |
| `AppState.cs` | Estado global da aplicação (na tela de menu, no lobby, na partida...) |

**✅ O que entra:**  
Scripts de inicialização, carregamento, eventos globais, utilitários sem dependência de gameplay.

**🚫 O que evitar:**  
Lógica de roles, tasks, UI ou referências diretas ao NetworkManager.

---

### `Scripts/Network/`

Gerencia o **ciclo de vida da conexão**. Não contém lógica de jogo.

| Arquivo | Responsabilidade |
|---|---|
| `NetworkConnectionManager.cs` | Inicia e encerra conexões (StartHost, StartClient, Shutdown) |
| `NetworkSpawnManager.cs` | Coordena o spawn de NetworkObjects (players, NPCs) |
| `HostManager.cs` | Lógica exclusiva do Host (inicialização de partida, validações) |
| `ClientManager.cs` | Lógica exclusiva do Client (reconexão, estado local) |
| `NetworkEventRelay.cs` | Ponte entre eventos de rede e o GameEvents bus |

**✅ O que entra:**  
Tudo que gerencia conexão, desconexão, reconexão e spawn de objetos de rede.

**🚫 O que evitar:**  
Lógica de roles, tasks, iluminação. Esse módulo não sabe o que é um Vampiro.

---

### `Scripts/Gameplay/Character/`

**Compartilhado entre Player e NPC.** Montagem visual e animação do marshmallow.

| Arquivo | Responsabilidade |
|---|---|
| `CharacterVisuals.cs` | Monta o personagem: aplica cor + sprites de cada parte + acessórios |
| `CharacterAnimator.cs` | Controla animações (andar, missão, morte, ataque) |
| `CharacterNetworkSync.cs` | Sincroniza posição e estado visual via rede |
| `CharacterState.cs` | Estado atual: vivo, morto, fazendo missão, parado em tocha |
| `CharacterColorData.cs` | Mapeia enum de cor para os sprites corretos |

**Regra principal:**  
`Player/` e `NPC/` são controladores que **usam** `Character/`. Toda lógica de visual fica aqui.

---

### `Scripts/Gameplay/Player/`

Controle do personagem jogável por humano. Separado por responsabilidade para evitar o anti-pattern "PlayerController que faz tudo".

| Arquivo | Responsabilidade |
|---|---|
| `PlayerInputHandler.cs` | Lê input do New Input System, dispara eventos |
| `PlayerMovement.cs` | Aplica movimento ao Rigidbody2D com base nos eventos de input |
| `PlayerNetworkSync.cs` | NetworkBehaviour — sincroniza posição e estado |
| `PlayerState.cs` | Estado atual do jogador: papel, alive, carregando tocha, etc. |
| `PlayerInteraction.cs` | Detecta e executa interações (missão, reportar corpo, apagar tocha) |

**✅ O que entra:**  
Input, movimento, interação com objetos do mundo.

**🚫 O que evitar:**  
Lógica de role dentro de PlayerInputHandler. Input não sabe o que é um Vampiro.

---

### `Scripts/Gameplay/NPC/`

IA dos NPCs. **Roda exclusivamente no Host.**

| Arquivo | Responsabilidade |
|---|---|
| `NPCController.cs` | Coordena todos os componentes do NPC |
| `NPCAIBrain.cs` | Decide o próximo objetivo (missão fake, tocha, deambulação) |
| `NPCTaskExecutor.cs` | Executa a animação de missão fake no ponto escolhido |
| `NPCLightInteraction.cs` | Comportamento de parar próximo a tochas por tempo aleatório |

**Regra crítica:**  
`NPCAIBrain` só executa se `IsServer` for `true`. Clients nunca tomam decisões de IA.

---

### `Scripts/Gameplay/Roles/`

Sistema de papéis. **Sorteio e validação são autoritativos no Host.**

| Arquivo | Responsabilidade |
|---|---|
| `RoleManager.cs` | Sorteia e distribui papéis no início da partida (Host only) |
| `RoleDefinition.cs` | Dados puros de um papel (ScriptableObject) |
| `IRoleAbility.cs` | Interface para habilidades de papel |
| `InnocentRole.cs` | Implementação das habilidades do Inocente |
| `VampireRole.cs` | Implementação das habilidades do Vampiro |
| `GuardRole.cs` | Implementação das habilidades do Guarda |

**Separação obrigatória:**  
- **Dados** (`RoleDefinition.cs`): o que o papel é  
- **Comportamento** (`VampireRole.cs`): o que o papel faz  
- **Gerenciamento** (`RoleManager.cs`): quem tem qual papel

---

### `Scripts/Gameplay/Tasks/`

Sistema de missões. Banco extensível de 30+ missões.

| Arquivo | Responsabilidade |
|---|---|
| `TaskBase.cs` | Classe abstrata base para todas as missões |
| `ITask.cs` | Interface de contrato de missão |
| `TaskManager.cs` | Gerencia missões ativas, progresso global (Host) |
| `TaskProgressTracker.cs` | Rastreia progresso individual de cada jogador |
| `TaskDefinition.cs` | ScriptableObject com dados de uma missão específica |

**Fluxo:**  
Client aperta E → `PlayerInteraction` → ServerRpc → `TaskManager` valida → confirma para todos os clients.

---

### `Scripts/Gameplay/Lighting/`

Mecânica central de iluminação/escuridão.

| Arquivo | Responsabilidade |
|---|---|
| `TorchBehavior.cs` | NetworkBehaviour da tocha: estado acesa/apagada |
| `LightSourceState.cs` | `NetworkVariable<bool>` torchIsLit — a única coisa sincronizada |
| `PlayerVisibilityController.cs` | Decide o que o jogador local vê (local only) |
| `DarknessManager.cs` | Controla a camada de escuridão global do mapa |

**Regra de ouro:**  
`NetworkVariable<bool> torchIsLit` é tudo que vai pela rede. Efeitos visuais (círculo de luz, intensidade, chama) são renderizados localmente em cada client.

---

### `Scripts/Gameplay/Meeting/`

Reunião e votação. **Completamente autoritativo no Host.**

| Arquivo | Responsabilidade |
|---|---|
| `MeetingManager.cs` | Controla o ciclo de vida da reunião (abrir, fechar, timer) |
| `VotingSystem.cs` | Coleta e contabiliza votos |
| `MeetingResultResolver.cs` | Calcula resultado (maioria, empate), executa banimento |
| `BodyReportHandler.cs` | Valida e processa o reporte de corpo |

---

### `Scripts/Gameplay/Match/`

State machine e fluxo geral da partida.

| Arquivo | Responsabilidade |
|---|---|
| `MatchStateMachine.cs` | Estados: Lobby → Loading → Playing → Meeting → Voting → Resolution → Ended |
| `MatchManager.cs` | Coordena todos os sistemas durante a partida |
| `MatchConfig.cs` | Configurações da partida (vindas do lobby) |
| `MatchResultHandler.cs` | Calcula vencedores e distribui XP |
| `PlayerSpawnCoordinator.cs` | Spawn coordenado de até 15 players no início da partida |

---

### `Scripts/UI/`

Apresentação de dados e reação a eventos. **Nunca contém regra de negócio.**

```
UI/
├── HUD/         → ícone de papel, progresso de missões, cooldowns, alertas
├── Lobby/       → lista de players, configurações, chat de lobby
├── Meeting/     → avatares, votos em tempo real, timer
└── Menus/       → título, configurações, coleção, fim de partida
```

**Regra absoluta:**  
Scripts de UI apenas leem eventos e exibem dados. Nunca chamam `NetworkManager`, `RoleManager` ou qualquer sistema de gameplay diretamente.

---

### `Scripts/Services/`

Integração com Unity Services. Abstrai os SDKs externos.

| Arquivo | Responsabilidade |
|---|---|
| `SessionService.cs` | Cria, entra e sai de sessões via Multiplayer Services SDK |
| `LobbyService.cs` | Gerencia dados do lobby (lista de players, configurações) |
| `RelayService.cs` | Cria e entra em alocações do Relay |
| `ServiceBootstrapper.cs` | Inicializa o Unity Services SDK na ordem correta |

---

## `Art/`

```
Art/
├── Characters/
│   ├── Variants/
│   │   ├── White/
│   │   │   ├── Head/     ← sprites de cabeça branca (default + com acessórios)
│   │   │   ├── Body/
│   │   │   ├── Hands/
│   │   │   └── Feet/
│   │   ├── Red/
│   │   ├── Orange/
│   │   ├── Purple/
│   │   ├── Green/
│   │   ├── Blue/
│   │   ├── DarkRed/
│   │   ├── Yellow/
│   │   ├── LightGreen/
│   │   ├── Cyan/
│   │   ├── Pink/
│   │   ├── Brown/
│   │   └── Magenta/
│   ├── Eyes/             ← olhos visíveis no breu (compartilhados entre cores)
│   └── Guard/
│       ├── Default/
│       └── Premium/
├── Environment/
│   ├── Village/
│   ├── Lighting/         ← sprites de tochas, lanternas, halos de luz
│   └── Interactables/    ← objetos de missão
├── Animations/
│   ├── Player/
│   ├── NPC/
│   └── Tasks/            ← animações específicas de cada missão
└── UI/
    ├── HUD/
    ├── Lobby/
    └── Meeting/
```

**Regra de nomenclatura de sprites:**  
`[cor]_[parte]_[variante].png` → ex: `white_head_default.png`, `red_body_hat01.png`

---

## `Prefabs/`

```
Prefabs/
├── Network/
│   ├── NetworkManager.prefab         ← configurado com o Player Prefab registrado
│   ├── PlayerNetworkObject.prefab    ← prefab registrado no NetworkManager
│   └── MatchNetworkState.prefab      ← estado global da partida sincronizado
├── Players/
├── Environment/
└── UI/
```

**Regra crítica:**  
Todo prefab que será spawnado pela rede DEVE ter um `NetworkObject` component e DEVE estar registrado na lista de `NetworkPrefabs` do `NetworkManager`.

---

## `Scenes/`

| Cena | Função |
|---|---|
| `Bootstrap.unity` | Entry point. Inicializa serviços, nunca é descarregada |
| `MainMenu.unity` | Tela inicial, configurações, coleção |
| `Lobby.unity` | Criação/entrada de sala, configurações da partida |
| `Match.unity` | Gameplay completo da partida |

**Fluxo de cenas:**  
`Bootstrap` → `MainMenu` → `Lobby` → `Match` → (fim) → `Lobby`

---

## `ScriptableObjects/`

```
ScriptableObjects/
├── Roles/          ← RoleDefinitionSO para cada papel
├── Tasks/          ← TaskDefinitionSO para cada uma das 30+ missões
├── MatchSettings/  ← valores padrão das configurações do lobby
└── Audio/          ← AudioConfigSO com referências e volumes
```

---

## `Plugins/`

```
Plugins/
└── AstarPathfinder/    ← A* Pathfinder Pro — nunca modificar arquivos aqui
```

**Regra:**  
Pasta read-only. Nunca modificar arquivos de terceiros diretamente.

---

## `Tests/`

```
Tests/
├── EditMode/    ← lógica pura: RoleSystem, TaskSystem, state machine, resolução de votação
└── PlayMode/    ← integração: spawn, sincronização de NetworkVariable
```

Assembly Definitions necessárias em cada pasta para o Unity reconhecer como projetos de teste.

---

*[← Voltar ao índice](../README.md)*
