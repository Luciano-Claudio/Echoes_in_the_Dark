using UnityEngine;
using UnityEngine.UI;
using EchoesInTheDark.Core;

namespace EchoesInTheDark.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button _buttonJogar;
        [SerializeField] private Button _buttonConfiguracoes;
        [SerializeField] private Button _buttonShopping;
        [SerializeField] private Button _buttonSair;

        [SerializeField] private SettingsController _settingsController;

        private void OnEnable()
        {
            _buttonJogar.onClick.AddListener(OnJogarClicked);
            _buttonConfiguracoes.onClick.AddListener(OnConfiguracoesClicked);
            _buttonShopping.onClick.AddListener(OnShoppingClicked);
            _buttonSair.onClick.AddListener(OnSairClicked);
        }

        private void OnDisable()
        {
            _buttonJogar.onClick.RemoveListener(OnJogarClicked);
            _buttonConfiguracoes.onClick.RemoveListener(OnConfiguracoesClicked);
            _buttonShopping.onClick.RemoveListener(OnShoppingClicked);
            _buttonSair.onClick.RemoveListener(OnSairClicked);
        }

        private void OnJogarClicked() => SceneLoader.Instance.GoToLobby();
        private void OnConfiguracoesClicked() => _settingsController.Abrir();
        private void OnShoppingClicked() => Debug.Log("[MainMenu] Shopping — em breve.");
        private void OnSairClicked() => SceneLoader.Instance.QuitGame();
    }
}