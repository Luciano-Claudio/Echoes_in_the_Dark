using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEditor.Searcher.SearcherWindow.Alignment;
using static UnityEngine.EventSystems.EventTrigger;

namespace EchoesInTheDark.Core
{
    /// <summary>
    /// Singleton que mantém o InputActionAsset carregado durante toda a sessão.
    /// Centraliza o acesso às actions pelo nome.
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public static InputManager Instance { get; private set; }

        [SerializeField] private InputActionAsset _actionAsset;

        public InputActionAsset ActionAsset => _actionAsset;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            CarregarOverrides();
            _actionAsset.Enable();
        }

        private void OnDestroy() => _actionAsset?.Disable();

        /// <summary>
        /// Retorna uma InputAction pelo nome (busca em todos os Action Maps).
        /// </summary>
        public InputAction FindAction(string actionName)
        {
            foreach (var map in _actionAsset.actionMaps)
            {
                var action = map.FindAction(actionName);
                if (action != null) return action;
            }
            Debug.LogWarning($"[InputManager] Action '{actionName}' não encontrada.");
            return null;
        }

        // ── Persistência global de overrides ──────────────────────────

        private const string OVERRIDES_KEY = "InputBindingOverrides";

        public void SalvarOverrides()
        {
            PlayerPrefs.SetString(OVERRIDES_KEY, _actionAsset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
            Debug.Log("[InputManager] Overrides salvos.");
        }

        public void CarregarOverrides()
        {
            if (PlayerPrefs.HasKey(OVERRIDES_KEY))
            {
                _actionAsset.LoadBindingOverridesFromJson(PlayerPrefs.GetString(OVERRIDES_KEY));
                Debug.Log("[InputManager] Overrides carregados.");
            }
        }

        public void ResetarTodosOverrides()
        {
            _actionAsset.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(OVERRIDES_KEY);
            Debug.Log("[InputManager] Todos os overrides removidos.");
        }
    }
}