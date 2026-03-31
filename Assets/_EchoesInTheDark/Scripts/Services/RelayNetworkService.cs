using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace EchoesInTheDark.Services
{
    /// <summary>
    /// Abstrai o Unity Relay Service.
    /// Host: aloca servidor e obtém JoinCode.
    /// Client: entra na alocação usando o JoinCode.
    /// Usa o construtor manual de RelayServerData (Unity Transport 2.x — sem construtor de conveniência).
    /// </summary>
    public class RelayNetworkService
    {
        private const int MAX_CONNECTIONS = 15;

        /// <summary>
        /// [HOST] Cria alocação Relay e retorna o JoinCode de 6 caracteres.
        /// Já configura o Transport antes de retornar.
        /// </summary>
        public async Task<string> CreateRelayAndGetJoinCode()
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MAX_CONNECTIONS);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            // Unity Transport 2.x: construtor manual
            // Para o Host, hostConnectionData == connectionData (é o próprio host)
            transport.SetRelayServerData(new RelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.ConnectionData,
                allocation.ConnectionData,  // hostConnectionData = connectionData para o Host
                allocation.Key,
                isSecure: true              // DTLS
            ));

            Debug.Log($"[RelayNetworkService] Alocação criada. JoinCode: {joinCode}");
            return joinCode;
        }

        /// <summary>
        /// [CLIENT] Entra na alocação Relay pelo JoinCode.
        /// Já configura o Transport antes de retornar.
        /// </summary>
        public async Task JoinRelay(string joinCode)
        {
            Debug.Log($"[RelayNetworkService] Tentando JoinAllocationAsync com código: '{joinCode}' (len: {joinCode?.Length})");

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            // Para o Client, hostConnectionData vem separado (dados do Host)
            transport.SetRelayServerData(new RelayServerData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData,  // dados do Host — diferente do Client
                joinAllocation.Key,
                isSecure: true
            ));

            Debug.Log($"[RelayNetworkService] Conectado ao Relay. JoinCode: {joinCode}");
        }
    }
}