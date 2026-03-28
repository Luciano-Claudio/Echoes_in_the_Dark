# 02 · Stack Técnica

[← Voltar ao índice](../README.md)

> Todas as tecnologias, versões, pacotes instalados e as decisões arquiteturais por trás de cada escolha.

---

## Versões Instaladas

| Tecnologia | Versão | Fonte |
|---|---|---|
| Unity | **6.3 LTS** | Unity Hub |
| Netcode for GameObjects | **2.11.0** | Package Manager |
| Unity Transport | **2.6.0** | Package Manager |
| Multiplayer Tools | **2.2.8** | Package Manager |
| Multiplayer Services SDK | mais recente | Package Manager |
| Multiplayer Play Mode | mais recente | Package Manager |
| A* Pathfinder Pro | — | Asset Store (importado em `Plugins/`) |

---

## Pacotes do Unity

### Netcode for GameObjects (NGO)
**O que faz:** Camada de alto nível de networking. Gerencia sincronização de `NetworkObject`, `NetworkVariable`, RPCs e spawn/despawn pela rede.

**Por que NGO e não outras soluções:**
- Integração nativa com Unity 6.3
- Suporte oficial da Unity
- `NetworkVariable` com dirty tracking eficiente
- Suficiente para jogo de ritmo lento (social deduction não precisa de rollback/reconciliation)

**O que usamos dele:**
- `NetworkManager` — gerencia conexões e ciclo de vida
- `NetworkObject` — componente em todo prefab sincronizado
- `NetworkVariable<T>` — estados sincronizados com dirty tracking
- `[ServerRpc]` — cliente envia pedido para o servidor
- `[ClientRpc]` — servidor envia dado para todos os clientes
- `NetworkBehaviour` — base de todos os scripts de rede

---

### Unity Transport
**O que faz:** Camada de transporte UDP de baixo nível que o NGO usa internamente.

**Nossa interação:** Indireta. O NGO abstrai o Transport. Configuramos apenas o tipo de transporte no `NetworkManager` (UnityTransport para conexões reais).

---

### Multiplayer Services SDK
**O que faz:** Abstrai três serviços da Unity Cloud em uma única API:
- **Lobby** — criar/entrar em salas, ver lista de jogadores
- **Relay** — conexão peer-to-peer sem expor IP, sem servidor dedicado
- **Session** — estado persistente da sessão de jogo

**Por que Relay:**  
No modelo Client Hosted, o Host precisa aceitar conexões. Sem Relay, o Host precisaria abrir porta no roteador (NAT traversal). O Relay da Unity age como intermediário — clients conectam ao Relay, que encaminha para o Host. Solução limpa e sem configuração do usuário final.

---

### Multiplayer Play Mode
**O que faz:** Permite rodar múltiplas instâncias do jogo no Editor sem fazer build. Essencial para testar com 2–4 jogadores na mesma máquina.

**Como configuramos:**
- Main Editor = sempre o Host
- Virtual Players (Player 2, 3, 4) = Clients com auto-connect por tag

---

### Multiplayer Tools
**O que faz:** Ferramentas de debug e otimização de rede.

| Ferramenta | Quando usar |
|---|---|
| Network Scene Visualization | Ver quais objetos são NetworkObjects na cena |
| Runtime Net Stats Monitor | Monitorar bandwidth, mensagens/s em tempo real |
| Network Simulator | Simular latência e packet loss para testar robustez |
| Network Profiler | Identificar gargalos de bandwidth por mensagem |
| Hierarchy Network Debug View | Ver ownership de cada NetworkObject na Hierarchy |

---

### A* Pathfinder Pro
**O que faz:** Pathfinding e navegação para NPCs usando o algoritmo A*.

**Por que não NavMesh do Unity:**  
NavMesh do Unity é 3D-first. A* Pathfinder Pro tem suporte nativo a 2D top-down com grid graphs e point graphs, que se encaixam melhor no layout da vila.

**Onde fica:** `Assets/Plugins/AstarPathfinder/` — nunca modificar.

