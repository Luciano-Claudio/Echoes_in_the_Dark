using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace EchoesInTheDark.Services
{
    /// <summary>
    /// Abstrai o Unity Lobby Service.
    /// Gerencia criação, entrada, heartbeat, estado "Pronto" e polling da sala.
    /// Nome evita conflito com Unity.Services.Lobbies.LobbyService (SDK).
    /// </summary>
    public class LobbyNetworkService
    {
        private const string KEY_RELAY_CODE = "RelayJoinCode";
        private const string KEY_IS_READY   = "IsReady";
        private const int    MAX_PLAYERS    = 15; // GDD: 2 a 15 jogadores humanos (fix: era 16)

        private Lobby _currentLobby;

        // ── Host ──────────────────────────────────────────────────────────

        /// <summary>[HOST] Cria sala privada com o RelayJoinCode embutido nos metadados.</summary>
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
                MAX_PLAYERS,
                options
            );

            Debug.Log($"[LobbyNetworkService] Lobby criado. ID: {_currentLobby.Id} | Código: {_currentLobby.LobbyCode}");
            return _currentLobby;
        }

        /// <summary>[HOST] Envia heartbeat a cada 15s para manter o Lobby ativo (SDK remove após 30s sem ping).</summary>
        public async Task SendHeartbeat()
        {
            if (_currentLobby == null) return;
            await LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
        }

        // ── Client ────────────────────────────────────────────────────────

        /// <summary>
        /// [CLIENT] Entra no Lobby pelo código e retorna o RelayJoinCode armazenado.
        /// FIX 1: normaliza o lobbyCode antes de enviar ao SDK.
        /// FIX 2: se já for membro (Relay falhou numa tentativa anterior), reconecta via GetLobbyAsync
        ///        em vez de falhar com "player is already a member of the lobby".
        /// </summary>
        public async Task<string> JoinLobbyAndGetRelayCode(string lobbyCode)
        {
            // Lobby codes são case-insensitive no SDK, mas normalizamos por segurança
            lobbyCode = lobbyCode.Trim().ToUpper();

            try
            {
                _currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.Conflict)
            {
                // Ocorre quando o Relay falhou numa tentativa anterior:
                // o Lobby foi aceito mas o Transport não conectou — ao tentar de novo,
                // o SDK rejeita porque o player já é membro.
                // Solução: recarregar os dados do Lobby existente para recuperar o RelayCode.
                Debug.LogWarning("[LobbyNetworkService] Já é membro do Lobby — recarregando dados para retry do Relay.");
                if (_currentLobby == null)
                    throw new System.Exception("Já membro do Lobby mas sem ID em cache para reconectar.", e);
                _currentLobby = await LobbyService.Instance.GetLobbyAsync(_currentLobby.Id);
            }

            string relayCode = _currentLobby.Data[KEY_RELAY_CODE].Value;
            Debug.Log($"[LobbyNetworkService] Entrou no Lobby. RelayCode: '{relayCode}'");
            return relayCode;
        }

        // ── Ready System ──────────────────────────────────────────────────

        /// <summary>
        /// Atualiza o estado "Pronto" do jogador local no Lobby.
        /// Armazenado em PlayerDataObject — visível para todos os membros.
        /// </summary>
        public async Task UpdatePlayerReadyAsync(bool isReady)
        {
            if (_currentLobby == null) return;

            string playerId = AuthenticationService.Instance.PlayerId;

            var options = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    {
                        KEY_IS_READY,
                        new PlayerDataObject(
                            PlayerDataObject.VisibilityOptions.Member,
                            isReady ? "1" : "0"
                        )
                    }
                }
            };

            _currentLobby = await LobbyService.Instance.UpdatePlayerAsync(
                _currentLobby.Id,
                playerId,
                options
            );

            Debug.Log($"[LobbyNetworkService] IsReady={isReady} atualizado. Player: {playerId}");
        }

        /// <summary>
        /// Verifica se todos os CLIENTES (não-host) estão prontos.
        /// Host é excluído da checagem — ele pressiona "Iniciar" em vez de "Pronto".
        /// Retorna false se não houver ao menos 1 cliente.
        /// </summary>
        public bool TodosClientesProntos()
        {
            if (_currentLobby == null) return false;

            int clientesTotal   = 0;
            int clientesProntos = 0;

            foreach (var player in _currentLobby.Players)
            {
                if (player.Id == _currentLobby.HostId) continue; // pula o host

                clientesTotal++;

                if (player.Data != null &&
                    player.Data.TryGetValue(KEY_IS_READY, out var readyData) &&
                    readyData.Value == "1")
                {
                    clientesProntos++;
                }
            }

            // Precisa de ao menos 1 cliente E todos prontos
            return clientesTotal > 0 && clientesTotal == clientesProntos;
        }

        // ── Polling ───────────────────────────────────────────────────────

        /// <summary>Recarrega dados atualizados do Lobby do servidor (lista de players, estados).</summary>
        public async Task<Lobby> RefreshLobbyAsync()
        {
            if (_currentLobby == null) return null;
            _currentLobby = await LobbyService.Instance.GetLobbyAsync(_currentLobby.Id);
            return _currentLobby;
        }

        // ── Getters ───────────────────────────────────────────────────────

        public Lobby  GetCurrentLobby() => _currentLobby;
        public string GetLobbyCode()    => _currentLobby?.LobbyCode ?? string.Empty;
        public string GetLobbyId()      => _currentLobby?.Id        ?? string.Empty;
        public string GetHostId()       => _currentLobby?.HostId    ?? string.Empty;
    }
}
