using UnityEngine;
using UnityEngine.Audio;

namespace EchoesInTheDark.Core
{
    /// <summary>
    /// Persiste e aplica todas as configurações do jogo.
    /// Singleton DontDestroyOnLoad — existe durante toda a sessão.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        // ── Chaves PlayerPrefs ─────────────────────────────────────────
        private const string KEY_IDIOMA = "Settings_Idioma";
        private const string KEY_RESOLUCAO = "Settings_Resolucao";
        private const string KEY_QUALIDADE = "Settings_Qualidade";
        private const string KEY_DISPLAY = "Settings_Display";
        private const string KEY_SHOW_FPS = "Settings_ShowFPS";
        private const string KEY_VOL_GERAL = "Settings_VolGeral";
        private const string KEY_VOL_MUSICA = "Settings_VolMusica";
        private const string KEY_VOL_SFX = "Settings_VolSFX";
        private const string KEY_VOL_CHAT = "Settings_VolChat";
        private const string KEY_SENSIBILIDADE = "Settings_Sensibilidade";

        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer _audioMixer;

        // ── Resoluções disponíveis ─────────────────────────────────────
        public static readonly (int w, int h)[] Resolucoes =
        {
            (1024, 576),
            (1280, 720),
            (1366, 768),
            (1600, 900),
            (1920, 1080),
            (2560, 1080)
        };

        // ── Propriedades públicas (leitura) ────────────────────────────
        public int Idioma { get; private set; }
        public int Resolucao { get; private set; }
        public int Qualidade { get; private set; }
        public int Display { get; private set; }
        public bool ShowFPS { get; private set; }
        public float VolGeral { get; private set; }
        public float VolMusica { get; private set; }
        public float VolSFX { get; private set; }
        public float VolChat { get; private set; }
        public float Sensibilidade { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAll();
        }

        // ── Load ───────────────────────────────────────────────────────

        public void LoadAll()
        {
            Idioma = PlayerPrefs.GetInt(KEY_IDIOMA, 0);
            Resolucao = PlayerPrefs.GetInt(KEY_RESOLUCAO, 4); // 1920x1080
            Qualidade = PlayerPrefs.GetInt(KEY_QUALIDADE, 0); // Alta
            Display = PlayerPrefs.GetInt(KEY_DISPLAY, 0);   // Tela Cheia
            ShowFPS = PlayerPrefs.GetInt(KEY_SHOW_FPS, 0) == 1;
            VolGeral = PlayerPrefs.GetFloat(KEY_VOL_GERAL, 1f);
            VolMusica = PlayerPrefs.GetFloat(KEY_VOL_MUSICA, 1f);
            VolSFX = PlayerPrefs.GetFloat(KEY_VOL_SFX, 1f);
            VolChat = PlayerPrefs.GetFloat(KEY_VOL_CHAT, 1f);
            Sensibilidade = PlayerPrefs.GetFloat(KEY_SENSIBILIDADE, 0.5f);

            ApplyAll();
            Debug.Log("[SettingsManager] Configurações carregadas e aplicadas.");
        }

        // ── Apply ──────────────────────────────────────────────────────

        public void ApplyAll()
        {
            ApplyResolucao();
            ApplyQualidade();
            ApplyDisplay();
            ApplyVolume();
        }

        private void ApplyResolucao()
        {
            var (w, h) = Resolucoes[Resolucao];
            Screen.SetResolution(w, h, Display == 0);
            Debug.Log($"[SettingsManager] Resolução: {w}x{h}");
        }