**Regra de rede:** Todo pathfinding roda exclusivamente no Host. Clients recebem apenas a posição final do NPC via `NetworkVariable<Vector2>`. O pathfinding nunca é executado em clients.

---

## Modelo de Hosting

### Client Hosted (protótipo e jogo final)

```
Jogador A (Host)
  ├── Roda o NGO como Host (server + client simultaneamente)
  ├── Tem autoridade sobre todos os GameObjects
  ├── Roda toda a IA dos NPCs
  └── Conectado ao Relay da Unity

Relay (Unity Cloud)
  └── Intermediário sem estado — apenas encaminha pacotes

Jogadores B, C, D... (Clients)
  ├── Conectam ao Relay com o Join Code gerado pelo Host
  ├── Recebem estado via NetworkVariable
  └── Enviam ações via ServerRpc
```

**Vantagens para este projeto:**
- Sem custo de servidor dedicado
- Relay gratuito para volume baixo
- Suficiente para sessões de 5–15 jogadores com ritmo lento

**Risco conhecido:**  
Se o Host sair durante a partida, a sessão termina. Mitigação futura: host migration.

---

## Fluxo de Conexão

```
1. Host cria Relay allocation
2. Relay retorna Join Code (ex: "ABCD12")
3. Host cria Lobby com o Join Code nos dados
4. Host inicia NetworkManager.StartHost()

5. Client busca Lobby pelo código
6. Client lê o Join Code dos dados do Lobby
7. Client configura UnityTransport com dados do Relay
8. Client inicia NetworkManager.StartClient()
9. Conexão estabelecida via Relay
```

---

## Decisões Arquiteturais

### Por que Host Autoritativo e não Client-Side Prediction?

Echoes in the Dark é um jogo de ritmo **lento** (social deduction). Latência de 100–300ms não impacta a experiência como impactaria em um FPS. Client-side prediction adiciona complexidade enorme (reconciliation, rollback) sem benefício perceptível para este tipo de jogo.

**Regra:** Toda validação de gameplay acontece no Host. Clients apenas solicitam ações via `ServerRpc`.

---

### NetworkVariable vs ServerRpc/ClientRpc

| Usar `NetworkVariable` quando... | Usar RPC quando... |
|---|---|
| Estado persistente que clients novos precisam saber | Evento pontual (morte, votação, animação) |
| Valor muda com frequência baixa/média | Ação que acontece uma vez |
| Dado que precisa de dirty tracking automático | Precisa enviar parâmetros complexos |

**Exemplos no projeto:**

```csharp
// NetworkVariable — estado persistente
NetworkVariable<bool> torchIsLit;           // tocha acesa/apagada
NetworkVariable<PlayerRole> assignedRole;   // papel do jogador
NetworkVariable<int> tasksCompleted;        // missões completadas
NetworkVariable<Vector2> npcPosition;       // posição do NPC

// ServerRpc — cliente solicita ação
[ServerRpc] void RequestInteractTaskServerRpc(int taskId);
[ServerRpc] void RequestKillTargetServerRpc(ulong targetId);
[ServerRpc] void RequestBlowTorchServerRpc(ulong torchId);

// ClientRpc — servidor notifica todos
[ClientRpc] void OnPlayerKilledClientRpc(ulong victimId);
[ClientRpc] void OnMeetingStartedClientRpc(ulong reporterId);
[ClientRpc] void OnVoteResultClientRpc(ulong bannedPlayerId);
```

---

### Cenas e DontDestroyOnLoad

```
Bootstrap (nunca descarregada)
 └── NetworkManager (DontDestroyOnLoad)
 └── ServiceBootstrapper (DontDestroyOnLoad)
 └── GameEvents (DontDestroyOnLoad)

MainMenu / Lobby / Match (carregadas/descarregadas normalmente)
```

O `NetworkManager` nunca é destruído entre cenas. Isso garante que a conexão de rede persiste durante a transição de Lobby → Match.

---

## Plataforma Alvo

- **PC — Windows 10/11 (64-bit)**
- Controles: teclado + mouse
- Gamepad: não prioritário no protótipo

---

*[← Voltar ao índice](../README.md)*
