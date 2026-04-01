using System;
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
    /// Client: entra na alocação usando o JoinCode (com retry automático).
    /// Usa o construtor manual de RelayServerData (Unity Transport 2.x).
    /// </summary>
    public class RelayNetworkService
    {
        private const int MAX_CONNECTIONS      = 15;
        private const int JOIN_MAX_RETRIES     = 3;
        private const int JOIN_RETRY_BASE_MS   = 1500; // 1.5s → 3s (backoff exponencial)

        // ── Host ──────────────────────────────────────────────────────────

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
                allocation.ConnectionData, // hostConnectionData = connectionData para o Host
                allocation.Key,
                isSecure: true             // DTLS obrigatório
            ));

            Debug.Log($"[RelayNetworkService] Alocação criada. JoinCode: {joinCode}");
            return joinCode;
        }

        // ── Client ────────────────────────────────────────────────────────

        /// <summary>
        /// [CLIENT] Entra na alocação Relay pelo JoinCode.
        /// Já configura o Transport antes de retornar.
        ///
        /// FIX "Join Code Not Found":
        ///   1. Normaliza o código (.Trim().ToUpper()) — Relay é case-sensitive.
        ///   2. Retry com backoff exponencial (até JOIN_MAX_RETRIES tentativas).
        ///      Falhas transientes de rede ou propagação da alocação são recuperáveis.
        /// </summary>
        public async Task JoinRelay(string joinCode)
        {
            if (string.IsNullOrWhiteSpace(joinCode))
                throw new ArgumentException("JoinCode não pode ser vazio.", nameof(joinCode));

            // Normaliza: remove espaços e garante maiúsculas
            joinCode = joinCode.Trim().ToUpper();
            Debug.Log($"[RelayNetworkService] JoinCode normalizado: '{joinCode}' (len={joinCode.Length})");

            Exception lastException = null;

            for (int attempt = 1; attempt <= JOIN_MAX_RETRIES; attempt++)
            {
                try
                {
                    Debug.Log($"[RelayNetworkService] JoinAllocationAsync tentativa {attempt}/{JOIN_MAX_RETRIES}...");

                    JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                    var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

                    // Para o Client, hostConnectionData vem separado (dados do Host remoto)
                    transport.SetRelayServerData(new RelayServerData(
                        joinAllocation.RelayServer.IpV4,
                        (ushort)joinAllocation.RelayServer.Port,
                        joinAllocation.AllocationIdBytes,
                        joinAllocation.ConnectionData,
                        joinAllocation.HostConnectionData, // diferente do Host: são os dados do Host remoto
                        joinAllocation.Key,
                        isSecure: true
                    ));

                    Debug.Log($"[RelayNetworkService] Relay configurado com sucesso na tentativa {attempt}.");
                    return;
                }
                catch (Exception e)
                {
                    lastException = e;
                    Debug.LogWarning($"[RelayNetworkService] Tentativa {attempt}/{JOIN_MAX_RETRIES} falhou: {e.Message}");

                    if (attempt < JOIN_MAX_RETRIES)
                    {
                        int delayMs = JOIN_RETRY_BASE_MS * attempt; // 1.5s, 3s
                        Debug.Log($"[RelayNetworkService] Aguardando {delayMs}ms antes da próxima tentativa...");
                        await Task.Delay(delayMs);
                    }
                }
            }

            throw new Exception(
                $"[RelayNetworkService] Falha ao entrar no Relay após {JOIN_MAX_RETRIES} tentativas. " +
                $"Último erro: {lastException?.Message}", lastException);
        }
    }
}
