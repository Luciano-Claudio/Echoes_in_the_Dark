# 06 · Planejamento — MainMenu → Lobby

[← Voltar ao índice](../README.md)

> Plano completo das próximas duas sessões de implementação.  
> Leia antes de começar cada sessão para não perder contexto arquitetural.

---

## Visão Geral do Fluxo

```
Bootstrap (já implementado)
  └── LoadMainMenu()
        │
        ▼
[SESSÃO A] MainMenu.unity
  ├── UI: Botões Jogar, Configurações, Sair
  ├── MainMenuController.cs → reage a cliques
  └── SceneLoader.cs → transição para Lobby
        │
        ▼
[SESSÃO B] Lobby.unity — Criar Sala (Host)
  ├── LobbyService.cs → cria sala no Unity Lobby
  ├── RelayService.cs → aloca servidor Relay
  ├── Exibe código de 6 dígitos para compartilhar
  └── NetworkManager.StartHost() com dados do Relay
        │         │
        │    [SESSÃO B] Lobby.unity — Entrar na Sala (Client)
        │         ├── LobbyService.cs → busca sala pelo código
        │         ├── RelayService.cs → lê JoinCode dos dados do Lobby
        │         └── NetworkManager.StartClient() com dados do Relay
        │
        ▼ (quando Host clica em Iniciar)
Match.unity
```

---

# SESSÃO A — MainMenu

## Objetivo

Criar a cena `MainMenu.unity` com UI funcional básica e transição para o Lobby.

> **Filosofia da sessão:** A arte e estilo visual ficam para depois. O que importa agora é o **fluxo funcional**: clicar em Jogar leva ao Lobby. Tudo mais é placeholder.

---

## Estrutura da Cena MainMenu

```
MainMenu.unity
├── Main Camera
├── Canvas (Screen Space - Overlay)
│   └── MainMenuPanel
│       ├── Title (TextMeshProUGUI) — "Echoes in the Dark"
│       ├── ButtonJogar (Button + TextMeshProUGUI) — "Jogar"
│       ├── ButtonConfiguracoes (Button + TextMeshProUGUI) — "Configurações"
│       └── ButtonSair (Button + TextMeshProUGUI) — "Sair"
└── MainMenuManager (GameObject vazio)
    └── MainMenuController.cs
```

---

## Scripts da Sessão A

### SceneLoader.cs

**Localização:** `Assets/_EchoesInTheDark/Scripts/Core/SceneLoader.cs`

Centraliza toda navegação entre cenas. Nenhum outro script chama `SceneManager` diretamente.

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoesInTheDark.Core
{
    /// <summary>
    /// Singleton simples (não de rede) para navegação entre cenas.
    /// Unico ponto de chamada para SceneManager no projeto.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        private const string SCENE_MAIN_MENU = "MainMenu";
        private const string SCENE_LOBBY     = "Lobby";
        private const string SCENE_MATCH     = "Match";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void GoToMainMenu()   => Load(SCENE_MAIN_MENU);
        public void GoToLobby()      => Load(SCENE_LOBBY);
        public void GoToMatch()      => Load(SCENE_MATCH);
        public void QuitGame()       => Application.Quit();

        private void Load(string sceneName)
            => SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}
```

> **Nota de evolução:** Em sessões futuras, este SceneLoader pode ganhar uma loading screen antes de `LoadScene`. Por ora, a transição é direta.

---

### MainMenuController.cs

**Localização:** `Assets/_EchoesInTheDark/Scripts/UI/Menus/MainMenuController.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using EchoesInTheDark.Core;

namespace EchoesInTheDark.UI
{
    /// <summary>
    /// Controla os botões da tela principal.
    /// Não contém lógica de negócio — apenas delega para SceneLoader.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button _buttonJogar;
        [SerializeField] private Button _buttonConfiguracoes;
        [SerializeField] private Button _buttonSair;

