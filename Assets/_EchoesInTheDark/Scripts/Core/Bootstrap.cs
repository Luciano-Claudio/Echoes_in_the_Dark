using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace EchoesInTheDark.Core
{
    /// <summary>
    /// Entry point do jogo. Fluxo:
    /// PanelLogo → PanelIntro (vídeo) → PanelLoading → MainMenu
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        private const string SCENE_MAIN_MENU = "MainMenu";

        [Header("Painéis de Intro")]
        [SerializeField] private GameObject _panelLogo;
        [SerializeField] private GameObject _panelIntro;
        [SerializeField] private GameObject _panelLoading;

        [Header("Logo")]
        [Tooltip("Duração em segundos da tela de logo do estúdio")]
        [SerializeField] private float _logoDuration = 3f;

        [Header("Intro — Vídeo")]
        [Tooltip("Componente VideoPlayer que reproduz a animação de intro")]
        [SerializeField] private VideoPlayer _videoPlayer;

        [Header("Loading UI")]
        [SerializeField] private Slider _progressBar;
        [SerializeField] private TextMeshProUGUI _textStatus;
        [SerializeField] private TextMeshProUGUI _textErro;

        private bool _skipRequested;
        private bool _servicesReady;
        private bool _servicesFailed;
        private string _failMessage = string.Empty;

        private async void Awake()
        {
            DontDestroyOnLoad(gameObject);

            // Serviços inicializam em paralelo enquanto as telas de intro rodam
            _ = InitializeServicesAsync();

            await ShowLogoAsync();
            await ShowIntroAsync();
            await ShowLoadingAsync();

            if (_servicesFailed)
            {
                _textErro.text = $"Erro crítico: {_failMessage}\nVerifique sua conexão e reinicie o jogo.";
                return;
            }

            LoadMainMenu();
        }

        // ── LOGO ──────────────────────────────────────────────────────────

        private async Task ShowLogoAsync()
        {
            _panelLogo.SetActive(true);

            float elapsed = 0f;
            while (elapsed < _logoDuration)
            {
                // Qualquer tecla do teclado ou botão de gamepad pula
                if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                    break;
                if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
                    break;

                elapsed += Time.deltaTime;
                await Task.Yield();
            }

            _panelLogo.SetActive(false);

            // Aguarda um frame de folga para garantir que o input da logo
            // não vaze para a próxima fase
            await Task.Yield();
            await Task.Yield();
        }

        // ── INTRO (VÍDEO) ─────────────────────────────────────────────────

        private async Task ShowIntroAsync()
        {
            if (_videoPlayer == null)
            {
                Debug.LogWarning("[Bootstrap] VideoPlayer não atribuído — pulando intro.");
                return;
            }

            _panelIntro.SetActive(true);

            // Prepara o vídeo
            _videoPlayer.Prepare();
            while (!_videoPlayer.isPrepared)
                await Task.Yield();

            // TaskCompletionSource que resolve quando o vídeo termina naturalmente
            var videoFinished = new System.Threading.Tasks.TaskCompletionSource<bool>();
            _videoPlayer.loopPointReached += _ => videoFinished.TrySetResult(true);

            _videoPlayer.Play();

            // Folga de 3 frames para estabilizar antes de aceitar input
            await Task.Yield();
            await Task.Yield();
            await Task.Yield();

            Debug.Log($"[Bootstrap] Vídeo iniciado. Aguardando término ou skip...");

            // Loop: aguarda término do vídeo OU input de skip
            bool skipped = false;
            while (!videoFinished.Task.IsCompleted)
            {
                if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                {
                    skipped = true;
                    break;
                }
                if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
                {
                    skipped = true;
                    break;
                }
                await Task.Yield();
            }

            Debug.Log($"[Bootstrap] Intro encerrada. Skip: {skipped}");

            _videoPlayer.loopPointReached -= _ => videoFinished.TrySetResult(true);
            _videoPlayer.Stop();
            _panelIntro.SetActive(false);

            // Folga para não vazar input para a próxima fase
            await Task.Yield();
            await Task.Yield();
        }

        // ── LOADING ───────────────────────────────────────────────────────

        private async Task ShowLoadingAsync()
        {
            _panelLoading.SetActive(true);
            _progressBar.value = 0f;
            _textErro.text = string.Empty;
            _textStatus.text = "Verificando serviços...";

            const float TIMEOUT = 15f;
            float elapsed = 0f;

            while (!_servicesReady && !_servicesFailed && elapsed < TIMEOUT)
            {
                elapsed += Time.deltaTime;
                _progressBar.value = Mathf.Clamp01(elapsed / TIMEOUT) * 0.9f;
                await Task.Yield();
            }

            if (_servicesFailed) return;

            if (!_servicesReady)
            {
                _servicesFailed = true;
                _failMessage = "Tempo limite de conexão esgotado.";
                return;
            }

            _textStatus.text = "Pronto!";
            _progressBar.value = 1f;
            await Task.Delay(500);
        }

        // ── SERVICES ──────────────────────────────────────────────────────

        private async Task InitializeServicesAsync()
        {
            try
            {
                var options = new InitializationOptions();

#if UNITY_EDITOR
                string profile = $"Player_{System.Diagnostics.Process.GetCurrentProcess().Id}";
                options.SetProfile(profile);
                Debug.Log($"[Bootstrap] Perfil: {profile}");
#endif

                await UnityServices.InitializeAsync(options);
                Debug.Log("[Bootstrap] Unity Services prontos.");

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log($"[Bootstrap] Autenticado. PlayerID: {AuthenticationService.Instance.PlayerId}");
                }

                _servicesReady = true;
            }
            catch (Exception e)
            {
                _servicesFailed = true;
                _failMessage = e.Message;
                Debug.LogError($"[Bootstrap] Falha: {e.Message}");
            }
        }

        // ── NAVEGAÇÃO ─────────────────────────────────────────────────────

        private void LoadMainMenu()
        {
            Debug.Log("[Bootstrap] Carregando MainMenu...");
            SceneManager.LoadScene(SCENE_MAIN_MENU, LoadSceneMode.Single);
        }
    }
}