using System;
using UnityEngine;
using UnityEngine.UI;

namespace EchoesInTheDark.UI
{
    /// <summary>
    /// Controle de volume em 5 degraus (0.0 → 0.2 → 0.4 → 0.6 → 0.8 → 1.0).
    /// Cada Image acende (branco) ou apaga (cinza) conforme o valor atual.
    /// </summary>
    public class VolumeStepControl : MonoBehaviour
    {
        [Header("Botões")]
        [SerializeField] private Button _botaoMenos;
        [SerializeField] private Button _botaoMais;

        [Header("Degraus (ordem: menor → maior)")]
        [Tooltip("Arraste nessa ordem: Image(4), Image(3), Image(2), Image(1), Image")]
        [SerializeField] private Image[] _degraus; // tamanho 5

        [Header("Cores")]
        [SerializeField] private Color _corAtivo = new Color(1f, 1f, 1f);         // 255,255,255
        [SerializeField] private Color _corInativo = new Color(0.839f, 0.839f, 0.839f); // 214,214,214

        // Valor atual: 0, 1, 2, 3, 4 ou 5 degraus acesos
        private int _degrausAtivos = 5; // padrão: volume máximo

        // Callback disparado quando o valor muda (0.0 a 1.0)
        public event Action<float> OnValueChanged;

        private void OnEnable()
        {
            _botaoMenos.onClick.AddListener(Diminuir);
            _botaoMais.onClick.AddListener(Aumentar);
        }

        private void OnDisable()
        {
            _botaoMenos.onClick.RemoveListener(Diminuir);
            _botaoMais.onClick.RemoveListener(Aumentar);
        }

        private void Diminuir()
        {
            if (_degrausAtivos <= 0) return;
            _degrausAtivos--;
            AtualizarVisual();
            OnValueChanged?.Invoke(GetValorNormalizado());
        }

        private void Aumentar()
        {
            if (_degrausAtivos >= _degraus.Length) return;
            _degrausAtivos++;
            AtualizarVisual();
            OnValueChanged?.Invoke(GetValorNormalizado());
        }

        /// <summary>
        /// Define o valor visualmente sem disparar o evento (para sincronização inicial).
        /// </summary>
        public void SetValueWithoutNotify(float valor)
        {
            // Converte 0.0~1.0 para 0~5 degraus, arredondando para o step mais próximo
            _degrausAtivos = Mathf.RoundToInt(valor * _degraus.Length);
            _degrausAtivos = Mathf.Clamp(_degrausAtivos, 0, _degraus.Length);
            AtualizarVisual();
        }

        public float GetValorNormalizado()
            => (float)_degrausAtivos / _degraus.Length;

        private void AtualizarVisual()
        {
            for (int i = 0; i < _degraus.Length; i++)
            {
                // i=0 é Image(4) (menor), acende se i < degrausAtivos
                _degraus[i].color = i < _degrausAtivos ? _corAtivo : _corInativo;
            }

            // Desabilita botões nos extremos para feedback visual
            _botaoMenos.interactable = _degrausAtivos > 0;
            _botaoMais.interactable = _degrausAtivos < _degraus.Length;
        }
    }
}