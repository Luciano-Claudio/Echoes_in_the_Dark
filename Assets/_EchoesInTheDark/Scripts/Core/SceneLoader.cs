using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoesInTheDark.Core
{
    /// <summary>
    /// Singleton simples (não de rede) que centraliza toda navegação entre cenas.
    /// Nenhum outro script deve chamar SceneManager diretamente.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        private const string SCENE_MAIN_MENU = "MainMenu";
        private const string SCENE_LOBBY = "Lobby";
        private const string SCENE_MATCH = "Match";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void GoToMainMenu() => Load(SCENE_MAIN_MENU);
        public void GoToLobby() => Load(SCENE_LOBBY);
        public void GoToMatch() => Load(SCENE_MATCH);
        public void QuitGame() => Application.Quit();

        private void Load(string sceneName)
            => SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }
}