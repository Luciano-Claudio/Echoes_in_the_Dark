using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EchoesInTheDark.Services;
using EchoesInTheDark.Core;
using Unity.Netcode;
using System.Threading.Tasks;

namespace EchoesInTheDark.UI
{
    /// <summary>
    /// Coordena a UI do Lobby com os serviços de Relay e Lobby.
    /// Separação estrita: UI não chama SDK — Controller coordena Services.
    /// </summary>
    public class LobbyController : MonoBehaviour
    {
        [Header("Painéis")]
        [SerializeField] private GameObject _panelEscolha;
        [SerializeField] private GameObject _panelHost;
        [SerializeField] private GameObject _panelClient;

        [Header("Painel Host")]
        [SerializeField] private TextMeshProUGUI _textCodigo;
        [SerializeField] private Button _buttonCriarSala;
        [SerializeField] private Button _buttonIniciarPartida;
        [SerializeField] private Button _buttonVoltarHost;

        [Header("Painel Client")]
        [SerializeField] private TMP_InputField _inputCodigo;
        [SerializeField] private Button _buttonEntrarCodigo;  // abre o PanelClient
        [SerializeField] private Button _buttonEntrar;        // confirma entrada
        [SerializeField] private Button _buttonVoltarClient;

        // Serviços instanciados localmente (sem DI por enquanto)
        private readonly RelayNetworkService _relayService = new RelayNetworkService();
        private readonly LobbyNetworkService _lobbyService = new LobbyNetworkService();

        private float _heartbeatTimer;
        private const float HEARTBEAT_INTERVAL = 15f;

        // ── Ciclo de vida ──────────────────────────────────────────────────

        private void OnEnable()
        {
            _buttonCriarSala.onClick.AddListener(OnCriarSalaClicked);
            _buttonEntrarCodigo.onClick.AddListener(OnEntrarCodigoClicked);
            _buttonEntrar.onClick.AddListener(OnEntrarClicked);
            _buttonIniciarPartida.onClick.AddListener(OnIniciarPartidaClicked);
            _buttonVoltarHost.onClick.AddListener(OnVoltarClicked);
            _buttonVoltarClient.onClick.AddListener(OnVoltarClicked);
        }

        private void OnDisable()
        {
            _buttonCriarSala.onClick.RemoveAllListeners();
            _buttonEntrarCodigo.onClick.RemoveAllListeners();
            _buttonEntrar.onClick.RemoveAllListeners();
            _buttonIniciarPartida.onClick.RemoveAllListeners();
            _buttonVoltarHost.onClick.RemoveAllListeners();
            _buttonVoltarClient.onClick.RemoveAllListeners();
        }

        private void Update()
        {
            // Heartbeat: só o Host envia, só enquanto estiver no Lobby
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
            SetPrimaryButtonsInteractable(false);

            try
            {
                // Se Bootstrap já iniciou como Host via IP direto (editor),
                // desconectar antes de reconectar via Relay
                await ShutdownIfRunningAsync();

                // 1. Aloca Relay — Transport já configurado após este await
                string relayCode = await _relayService.CreateRelayAndGetJoinCode();

                // 2. Cria Lobby com RelayCode nos metadados
                await _lobbyService.CreateLobby("EitD-Sala", relayCode);

                // 3. Exibe o código do Lobby (6 letras que o client digita)
                _textCodigo.text = _lobbyService.GetLobbyCode();

                // 4. Inicia o Host — Relay já configurado pelo RelayNetworkService
                NetworkManager.Singleton.StartHost();

                ShowPanel(_panelHost);
                Debug.Log($"[LobbyController] Host iniciado via Relay. Código: {_lobbyService.GetLobbyCode()}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyController] Erro ao criar sala: {e.Message}");
                SetPrimaryButtonsInteractable(true);
            }
        }

        /// <summary>
        /// Sempre chama Shutdown antes de StartHost/StartClient.
        /// Shutdown() é idempotente — seguro chamar mesmo se NGO não está rodando.
        /// Necessário para limpar estado residual de falhas de transport (ex: porta ocupada).
        /// </summary>
        private async Task ShutdownIfRunningAsync()
        {
            Debug.Log("[LobbyController] Limpando estado do NetworkManager...");
            NetworkManager.Singleton.Shutdown();
            await Task.Yield(); // aguarda um frame para o NGO limpar internamente
        }

        // ── Client ────────────────────────────────────────────────────────

        private void OnEntrarCodigoClicked() => ShowPanel(_panelClient);

        private async void OnEntrarClicked()
        {
            string codigo = _inputCodigo.text.Trim().ToUpper();
            if (string.IsNullOrEmpty(codigo)) return;

            SetPrimaryButtonsInteractable(false);

            try
            {
                // 1. Entra no Lobby e recupera o RelayCode dos metadados
                string relayCode = await _lobbyService.JoinLobbyAndGetRelayCode(codigo);

                // 2. Configura o Transport com os dados do Relay
                await _relayService.JoinRelay(relayCode);

                // 3. Conecta como Client — Relay já configurado
                NetworkManager.Singleton.StartClient();

                Debug.Log("[LobbyController] Client conectado via Relay.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyController] Erro ao entrar na sala: {e.Message}");
                SetPrimaryButtonsInteractable(true);
            }
        }

        // ── Match ─────────────────────────────────────────────────────────

        private void OnIniciarPartidaClicked()
        {
            if (!NetworkManager.Singleton.IsHost) return;
            // TODO (sessão futura): validar número mínimo de jogadores
            SceneLoader.Instance.GoToMatch();
        }

        // ── Navegação ─────────────────────────────────────────────────────

        private void OnVoltarClicked()
        {
            // Sempre desconectar antes de voltar — evita "já conectado" em nova tentativa
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
                NetworkManager.Singleton.Shutdown();

            SceneLoader.Instance.GoToMainMenu();
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void ShowPanel(GameObject target)
        {
            _panelEscolha.SetActive(false);
            _panelHost.SetActive(false);
            _panelClient.SetActive(false);
            target.SetActive(true);
        }

        private void SetPrimaryButtonsInteractable(bool value)
        {
            _buttonCriarSala.interactable = value;
            _buttonEntrar.interactable = value;
        }
    }
}