        private void OnEnable()
        {
            _buttonJogar.onClick.AddListener(OnJogarClicked);
            _buttonConfiguracoes.onClick.AddListener(OnConfiguracoesClicked);
            _buttonSair.onClick.AddListener(OnSairClicked);
        }

        private void OnDisable()
        {
            _buttonJogar.onClick.RemoveListener(OnJogarClicked);
            _buttonConfiguracoes.onClick.RemoveListener(OnConfiguracoesClicked);
            _buttonSair.onClick.RemoveListener(OnSairClicked);
        }

        private void OnJogarClicked()        => SceneLoader.Instance.GoToLobby();
        private void OnConfiguracoesClicked() => Debug.Log("[MainMenu] Configurações — em breve.");
        private void OnSairClicked()          => SceneLoader.Instance.QuitGame();
    }
}
```

---

## Atualização do Bootstrap

Após criar o `SceneLoader`, o `Bootstrap.cs` deve delegar a navegação para ele:

```csharp
// Substituir LoadMainMenu() no Bootstrap.cs
private void LoadMainMenu()
{
    Debug.Log("[Bootstrap] Carregando MainMenu...");
    // SceneLoader ainda não está na cena na primeira inicialização
    // então chamamos direto aqui — sem problema pois Bootstrap roda antes de tudo
    SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
}
```

> O `SceneLoader` será instanciado como parte do Bootstrap em sessão futura (quando houver loading screen). Por ora, o Bootstrap continua chamando `SceneManager` diretamente.

---

## Checklist da Sessão A

- [ ] Criar `SceneLoader.cs` em `Scripts/Core/`
- [ ] Criar `MainMenuController.cs` em `Scripts/UI/Menus/`
- [ ] Montar UI na cena `MainMenu.unity` (Canvas + 3 botões)
- [ ] Configurar EventSystem na cena (gerado automaticamente com Canvas)
- [ ] Linkar botões ao `MainMenuController` no Inspector
- [ ] Testar: Play → Bootstrap → MainMenu → clicar Jogar → Lobby
- [ ] Confirmar que NetworkManager persiste (DontDestroyOnLoad continua ativo)

---

---

# SESSÃO B — Lobby

## Objetivo

Implementar o fluxo completo de conexão real via Unity Relay:
- **Host:** criar sala → alocar Relay → exibir código → `StartHost()`
- **Client:** digitar código → entrar no Lobby → configurar Relay → `StartClient()`

---

## Estrutura da Cena Lobby

```
Lobby.unity
├── Main Camera
├── Canvas (Screen Space - Overlay)
│   ├── PanelEscolha             ← "Criar Sala" ou "Entrar com Código"
│   │   ├── ButtonCriarSala
│   │   └── ButtonEntrarCodigo
│   │
│   ├── PanelHost                ← visível após "Criar Sala"
│   │   ├── TextCodigo           ← código de 6 dígitos exibido
│   │   ├── ListaJogadores       ← nomes dos conectados
│   │   ├── ButtonIniciarPartida ← só ativo quando há jogadores suficientes
│   │   └── ButtonVoltar
│   │
│   └── PanelClient              ← visível após "Entrar com Código"
│       ├── InputFieldCodigo     ← campo de texto
│       ├── ButtonEntrar
│       └── ButtonVoltar
│
└── LobbyManager (GameObject vazio)
    └── LobbyController.cs
