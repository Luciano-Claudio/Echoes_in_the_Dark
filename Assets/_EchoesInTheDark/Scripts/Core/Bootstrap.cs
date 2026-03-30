using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoesInTheDark.Core
{
    /// <summary>
    /// Entry point do jogo. Persiste entre todas as cenas via DontDestroyOnLoad.
    /// Compatível com Multiplayer Play Mode 2.x (Unity 6.3).
    /// </summary>
    public class Bootstrap : MonoBehaviour
    {
        // ── Constantes ───────────────────────────────────────────────────────
        private const string SCENE_MAIN_MENU = "MainMenu";

        private const string TAG_VAMPIRE = "vampire";
        private const string TAG_INNOCENT = "innocent";
        private const string TAG_GUARD = "guard";

        // ── Ciclo de vida ────────────────────────────────────────────────────

        private async void Awake()
        {
            DontDestroyOnLoad(gameObject);

            try
            {
                await InitializeServicesAsync();
                AutoConnectInEditor();
                LoadMainMenu();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Bootstrap] Falha na inicialização: {e.Message}\n{e.StackTrace}");
            }
        }

        // ── Unity Services ───────────────────────────────────────────────────

        private async Task InitializeServicesAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                Debug.Log("[Bootstrap] Unity Services já inicializados.");
                return;
            }

            Debug.Log("[Bootstrap] Inicializando Unity Services...");
            await UnityServices.InitializeAsync();
            Debug.Log("[Bootstrap] Unity Services prontos.");
        }

        // ── Auto-connect (MPPM 2.x) ──────────────────────────────────────────

        private void AutoConnectInEditor()
        {
#if UNITY_EDITOR
            // No editor (MPPM local), usa IP direto — sem Relay.
            // Relay só entra em produção, via fluxo de Lobby/Relay allocation.
            Unity.Netcode.Transports.UTP.UnityTransport transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();

            transport.SetConnectionData("127.0.0.1", 7777);

            if (IsVirtualPlayer())
            {
                Debug.Log("[Bootstrap] Virtual Player → StartClient (IP direto)");
                NetworkManager.Singleton.StartClient();
            }
            else
            {
                Debug.Log("[Bootstrap] Main Editor → StartHost (IP direto)");
                NetworkManager.Singleton.StartHost();
            }
#endif
        }

        /// <summary>
        /// Detecta Virtual Players via argumentos de linha de comando.
        /// O MPPM injeta -mppmTag &lt;valor&gt; em cada instância virtual.
        /// Funciona no MPPM 1.x e 2.x sem depender de namespace externo.
        /// </summary>
        private static bool IsVirtualPlayer()
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                // MPPM injeta -mppmTag <valor> nos Virtual Players
                if (args[i] == "-mppmTag" && i + 1 < args.Length)
                {
                    string tag = args[i + 1].ToLower();
                    bool isKnownRole = tag == TAG_VAMPIRE
                                   || tag == TAG_INNOCENT
                                   || tag == TAG_GUARD;

                    Debug.Log($"[Bootstrap] Tag MPPM detectada: '{tag}' | Role conhecida: {isKnownRole}");
                    return isKnownRole;
                }
            }

            // Sem -mppmTag → é o Main Editor
            return false;
        }

        // ── Navegação ────────────────────────────────────────────────────────

        private void LoadMainMenu()
        {
            Debug.Log("[Bootstrap] Carregando MainMenu...");
            SceneManager.LoadScene(SCENE_MAIN_MENU, LoadSceneMode.Single);
        }
    }
}