using System;
using System.Collections;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;
using EchoesInTheDark.Core;
using EchoesInTheDark.Services;

namespace EchoesInTheDark.UI
{
    /// <summary>
    /// Coordena a UI do Lobby com os serviços de Relay e Lobby.
    /// Separação estrita: UI não chama SDK — Controller coordena Services.
    ///
    /// Fluxo Host:
    ///   PanelEscolha → [PanelLoading] → PanelSala (com BotaoIniciarPartida)
    ///
    /// Fluxo Client:
    ///   PanelEscolha → PanelEntrarCodigo → [PanelLoading] → PanelSala (com BotaoPronto)
    /// </summary>
    public class LobbyController : MonoBehaviour
    {
        // ── Painéis ───────────────────────────────────────────────────────

        [Header("Painéis Principais")]
        [SerializeField] private GameObject _panelEscolha;
        [SerializeField] private GameObject _panelLoading;       // spinner durante ops assíncronas
        [SerializeField] private GameObject _panelEntrarCodigo;  // client digita o código aqui
        [SerializeField] private GameObject _panelSala;          // painel compartilhado (host + client)

        // ── Painel Loading ────────────────────────────────────────────────

        [Header("Loading")]
        [SerializeField] private TextMeshProUGUI _textLoading;

        // ── Painel Escolha ────────────────────────────────────────────────

        [Header("Painel Escolha")]
        [SerializeField] private Button _buttonCriarSala;
        [SerializeField] private Button _buttonAbrirEntrarCodigo; // abre PanelEntrarCodigo

        // ── Painel Entrar Código ──────────────────────────────────────────

        [Header("Painel Entrar Código")]
        [SerializeField] private TMP_InputField _inputCodigo;
        [SerializeField] private Button         _buttonEntrar;
        [SerializeField] private Button         _buttonVoltarEntrar;

        // ── Painel Sala — Código ──────────────────────────────────────────

        [Header("Painel Sala — Código")]
        [SerializeField] private TextMeshProUGUI _textCodigo;   // "••••••" ou código real
        [SerializeField] private Button          _buttonOlho;   // toggle visibilidade
        [SerializeField] private Button          _buttonCopiar; // copia para clipboard

        // ── Painel Sala — Jogadores ───────────────────────────────────────

        [Header("Painel Sala — Jogadores")]
        [SerializeField] private TextMeshProUGUI          _textJogadoresCount;
        [SerializeField] private Transform                _playerListContainer;
        [SerializeField] private PlayerListItemController _playerListItemPrefab;

        // ── Painel Sala — Ações ───────────────────────────────────────────

        [Header("Painel Sala — Ações")]
        [SerializeField] private Button _buttonIniciarPartida; // host only
        [SerializeField] private Button _buttonPronto;         // client only
        [SerializeField] private Button _buttonSair;           // ambos

        // ── Serviços ──────────────────────────────────────────────────────

        private readonly RelayNetworkService _relayService = new RelayNetworkService();
        private readonly LobbyNetworkService _lobbyService = new LobbyNetworkService();

        // ── Estado ────────────────────────────────────────────────────────

        private const float HEARTBEAT_INTERVAL = 15f;
        private const float POLL_INTERVAL      = 2f;
        private const int   MAX_PLAYERS        = 15;

        private float  _heartbeatTimer;
        private bool   _codigoVisivel;
        private bool   _clienteEstaPronto;
        private bool   _isRefreshing;        // guard: evita polls simultâneos
        private string _lobbyCodeReal;       // código verdadeiro (toggle + clipboard)

        private Coroutine _pollCoroutine;

        // ── Lifecycle ─────────────────────────────────────────────────────

        private void OnEnable()
        {
            _buttonCriarSala.onClick.AddListener(OnCriarSalaClicked);
            _buttonAbrirEntrarCodigo.onClick.AddListener(OnAbrirEntrarCodigoClicked);

            _buttonEntrar.onClick.AddListener(OnEntrarClicked);
            _buttonVoltarEntrar.onClick.AddListener(OnVoltarParaEscolha);

            _buttonOlho.onClick.AddListener(OnOlhoClicked);
            _buttonCopiar.onClick.AddListener(OnCopiarClicked);

            _buttonIniciarPartida.onClick.AddListener(OnIniciarPartidaClicked);
            _buttonPronto.onClick.AddListener(OnProntoClicked);
            _buttonSair.onClick.AddListener(OnSairClicked);
        }