```

---

## Scripts da Sessão B

### RelayService.cs

**Localização:** `Assets/_EchoesInTheDark/Scripts/Services/RelayService.cs`

Abstrai completamente o SDK do Relay. Nenhum outro script chama o SDK diretamente.

```csharp
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace EchoesInTheDark.Services
{
    /// <summary>
    /// Abstrai o Unity Relay Service.
    /// Host: aloca servidor e obtém JoinCode.
    /// Client: entra na alocação usando o JoinCode.
    /// </summary>
    public class RelayService
    {
        private const int MAX_CONNECTIONS = 15; // máximo de players - 1 (host não conta)

        // Host: cria alocação e retorna o JoinCode (ex: "ABCD12")
        public async Task<string> CreateRelayAndGetJoinCode()
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MAX_CONNECTIONS);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Configura o Transport com os dados da alocação
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

            Debug.Log($"[RelayService] Alocação criada. JoinCode: {joinCode}");
            return joinCode;
        }

        // Client: entra na alocação usando o JoinCode
        public async Task JoinRelay(string joinCode)
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            Debug.Log($"[RelayService] Conectado ao Relay. JoinCode: {joinCode}");
        }
    }
}
```

---

### LobbyNetworkService.cs

**Localização:** `Assets/_EchoesInTheDark/Scripts/Services/LobbyNetworkService.cs`

> Nomear `LobbyNetworkService` (e não `LobbyService`) para evitar conflito de nome com `Unity.Services.Lobbies.LobbyService`.

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace EchoesInTheDark.Services
{
    /// <summary>
    /// Abstrai o Unity Lobby Service.
    /// Gerencia criação, entrada e heartbeat de salas.
    /// </summary>
    public class LobbyNetworkService
    {
        private const string KEY_RELAY_CODE = "RelayJoinCode";
        private Lobby _currentLobby;
        private float _heartbeatTimer;
        private const float HEARTBEAT_INTERVAL = 15f;

        // Host: cria sala e guarda o JoinCode do Relay nos dados
        public async Task<Lobby> CreateLobby(string lobbyName, string relayJoinCode)
        {
            var options = new CreateLobbyOptions
            {
                IsPrivate = true, // acesso só por código
                Data = new Dictionary<string, DataObject>
                {
                    {
                        KEY_RELAY_CODE,
                        new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode)
                    }
                }
            };

            _currentLobby = await LobbyService.Instance.CreateLobbyAsync(
                lobbyName,
                maxPlayers: 16,
                options
            );

            Debug.Log($"[LobbyNetworkService] Lobby criado: {_currentLobby.Id}");
            return _currentLobby;
        }

        // Client: entra na sala pelo código e lê o JoinCode do Relay
        public async Task<string> JoinLobbyAndGetRelayCode(string lobbyCode)
        {
            _currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            string relayCode = _currentLobby.Data[KEY_RELAY_CODE].Value;
            Debug.Log($"[LobbyNetworkService] Entrou no Lobby. RelayCode: {relayCode}");
            return relayCode;
        }

        // Deve ser chamado periodicamente pelo Host para manter o Lobby ativo
        public async Task SendHeartbeat()
        {
            if (_currentLobby == null) return;
            await LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
        }

        public string GetLobbyCode() => _currentLobby?.LobbyCode ?? "";
    }
}
```

---

### LobbyController.cs

**Localização:** `Assets/_EchoesInTheDark/Scripts/UI/Lobby/LobbyController.cs`

Coordena os serviços e a UI do Lobby. Segue a separação: UI não chama SDK; Controller coordena Serviços.

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EchoesInTheDark.Services;
using EchoesInTheDark.Core;
using Unity.Netcode;

namespace EchoesInTheDark.UI
{
    public class LobbyController : MonoBehaviour
    {
        [Header("Painéis")]
        [SerializeField] private GameObject _panelEscolha;
        [SerializeField] private GameObject _panelHost;
        [SerializeField] private GameObject _panelClient;

        [Header("Host UI")]
        [SerializeField] private TextMeshProUGUI _textCodigo;
        [SerializeField] private Button _buttonIniciarPartida;
        [SerializeField] private Button _buttonCriarSala;

        [Header("Client UI")]
        [SerializeField] private TMP_InputField _inputCodigo;
        [SerializeField] private Button _buttonEntrar;
        [SerializeField] private Button _buttonEntrarCodigo;

