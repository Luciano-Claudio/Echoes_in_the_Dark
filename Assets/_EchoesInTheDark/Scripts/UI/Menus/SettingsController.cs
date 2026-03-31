using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EchoesInTheDark.Core;

namespace EchoesInTheDark.UI
{
    /// <summary>
    /// Controla o painel de configurações — abas, UI e delegação ao SettingsManager.
    /// </summary>
    public class SettingsController : MonoBehaviour
    {
        [Header("Painel raiz")]
        [SerializeField] private GameObject _settingsPanel;

        [Header("Botões de Aba")]
        [SerializeField] private Button _botaoGeral;
        [SerializeField] private Button _botaoGraficos;
        [SerializeField] private Button _botaoSom;
        [SerializeField] private Button _botaoControles;
        [SerializeField] private Button _botaoFechar;

        [Header("Painéis de Conteúdo")]
        [SerializeField] private GameObject _painelGeral;
        [SerializeField] private GameObject _painelGraficos;
        [SerializeField] private GameObject _painelSom;
        [SerializeField] private GameObject _painelControles;

        [Header("Geral")]
        [SerializeField] private TMP_Dropdown _dropdownIdioma;
        [SerializeField] private Button _botaoPolitica;
        [SerializeField] private Button _botaoTermos;

        [Header("Gráficos")]
        [SerializeField] private TMP_Dropdown _dropdownResolucao;
        [SerializeField] private TMP_Dropdown _dropdownQualidade;
        [SerializeField] private TMP_Dropdown _dropdownDisplay;
        [SerializeField] private Toggle _toggleFPS;

        [Header("Som")]
        [SerializeField] private VolumeStepControl _volumeGeral;
        [SerializeField] private VolumeStepControl _volumeMusica;
        [SerializeField] private VolumeStepControl _volumeSFX;
        [SerializeField] private VolumeStepControl _volumeChat;

        [Header("Controles")]
        [SerializeField] private Button _botaoPadrao;

        [Header("Rebind")]
        [SerializeField] private GameObject _painelRebindEspera;
        [SerializeField] private RebindButton[] _botoesRebind;
        [SerializeField] private TextMeshProUGUI _textConflito; // texto no PainelRebindEspera

        private void OnEnable()
        {
            // Abas
            _botaoGeral.onClick.AddListener(() => MostrarAba(_painelGeral));
            _botaoGraficos.onClick.AddListener(() => MostrarAba(_painelGraficos));
            _botaoSom.onClick.AddListener(() => MostrarAba(_painelSom));
            _botaoControles.onClick.AddListener(() => MostrarAba(_painelControles));
            _botaoFechar.onClick.AddListener(Fechar);

            // Geral
            _dropdownIdioma.onValueChanged.AddListener(SettingsManager.Instance.SetIdioma);
            _botaoPolitica.onClick.AddListener(() => Application.OpenURL("https://seusite.com/privacidade"));
            _botaoTermos.onClick.AddListener(() => Application.OpenURL("https://seusite.com/termos"));

            // Gráficos
            _dropdownResolucao.onValueChanged.AddListener(SettingsManager.Instance.SetResolucao);
            _dropdownQualidade.onValueChanged.AddListener(SettingsManager.Instance.SetQualidade);
            _dropdownDisplay.onValueChanged.AddListener(SettingsManager.Instance.SetDisplay);
            _toggleFPS.onValueChanged.AddListener(SettingsManager.Instance.SetShowFPS);

            // Som
            _volumeGeral.OnValueChanged += SettingsManager.Instance.SetVolGeral;
            _volumeMusica.OnValueChanged += SettingsManager.Instance.SetVolMusica;
            _volumeSFX.OnValueChanged += SettingsManager.Instance.SetVolSFX;
            _volumeChat.OnValueChanged += SettingsManager.Instance.SetVolChat;

            // Controles
            foreach (var botao in _botoesRebind)
            {
                botao.OnRebindStarted += AbrirPainelRebind;
                botao.OnRebindComplete += FecharPainelRebind;
                botao.OnRebindConflito += OnConflito;
            }
            _botaoPadrao.onClick.AddListener(OnPadraoClicked);
        }