        private void OnDisable()
        {
            _buttonCriarSala.onClick.RemoveAllListeners();
            _buttonAbrirEntrarCodigo.onClick.RemoveAllListeners();
            _buttonEntrar.onClick.RemoveAllListeners();
            _buttonVoltarEntrar.onClick.RemoveAllListeners();
            _buttonOlho.onClick.RemoveAllListeners();
            _buttonCopiar.onClick.RemoveAllListeners();
            _buttonIniciarPartida.onClick.RemoveAllListeners();
            _buttonPronto.onClick.RemoveAllListeners();
            _buttonSair.onClick.RemoveAllListeners();

            StopPolling();
        }

        private void Update()
        {
            // Heartbeat: somente Host enquanto estiver no Lobby
            if (!NetworkManager.Singleton.IsHost) return;

            _heartbeatTimer += Time.deltaTime;
            if (_heartbeatTimer >= HEARTBEAT_INTERVAL)
            {
                _heartbeatTimer = 0f;
                _ = _lobbyService.SendHeartbeat(); // fire-and-forget intencional
            }
        }

        // ── Host ──────────────────────────────────────────────────────────

        private async void OnCriarSalaClicked()
        {
            ShowPanel(_panelLoading);
            SetLoadingText("Criando sala...");

            try
            {
                await ShutdownIfRunningAsync();

                string relayCode = await _relayService.CreateRelayAndGetJoinCode();

                // Inicia o Host ANTES de criar o Lobby para garantir que o Transport
                // estabeleça o handshake com o Relay antes de o código ser exposto.
                // FIX "join code not found": clientes não devem receber o código
                // enquanto o Host não estiver conectado ao Relay server.
                NetworkManager.Singleton.StartHost();
                await WaitForHostRelayAsync();

                await _lobbyService.CreateLobby("EitD-Sala", relayCode);

                _lobbyCodeReal = _lobbyService.GetLobbyCode();
                ConfigureSalaParaHost();
                AtualizarListaJogadores(_lobbyService.GetCurrentLobby());
                StartPolling();

                ShowPanel(_panelSala);
                Debug.Log($"[LobbyController] Host iniciado. Lobby code: {_lobbyCodeReal}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyController] Erro ao criar sala: {e.Message}");
                ShowPanel(_panelEscolha);
            }
        }

        // ── Client ────────────────────────────────────────────────────────

        private void OnAbrirEntrarCodigoClicked() => ShowPanel(_panelEntrarCodigo);

        private async void OnEntrarClicked()
        {
            string codigoDigitado = _inputCodigo.text.Trim().ToUpper();
            if (string.IsNullOrEmpty(codigoDigitado))
            {
                Debug.LogWarning("[LobbyController] Código vazio — ignorado.");
                return;
            }

            ShowPanel(_panelLoading);
            SetLoadingText("Entrando na sala...");

            try
            {
                await ShutdownIfRunningAsync();

                string relayCode = await _lobbyService.JoinLobbyAndGetRelayCode(codigoDigitado);
                await _relayService.JoinRelay(relayCode); // FIX: retry automático dentro do JoinRelay

                NetworkManager.Singleton.StartClient();

                _lobbyCodeReal = _lobbyService.GetLobbyCode();
                ConfigureSalaParaClient();
                AtualizarListaJogadores(_lobbyService.GetCurrentLobby());
                StartPolling();

                ShowPanel(_panelSala);
                Debug.Log($"[LobbyController] Client conectado. Lobby code: {_lobbyCodeReal}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyController] Erro ao entrar na sala: {e.Message}");
                ShowPanel(_panelEntrarCodigo);
            }
        }

        private void OnVoltarParaEscolha()
        {
            _inputCodigo.text = string.Empty;
            ShowPanel(_panelEscolha);
        }

        // ── Configuração de papel na Sala ─────────────────────────────────