        [Header("Compartilhado")]
        [SerializeField] private Button _buttonVoltar;

        // Serviços (instanciados localmente — sem DI por enquanto)
        private readonly RelayService _relayService = new RelayService();
        private readonly LobbyNetworkService _lobbyService = new LobbyNetworkService();

        private void OnEnable()
        {
            _buttonCriarSala.onClick.AddListener(OnCriarSalaClicked);
            _buttonEntrarCodigo.onClick.AddListener(OnEntrarCodigoClicked);
            _buttonEntrar.onClick.AddListener(OnEntrarClicked);
            _buttonIniciarPartida.onClick.AddListener(OnIniciarPartidaClicked);
            _buttonVoltar.onClick.AddListener(OnVoltarClicked);
        }

        private void OnDisable()
        {
            _buttonCriarSala.onClick.RemoveAllListeners();
            _buttonEntrarCodigo.onClick.RemoveAllListeners();
            _buttonEntrar.onClick.RemoveAllListeners();
            _buttonIniciarPartida.onClick.RemoveAllListeners();
            _buttonVoltar.onClick.RemoveAllListeners();
        }

        // ── Host ──────────────────────────────────────────────────────────

        private async void OnCriarSalaClicked()
        {
            SetButtonsInteractable(false);

            try
            {
                // 1. Aloca Relay e obtém JoinCode
                string relayCode = await _relayService.CreateRelayAndGetJoinCode();

                // 2. Cria Lobby com o JoinCode nos dados
                await _lobbyService.CreateLobby("EitD-Sala", relayCode);

                // 3. Exibe código da sala (código do Lobby, não do Relay)
                string lobbyCode = _lobbyService.GetLobbyCode();
                _textCodigo.text = lobbyCode;

                // 4. Inicia como Host (Relay já configurado pelo RelayService)
                NetworkManager.Singleton.StartHost();

                // 5. Mostra painel do Host
                ShowPanel(_panelHost);

                Debug.Log($"[LobbyController] Host iniciado. Código da sala: {lobbyCode}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyController] Erro ao criar sala: {e.Message}");
                SetButtonsInteractable(true);
            }
        }

        // ── Client ────────────────────────────────────────────────────────

        private void OnEntrarCodigoClicked()
            => ShowPanel(_panelClient);

        private async void OnEntrarClicked()
        {
            string codigoDigitado = _inputCodigo.text.Trim().ToUpper();
            if (string.IsNullOrEmpty(codigoDigitado)) return;

            SetButtonsInteractable(false);

            try
            {
                // 1. Entra no Lobby e obtém RelayCode
                string relayCode = await _lobbyService.JoinLobbyAndGetRelayCode(codigoDigitado);

                // 2. Configura Relay com o código obtido
                await _relayService.JoinRelay(relayCode);

                // 3. Conecta como Client
                NetworkManager.Singleton.StartClient();

                Debug.Log("[LobbyController] Client conectado via Relay.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyController] Erro ao entrar na sala: {e.Message}");
                SetButtonsInteractable(true);
            }
        }

        // ── Match ─────────────────────────────────────────────────────────

        private void OnIniciarPartidaClicked()
        {
            // Só o Host pode iniciar — a validação real vem do NetworkManager
            if (!NetworkManager.Singleton.IsHost) return;

            // TODO (sessão futura): checar número mínimo de jogadores
            SceneLoader.Instance.GoToMatch();
        }

        // ── Navegação ─────────────────────────────────────────────────────

