using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace EchoesInTheDark.Services
{
    /// <summary>
    /// Abstrai o Unity Lobby Service.
    /// Nome evita conflito com Unity.Services.Lobbies.LobbyService (SDK).
    /// </summary>
    public class LobbyNetworkService
    {
        private const string KEY_RELAY_CODE = "RelayJoinCode";
        private Lobby _currentLobby;

        /// <summary>
        /// [HOST] Cria sala privada com o RelayJoinCode embutido nos dados.
        /// </summary>
        public async Task<Lobby> CreateLobby(string lobbyName, string relayJoinCode)
        {
            var options = new CreateLobbyOptions
            {
                IsPrivate = true, // acesso exclusivo por código
                Data = new Dictionary<string, DataObject>
                {
                    {
                        KEY_RELAY_CODE,
                        new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode)
                    }
                }
            };

            _currentLobby = await LobbyService.Instance.CreateLobbyAsync(
                lobbyName,
                maxPlayers: 16,
                options
            );

            Debug.Log($"[LobbyNetworkService] Lobby criado. ID: {_currentLobby.Id} | Código: {_currentLobby.LobbyCode}");
            return _currentLobby;
        }

        /// <summary>
        /// [CLIENT] Entra no Lobby pelo código e retorna o RelayJoinCode armazenado.
        /// </summary>
        public async Task<string> JoinLobbyAndGetRelayCode(string lobbyCode)
        {
            _currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            string relayCode = _currentLobby.Data[KEY_RELAY_CODE].Value;
            Debug.Log($"[LobbyNetworkService] Entrou no Lobby. RelayCode: {relayCode}");
            return relayCode;
        }

        /// <summary>
        /// [HOST] Envia heartbeat para manter o Lobby ativo (Unity remove após 30s sem ping).
        /// Chamar a cada 15s.
        /// </summary>
        public async Task SendHeartbeat()
        {
            if (_currentLobby == null) return;
            await LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
        }

        public string GetLobbyCode() => _currentLobby?.LobbyCode ?? string.Empty;
    }
}