        private void ConfigureSalaParaHost()
        {
            _codigoVisivel = false;
            AtualizarDisplayCodigo();

            _buttonIniciarPartida.gameObject.SetActive(true);
            _buttonIniciarPartida.interactable = false; // ativo só quando todos prontos
            _buttonPronto.gameObject.SetActive(false);
        }

        private void ConfigureSalaParaClient()
        {
            _codigoVisivel     = false;
            _clienteEstaPronto = false;
            AtualizarDisplayCodigo();
            AtualizarTextoBotaoPronto();

            _buttonIniciarPartida.gameObject.SetActive(false);
            _buttonPronto.gameObject.SetActive(true);
        }

        // ── Código Mascarado ──────────────────────────────────────────────

        private void OnOlhoClicked()
        {
            _codigoVisivel = !_codigoVisivel;
            AtualizarDisplayCodigo();
        }

        private void OnCopiarClicked()
        {
            if (string.IsNullOrEmpty(_lobbyCodeReal)) return;
            GUIUtility.systemCopyBuffer = _lobbyCodeReal;
            Debug.Log($"[LobbyController] '{_lobbyCodeReal}' copiado para o clipboard.");
            // TODO: exibir toast "Copiado!" por 2s
        }

        private void AtualizarDisplayCodigo()
        {
            if (string.IsNullOrEmpty(_lobbyCodeReal))
            {
                _textCodigo.text = "------";
                return;
            }
            _textCodigo.text = _codigoVisivel
                ? _lobbyCodeReal
                : new string('•', _lobbyCodeReal.Length); // "••••••"
        }

        // ── Sistema de Pronto ─────────────────────────────────────────────

        private async void OnProntoClicked()
        {
            _buttonPronto.interactable = false;
            _clienteEstaPronto         = !_clienteEstaPronto; // toggle otimista

            try
            {
                await _lobbyService.UpdatePlayerReadyAsync(_clienteEstaPronto);
                AtualizarTextoBotaoPronto();
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyController] Erro ao atualizar Pronto: {e.Message}");
                _clienteEstaPronto = !_clienteEstaPronto; // reverte em caso de falha
                AtualizarTextoBotaoPronto();
            }
            finally
            {
                _buttonPronto.interactable = true;
            }
        }

        private void AtualizarTextoBotaoPronto()
        {
            var label = _buttonPronto.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = _clienteEstaPronto ? "✓ Pronto" : "Pronto";
        }

        // ── Iniciar Partida ───────────────────────────────────────────────

        private void OnIniciarPartidaClicked()
        {
            if (!NetworkManager.Singleton.IsHost) return;
            StopPolling();
            // TODO (sessão futura): usar NetworkManager.SceneManager.LoadScene
            // para garantir que todos os clients também transicionem.
            SceneLoader.Instance.GoToMatch();
        }

        // ── Polling ───────────────────────────────────────────────────────

        private void StartPolling()
        {
            StopPolling();
            _pollCoroutine = StartCoroutine(PollLobbyCoroutine());
        }

        private void StopPolling()
        {
            if (_pollCoroutine == null) return;
            StopCoroutine(_pollCoroutine);
            _pollCoroutine = null;
        }

        private IEnumerator PollLobbyCoroutine()
        {
            var wait = new WaitForSeconds(POLL_INTERVAL);
            while (true)
            {
                yield return wait;
                if (!_isRefreshing)
                    _ = RefreshLobbyUI(); // fire-and-forget protegido por _isRefreshing
            }
        }

