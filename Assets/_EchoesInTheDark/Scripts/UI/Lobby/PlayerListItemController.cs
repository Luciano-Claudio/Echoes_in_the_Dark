using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EchoesInTheDark.UI
{
    /// <summary>
    /// Representa um jogador na lista da sala do Lobby.
    ///
    /// Prefab esperado:
    ///   PlayerListItem (GameObject)
    ///   ├── TextNome      (TextMeshProUGUI)
    ///   └── ImagemStatus  (Image) — verde = pronto, cinza = aguardando
    /// </summary>
    public class PlayerListItemController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _textNome;
        [SerializeField] private Image           _imagemStatus;

        [Header("Cores de Status")]
        [SerializeField] private Color _corPronto    = new Color(0.20f, 0.78f, 0.20f, 1f); // verde
        [SerializeField] private Color _corAguardando = new Color(0.50f, 0.50f, 0.50f, 1f); // cinza

        /// <summary>
        /// Configura o item com os dados do jogador.
        /// </summary>
        /// <param name="nomeDisplay">Nome exibido na lista.</param>
        /// <param name="pronto">Se o jogador clicou em "Pronto".</param>
        /// <param name="ehHost">Se é o host — exibe "(Host)" e sempre aparece como pronto.</param>
        public void Setup(string nomeDisplay, bool pronto, bool ehHost)
        {
            if (ehHost)
            {
                // Rich text: nome normal + tag "(Host)" menor e acinzentada
                _textNome.text = $"{nomeDisplay}  <size=70%><color=#AAAAAA>(Host)</color></size>";
                // Host não precisa clicar Pronto — sempre aparece como pronto visualmente
                _imagemStatus.color = _corPronto;
            }
            else
            {
                _textNome.text      = nomeDisplay;
                _imagemStatus.color = pronto ? _corPronto : _corAguardando;
            }
        }
    }
}