        private void ApplyQualidade()
        {
            // Unity Quality Levels: 0=Low, 1=Medium, 2=High (dependendo do projeto)
            // Invertemos pois o usuário vê Alta=0, Média=1, Baixa=2
            int unityLevel = QualitySettings.names.Length - 1 - Qualidade;
            unityLevel = Mathf.Clamp(unityLevel, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(unityLevel, true);
            Debug.Log($"[SettingsManager] Qualidade: {QualitySettings.names[unityLevel]}");
        }

        private void ApplyDisplay()
        {
            Screen.fullScreen = Display == 0;
        }

        private void ApplyVolume()
        {
            if (_audioMixer == null) return;
            // AudioMixer usa dB: volume 0~1 → -80~0 dB
            _audioMixer.SetFloat("VolGeral", LinearToDecibel(VolGeral));
            _audioMixer.SetFloat("VolMusica", LinearToDecibel(VolMusica));
            _audioMixer.SetFloat("VolSFX", LinearToDecibel(VolSFX));
            _audioMixer.SetFloat("VolChat", LinearToDecibel(VolChat));
        }

        private static float LinearToDecibel(float linear)
            => linear > 0.001f ? Mathf.Log10(linear) * 20f : -80f;

        // ── Setters (chamados pela UI) ─────────────────────────────────

        public void SetIdioma(int value)
        {
            Idioma = value;
            PlayerPrefs.SetInt(KEY_IDIOMA, value);
            // TODO: LocalizationManager.Instance.SetLanguage(value)
            Debug.Log($"[SettingsManager] Idioma: {value}");
        }

        public void SetResolucao(int index)
        {
            Resolucao = index;
            PlayerPrefs.SetInt(KEY_RESOLUCAO, index);
            ApplyResolucao();
        }

        public void SetQualidade(int index)
        {
            Qualidade = index;
            PlayerPrefs.SetInt(KEY_QUALIDADE, index);
            ApplyQualidade();
        }

        public void SetDisplay(int index)
        {
            Display = index;
            PlayerPrefs.SetInt(KEY_DISPLAY, index);
            ApplyDisplay();
        }

        public void SetShowFPS(bool value)
        {
            ShowFPS = value;
            PlayerPrefs.SetInt(KEY_SHOW_FPS, value ? 1 : 0);
            // FPSDisplay vai escutar isso via SettingsManager.Instance.ShowFPS
            Debug.Log($"[SettingsManager] ShowFPS: {value}");
        }

        public void SetVolGeral(float value)
        {
            VolGeral = value;
            PlayerPrefs.SetFloat(KEY_VOL_GERAL, value);
            ApplyVolume();
        }

        public void SetVolMusica(float value)
        {
            VolMusica = value;
            PlayerPrefs.SetFloat(KEY_VOL_MUSICA, value);
            ApplyVolume();
        }

        public void SetVolSFX(float value)
        {
            VolSFX = value;
            PlayerPrefs.SetFloat(KEY_VOL_SFX, value);
            ApplyVolume();
        }

        public void SetVolChat(float value)
        {
            VolChat = value;
            PlayerPrefs.SetFloat(KEY_VOL_CHAT, value);
            ApplyVolume();
        }

        public void SetSensibilidade(float value)
        {
            Sensibilidade = value;
            PlayerPrefs.SetFloat(KEY_SENSIBILIDADE, value);
        }

        public void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(KEY_IDIOMA);
            PlayerPrefs.DeleteKey(KEY_RESOLUCAO);
            PlayerPrefs.DeleteKey(KEY_QUALIDADE);
            PlayerPrefs.DeleteKey(KEY_DISPLAY);
            PlayerPrefs.DeleteKey(KEY_SHOW_FPS);
            PlayerPrefs.DeleteKey(KEY_VOL_GERAL);
            PlayerPrefs.DeleteKey(KEY_VOL_MUSICA);
            PlayerPrefs.DeleteKey(KEY_VOL_SFX);
            PlayerPrefs.DeleteKey(KEY_VOL_CHAT);
            PlayerPrefs.DeleteKey(KEY_SENSIBILIDADE);
            LoadAll();
            Debug.Log("[SettingsManager] Reset para padrão.");
        }

        public void SaveAll() => PlayerPrefs.Save();
    }
}