        private async Task RefreshLobbyUI()
        {
            _isRefreshing = true;
            try
            {
                Lobby lobby = await _lobbyService.RefreshLobbyAsync();
                if (lobby == null) return;

                AtualizarListaJogadores(lobby);

                if (NetworkManager.Singleton.IsHost)
                    AtualizarBotaoIniciar(lobby);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LobbyController] Falha no polling: {e.Message}");
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        // ── Lista de Jogadores ────────────────────────────────────────────

        private void AtualizarListaJogadores(Lobby lobby)
        {
            if (lobby == null) return;

            foreach (Transform child in _playerListContainer)
                Destroy(child.gameObject);

            _textJogadoresCount.text = $"{lobby.Players.Count}/{MAX_PLAYERS} jogadores";

            foreach (Player player in lobby.Players)
            {
                bool ehHost = player.Id == lobby.HostId;
                bool pronto = ehHost || IsPlayerReady(player);
                string nome = ObterNomeDisplay(player);

                var item = Instantiate(_playerListItemPrefab, _playerListContainer);
                item.Setup(nome, pronto, ehHost);
            }
        }

        private void AtualizarBotaoIniciar(Lobby lobby)
        {
            bool podeIniciar = _lobbyService.TodosClientesProntos() && lobby.Players.Count >= 2;
            _buttonIniciarPartida.interactable = podeIniciar;
        }

        private bool IsPlayerReady(Player player)
        {
            return player.Data != null &&
                   player.Data.TryGetValue("IsReady", out var data) &&
                   data.Value == "1";
        }

        private string ObterNomeDisplay(Player player)
        {
            string nome = player.Profile?.Name;
            if (!string.IsNullOrEmpty(nome)) return nome;
            int len = Mathf.Min(4, player.Id.Length);
            return $"Jogador_{player.Id.Substring(0, len)}";
        }

        // ── Sair ──────────────────────────────────────────────────────────

        private void OnSairClicked()
        {
            StopPolling();
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
                NetworkManager.Singleton.Shutdown();
            SceneLoader.Instance.GoToMainMenu();
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void ShowPanel(GameObject target)
        {
            _panelEscolha.SetActive(false);
            _panelLoading.SetActive(false);
            _panelEntrarCodigo.SetActive(false);
            _panelSala.SetActive(false);
            target.SetActive(true);
        }

        private void SetLoadingText(string texto) => _textLoading.text = texto;

        /// <summary>
        /// Aguarda o Host estabelecer conexão com o Relay Transport (handshake UDP).
        ///
        /// FIX "join code not found":
        ///   StartHost() é não-bloqueante — o Transport leva alguns frames para completar
        ///   o handshake com o Relay server. Se o Lobby for criado antes disso, clientes
        ///   que lerem o código imediatamente receberão "join code not found" porque o
        ///   Relay ainda não reconhece a alocação como ativa.
        ///
        ///   Usa OnServerStarted (dispara quando o NGO confirma o servidor) + delay extra
        ///   para o handshake UDP do Transport (que ocorre após o NGO reportar início).
        /// </summary>
        private async Task WaitForHostRelayAsync()
        {
            var tcs = new System.Threading.Tasks.TaskCompletionSource<bool>();
            void OnStarted() { tcs.TrySetResult(true); }
            NetworkManager.Singleton.OnServerStarted += OnStarted;

            // Timeout de segurança: se OnServerStarted não disparar em 5s, continua mesmo assim
            await Task.WhenAny(tcs.Task, Task.Delay(5000));
            NetworkManager.Singleton.OnServerStarted -= OnStarted;

            // Aguarda handshake UDP do Relay Transport (ocorre após NGO reportar início)
            await Task.Delay(1500);
            Debug.Log("[LobbyController] Host conectado ao Relay. Criando Lobby...");
        }

        /// <summary>
        /// Garante shutdown completo antes de StartHost/StartClient.
        ///
        /// FIX: aguarda ShutdownInProgress em vez de apenas 1 Task.Yield().
        /// 1 frame era insuficiente — o Transport precisa de vários frames para
        /// limpar buffers internos do Relay, causando estado residual entre sessões.
        /// </summary>
        private async Task ShutdownIfRunningAsync()
        {
            if (!NetworkManager.Singleton.IsListening) return;

            Debug.Log("[LobbyController] Desligando NetworkManager anterior...");
            NetworkManager.Singleton.Shutdown();

            // Aguarda o shutdown completar (pode levar 10–30+ frames com Relay)
            int maxFrames = 60; // timeout de segurança (~1s em 60fps)
            while (NetworkManager.Singleton.ShutdownInProgress && maxFrames-- > 0)
                await Task.Yield();

            await Task.Yield(); // frame extra para buffers do Transport
            await Task.Yield();

            Debug.Log("[LobbyController] NetworkManager desligado.");
        }
    }
}