        private void OnVoltarClicked()
        {
            // Se já conectado, desconectar antes de voltar
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
                NetworkManager.Singleton.Shutdown();

            SceneLoader.Instance.GoToMainMenu();
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void ShowPanel(GameObject panel)
        {
            _panelEscolha.SetActive(false);
            _panelHost.SetActive(false);
            _panelClient.SetActive(false);
            panel.SetActive(true);
        }

        private void SetButtonsInteractable(bool value)
        {
            _buttonCriarSala.interactable = value;
            _buttonEntrar.interactable = value;
        }
    }
}
```

---

## Fluxo Completo da Conexão (Sessão B)

```
HOST
  OnCriarSalaClicked()
    │
    ├─ RelayService.CreateRelayAndGetJoinCode()
    │     ├─ RelayService.Instance.CreateAllocationAsync(15)
    │     ├─ RelayService.Instance.GetJoinCodeAsync(allocationId)
    │     └─ transport.SetRelayServerData(allocation, "dtls")
    │
    ├─ LobbyNetworkService.CreateLobby("EitD-Sala", relayCode)
    │     └─ LobbyService.Instance.CreateLobbyAsync(name, 16, options{RelayCode})
    │
    ├─ Exibe LobbyCode na tela (código de 6 letras)
    └─ NetworkManager.Singleton.StartHost()  ✅ Relay já configurado

CLIENT
  OnEntrarClicked()
    │
    ├─ LobbyNetworkService.JoinLobbyAndGetRelayCode(codigoDigitado)
    │     ├─ LobbyService.Instance.JoinLobbyByCodeAsync(codigo)
    │     └─ retorna lobby.Data["RelayJoinCode"].Value
    │
    ├─ RelayService.JoinRelay(relayCode)
    │     ├─ RelayService.Instance.JoinAllocationAsync(relayCode)
    │     └─ transport.SetRelayServerData(joinAllocation, "dtls")
    │
    └─ NetworkManager.Singleton.StartClient()  ✅ Relay já configurado
```

---

## Heartbeat do Lobby

O Unity Lobby remove salas inativas após 30 segundos sem heartbeat. O Host deve enviar pings periódicos:

```csharp
// Em LobbyController.cs — adicionar no Update
private float _heartbeatTimer;
private const float HEARTBEAT_INTERVAL = 15f;

private void Update()
{
    if (!NetworkManager.Singleton.IsHost) return;

    _heartbeatTimer += Time.deltaTime;
    if (_heartbeatTimer >= HEARTBEAT_INTERVAL)
    {
        _heartbeatTimer = 0f;
        _ = _lobbyService.SendHeartbeat(); // fire-and-forget
    }
}
```

---

## Checklist da Sessão B

- [ ] Criar `RelayService.cs` em `Scripts/Services/`
- [ ] Criar `LobbyNetworkService.cs` em `Scripts/Services/`
- [ ] Criar `LobbyController.cs` em `Scripts/UI/Lobby/`
- [ ] Montar UI na cena `Lobby.unity` (3 painéis: Escolha / Host / Client)
- [ ] Adicionar TextMeshPro ao projeto (Window → Package Manager → TextMeshPro)
- [ ] Linkar GameObjects ao `LobbyController` no Inspector
- [ ] Testar com MPPM: Main Editor cria sala, Virtual Player entra com código
- [ ] Confirmar conexão real via Relay (não IP direto)
- [ ] Implementar heartbeat no Update do LobbyControl# 06 · Sessão B — Lobby: Conexão via Relay

[← Voltar ao índice](../README.md)

> O que foi implementado na sessão B: fluxo completo de conexão Host + Client via Unity Relay e Unity Lobby.  
> Sessão concluída em 31/03/2026.  
> **Próxima sessão:** ver [08 · Lobby Completo](08-planejamento-lobby-completo.md)

---

## Scripts Implementados

| Script | Namespace | Localização |
|---|---|---|
| `RelayNetworkService.cs` | `EchoesInTheDark.Services` | `Scripts/Services/` |
| `LobbyNetworkService.cs` | `EchoesInTheDark.Services` | `Scripts/Services/` |
| `LobbyController.cs` | `EchoesInTheDark.UI` | `Scripts/UI/Lobby/` |

---

## RelayNetworkService.cs

Abstrai completamente o SDK do Relay. Nenhum outro script chama o SDK diretamente.

### Por que o nome não é `RelayService`?

A classe do SDK chama-se `Unity.Services.Relay.RelayService`. Se a nossa classe também se chamasse `RelayService`, haveria ambiguidade de nome e o compilador resolveria para a classe local — `RelayService.Instance` chamaria a si mesma em loop. Solução: `RelayNetworkService`.

### Construtor manual de `RelayServerData`

O Unity Transport 2.x removeu o construtor de conveniência `RelayServerData(Allocation, "dtls")`. É necessário passar os campos manualmente:

```csharp
// HOST — hostConnectionData == connectionData (host é ele mesmo)
transport.SetRelayServerData(new RelayServerData(
    allocation.RelayServer.IpV4,
    (ushort)allocation.RelayServer.Port,
    allocation.AllocationIdBytes,
    allocation.ConnectionData,
    allocation.ConnectionData,  // ← mesmo que connectionData para o Host
    allocation.Key,
    isSecure: true
));

// CLIENT — hostConnectionData vem do campo separado JoinAllocation
transport.SetRelayServerData(new RelayServerData(
    joinAllocation.RelayServer.IpV4,
    (ushort)joinAllocation.RelayServer.Port,
    joinAllocation.AllocationIdBytes,
    joinAllocation.ConnectionData,
    joinAllocation.HostConnectionData,  // ← dados do Host remoto
    joinAllocation.Key,
    isSecure: true
));
```

---

## LobbyNetworkService.cs

Abstrai o Unity Lobby Service. O `RelayJoinCode` é armazenado nos metadados da sala como `DataObject.VisibilityOptions.Member` — visível apenas para membros do Lobby.

```
HOST
  CreateLobby(name, relayJoinCode)
    └── IsPrivate = true → acesso só por código de 6 letras
    └── Data["RelayJoinCode"] = relayJoinCode

CLIENT
  JoinLobbyAndGetRelayCode(lobbyCode)
    └── JoinLobbyByCodeAsync(lobbyCode)
    └── retorna lobby.Data["RelayJoinCode"].Value
```

**Heartbeat:** O Unity Lobby remove salas sem atividade após 30s. O `LobbyController` chama `SendHeartbeat()` a cada 15s no `Update()`, mas apenas quando `NetworkManager.Singleton.IsHost`.

---

## LobbyController.cs

Coordena a UI com os dois serviços. Separação estrita: UI não chama SDK, Controller coordena Services.

### Fluxo Host

```
OnCriarSalaClicked()
  1. ShutdownIfRunningAsync()        ← sempre limpa estado NGO antes
  2. RelayNetworkService.CreateRelayAndGetJoinCode()
  3. LobbyNetworkService.CreateLobby("EitD-Sala", relayCode)
  4. _textCodigo.text = lobbyCode
  5. NetworkManager.Singleton.StartHost()
  6. ShowPanel(_panelHost)
```

### Fluxo Client

```
OnEntrarClicked()
  1. LobbyNetworkService.JoinLobbyAndGetRelayCode(codigo)
  2. RelayNetworkService.JoinRelay(relayCode)
  3. NetworkManager.Singleton.StartClient()
```

### ShutdownIfRunningAsync — por que sempre chamar?

```csharp
private async Task ShutdownIfRunningAsync()
{
    NetworkManager.Singleton.Shutdown(); // idempotente — seguro chamar sempre
    await Task.Yield();                  // 1 frame para NGO limpar estado interno
}
```

`Shutdown()` é idempotente — chamar quando o NGO não está rodando não causa erro. Chamar sempre garante que estado residual de conexões anteriores seja limpo antes de uma nova tentativa.

---

## Problemas Resolvidos nesta Sessão

| Problema | Causa | Solução |
|---|---|---|
| `You are not signed in to Authentication` | `UnityServices.InitializeAsync()` sem `SignInAnonymously` | Adicionado no Bootstrap |
| `Cannot start Host while running` | Bootstrap chamava `StartHost` + Lobby também | `ShutdownIfRunningAsync()` sempre antes |
| `RelayServerData` sem construtor de conveniência | Unity Transport 2.x removeu o overload | Construtor manual com todos os campos |
| Perfis MPPM compartilhados | Virtual Players usavam mesmo cache de autenticação | `SetProfile($"Player_{PID}")` no Bootstrap |

---

## ⚠️ Problema Conhecido — Join Code Not Found (Relay)

**Sintoma:**
```
[LobbyNetworkService] Entrou no Lobby. RelayCode: XXXXXX   ← Lobby OK
[LobbyController] Erro ao entrar na sala: Not Found: join code not found  ← Relay falha
```

O `JoinLobbyAndGetRelayCode` funciona — o client entra no Lobby e recupera o RelayCode corretamente. Porém, `RelayService.Instance.JoinAllocationAsync(relayCode)` retorna "Not Found".

**Hipóteses investigadas:**
- Perfis MPPM — **parcialmente resolvido** com `SetProfile(PID)`. Em algumas execuções o erro persiste.
- Estado interno do NGO — `ShutdownIfRunningAsync` resolve conflito de conexão anterior
- Alocação Relay expirada — o Relay expira alocações após um tempo sem StartHost

**Status:** Em investigação. A próxima sessão foca exclusivamente no Lobby — a lista de players conectados em tempo real facilitará o diagnóstico visual do estado da conexão.

**O que JÁ funciona:**
- ✅ Host cria sala com sucesso
- ✅ Código do Lobby é gerado e exibido
- ✅ Client entra no Lobby e recupera o RelayCode
- ✅ Em algumas execuções, a conexão Relay completa com sucesso (`[LobbyController] Client conectado via Relay`)

---

## Console esperado — fluxo Host

```
✅ [LobbyController] Limpando estado do NetworkManager...
✅ [RelayNetworkService] Alocação criada. JoinCode: XXXXXX
✅ [LobbyNetworkService] Lobby criado. ID: YYYY | Código: ZZZZZZ
✅ [LobbyController] Host iniciado via Relay. Código: ZZZZZZ
```

## Console esperado — fluxo Client (quando funciona)

```
✅ [LobbyNetworkService] Entrou no Lobby. RelayCode: XXXXXX
✅ [RelayNetworkService] Tentando JoinAllocationAsync com código: 'XXXXXX' (len: 6)
✅ [RelayNetworkService] Conectado ao Relay. JoinCode: XXXXXX
✅ [LobbyController] Client conectado via Relay.
```

---

*[← Voltar ao índice](../README.md)*
ler

---

## Notas de Arquitetura para o Futuro

### Por que o Lobby usa código e não IP?
O Relay garante que nenhum jogador expõe seu IP. O código do Lobby é uma referência opaca — ninguém precisa saber o IP de ninguém.

### NetworkManager.Shutdown() ao voltar
Sempre que o jogador sair do Lobby, a conexão deve ser encerrada via `Shutdown()`. Isso limpa o estado do NGO para uma nova conexão futura. Não fazer isso causa erros de "já conectado" nas tentativas seguintes.

### Separação Relay vs Lobby
- **Lobby** = lista de jogadores + metadados da sala (não é transporte de dados de jogo)
- **Relay** = transporte real dos pacotes do NGO
- O Lobby apenas guarda o `RelayJoinCode` como um dado público/privado da sala

### Próximo passo após Lobby
Com a conexão estabelecida, a sessão seguinte começa o **Player base**:
- `PlayerNetworkObject.prefab` com `NetworkObject`
- `PlayerMovement.cs` com input + movimento sincronizado
- Registro do prefab no `NetworkManager`
- Spawn automático ao conectar

---

*[← Voltar ao índice](../README.md)*