        private void OnDisable()
        {
            _botaoGeral.onClick.RemoveAllListeners();
            _botaoGraficos.onClick.RemoveAllListeners();
            _botaoSom.onClick.RemoveAllListeners();
            _botaoControles.onClick.RemoveAllListeners();
            _botaoFechar.onClick.RemoveAllListeners();
            _dropdownIdioma.onValueChanged.RemoveAllListeners();
            _botaoPolitica.onClick.RemoveAllListeners();
            _botaoTermos.onClick.RemoveAllListeners();
            _dropdownResolucao.onValueChanged.RemoveAllListeners();
            _dropdownQualidade.onValueChanged.RemoveAllListeners();
            _dropdownDisplay.onValueChanged.RemoveAllListeners();
            _toggleFPS.onValueChanged.RemoveAllListeners();
            _volumeGeral.OnValueChanged -= SettingsManager.Instance.SetVolGeral;
            _volumeMusica.OnValueChanged -= SettingsManager.Instance.SetVolMusica;
            _volumeSFX.OnValueChanged -= SettingsManager.Instance.SetVolSFX;
            _volumeChat.OnValueChanged -= SettingsManager.Instance.SetVolChat;
            foreach (var botao in _botoesRebind)
            {
                botao.OnRebindStarted -= AbrirPainelRebind;
                botao.OnRebindComplete -= FecharPainelRebind;
                botao.OnRebindConflito -= OnConflito;
            }
            _botaoPadrao.onClick.RemoveAllListeners();
        }

        // ── Público (chamado pelo MainMenuController) ──────────────────

        public void Abrir()
        {
            SincronizarUI();
            _settingsPanel.SetActive(true);
            MostrarAba(_painelGeral);
        }

        public void Fechar()
        {
            SettingsManager.Instance.SaveAll();
            _settingsPanel.SetActive(false);
        }

        // ── Abas ──────────────────────────────────────────────────────

        private void MostrarAba(GameObject alvo)
        {
            _painelGeral.SetActive(false);
            _painelGraficos.SetActive(false);
            _painelSom.SetActive(false);
            _painelControles.SetActive(false);
            alvo.SetActive(true);
        }

        // ── Sincronização UI → Settings ────────────────────────────────

        private void SincronizarUI()
        {
            var s = SettingsManager.Instance;

            // Desconectar listeners temporariamente para não disparar eventos ao setar valores
            _dropdownIdioma.SetValueWithoutNotify(s.Idioma);
            _dropdownResolucao.SetValueWithoutNotify(s.Resolucao);
            _dropdownQualidade.SetValueWithoutNotify(s.Qualidade);
            _dropdownDisplay.SetValueWithoutNotify(s.Display);
            _toggleFPS.SetIsOnWithoutNotify(s.ShowFPS);
            _volumeGeral.SetValueWithoutNotify(s.VolGeral);
            _volumeMusica.SetValueWithoutNotify(s.VolMusica);
            _volumeSFX.SetValueWithoutNotify(s.VolSFX);
            _volumeChat.SetValueWithoutNotify(s.VolChat);
        }
        private void AbrirPainelRebind()
        {
            _textConflito.text = string.Empty;
            _painelRebindEspera.SetActive(true);
        }

        private void FecharPainelRebind() => _painelRebindEspera.SetActive(false);

        private void OnConflito(string nomeAction)
            => _textConflito.text = $"Tecla já usada por '{nomeAction}'.\nEscolha outra tecla.";

        private void OnPadraoClicked()
        {
            SettingsManager.Instance.ResetToDefaults();

            // Resetar todos os bindings de controle
            InputManager.Instance.ResetarTodosOverrides();
            foreach (var botao in _botoesRebind)
                botao.ResetarParaPadrao();

            SincronizarUI();
        }
    }
}