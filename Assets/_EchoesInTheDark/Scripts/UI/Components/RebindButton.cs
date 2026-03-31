using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using EchoesInTheDark.Core;

namespace EchoesInTheDark.UI
{
    /// <summary>
    /// Botão individual de rebind. Exibe a tecla atual e inicia
    /// o processo de captura ao ser clicado.
    /// </summary>
    public class RebindButton : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _textTecla;

        [Header("Configuração")]
        [Tooltip("Nome da Action no InputActionAsset (ex: Interagir)")]
        [SerializeField] private string _actionName;
        [Tooltip("Índice do binding dentro da action (0 para actions simples)")]
        [SerializeField] private int _bindingIndex = 0;

        [Header("Painel de Espera de Rebind")]
        [SerializeField] private GameObject _painelRebindEspera;

        // Evento disparado quando rebind termina — SettingsController escuta isso
        public event Action OnRebindComplete;
        public event Action OnRebindStarted;
        public event Action<string> OnRebindConflito; // nome da action conflitante

        private InputActionRebindingExtensions.RebindingOperation _rebindOperation;
        private InputAction _action;

        // Chave para salvar o override no PlayerPrefs
        private string SaveKey => $"Rebind_{_actionName}_{_bindingIndex}";

        private void Start()
        {
            _action = InputManager.Instance.FindAction(_actionName);
            CarregarOverride();
            AtualizarTexto();
        }

        private void OnEnable() => _button.onClick.AddListener(IniciarRebind);
        private void OnDisable()
        {
            _button.onClick.RemoveListener(IniciarRebind);
            _rebindOperation?.Dispose();
        }

        // ── Rebind ────────────────────────────────────────────────────

        private void IniciarRebind()
        {
            if (_action == null) return;

            if (_action.bindings[_bindingIndex].isComposite)
            {
                Debug.LogWarning($"[RebindButton] Binding {_bindingIndex} de '{_actionName}' é um Composite — não é rebindável diretamente.");
                return;
            }

            OnRebindStarted?.Invoke();
            _action.Disable();
            _textTecla.text = "...";
            _button.interactable = false;

            _rebindOperation = _action
                .PerformInteractiveRebinding(_bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Keyboard>/escape")      // ESC nunca pode ser atribuído
                .WithCancelingThrough("<Keyboard>/escape")        // ESC = cancelar e fechar painel
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op => ConcluirRebind(cancelado: false))
                .OnCancel(op => ConcluirRebind(cancelado: true))
                .Start();
        }

        private void ConcluirRebind(bool cancelado)
        {
            _rebindOperation.Dispose();
            _rebindOperation = null;

            _action.Enable();
            _button.interactable = true;

            if (!cancelado)
            {
                // Verifica se a tecla escolhida já está em uso por outro binding
                string novoPath = _action.bindings[_bindingIndex].overridePath
                               ?? _action.bindings[_bindingIndex].path;

                string conflito = EncontrarConflito(novoPath);

                if (conflito != null)
                {
                    // Cancela o rebind — reverte para o binding anterior
                    _action.RemoveBindingOverride(_bindingIndex);
                    Debug.LogWarning($"[RebindButton] Tecla já em uso por '{conflito}'. Rebind cancelado.");
                    OnRebindConflito?.Invoke(conflito);
                }
                else
                {
                    SalvarOverride();
                }
            }

            AtualizarTexto();
            OnRebindComplete?.Invoke();
        }

        /// <summary>
        /// Verifica se o path já está usado por qualquer outro binding no asset.
        /// Retorna o nome da action conflitante ou null se não há conflito.
        /// </summary>
        private string EncontrarConflito(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var asset = InputManager.Instance.ActionAsset;

            foreach (var map in asset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        // Pula o próprio binding que acabou de ser configurado
                        if (action.name == _actionName && i == _bindingIndex) continue;

                        // Pula Composites — eles não têm path real
                        if (action.bindings[i].isComposite) continue;

                        string pathAtivo = action.bindings[i].overridePath
                                        ?? action.bindings[i].path;

                        if (pathAtivo == path)
                            return action.name;
                    }
                }
            }

            return null;
        }

        // ── Persistência ──────────────────────────────────────────────

        private void SalvarOverride()
        {
            string json = _action.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(SaveKey, json);
            PlayerPrefs.Save();
        }

        private void CarregarOverride()
        {
            if (PlayerPrefs.HasKey(SaveKey))
            {
                string json = PlayerPrefs.GetString(SaveKey);
                _action.LoadBindingOverridesFromJson(json);
            }
        }

        public void ResetarParaPadrao()
        {
            _action?.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(SaveKey);
            AtualizarTexto();
        }

        // ── Visual ────────────────────────────────────────────────────

        private void AtualizarTexto()
        {
            if (_action == null) { _textTecla.text = "?"; return; }

            string displayString = InputControlPath.ToHumanReadableString(
                _action.bindings[_bindingIndex].effectivePath,
                InputControlPath.HumanReadableStringOptions.OmitDevice
            );

            _textTecla.text = string.IsNullOrEmpty(displayString) ? "?" : displayString.ToUpper();
        }
    }
}