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
| Multiplayer Play Mode | **2.0.2** | Package Manager |
| A* Pathfinder Pro | — | Asset Store (`Plugins/AstarPathfinder/`) |

> **Pacotes removidos intencionalmente:** Vivox foi removido do projeto — não utilizamos chat de voz e o pacote gerava erros de inicialização por falta de configuração no Unity Dashboard.

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

**Nossa interação:** Indireta. O NGO abstrai o Transport. Configuramos o tipo de transporte no `NetworkManager`:
- Prefab: `Relay Unity Transport` (produção)
- Editor (MPPM): sobrescrito em runtime para IP direto via `SetConnectionData()`

---

### Multiplayer Services SDK
**O que faz:** Abstrai três serviços da Unity Cloud em uma única API:
- **Lobby** — criar/entrar em salas, ver lista de jogadores
- **Relay** — conexão peer-to-peer sem expor IP, sem servidor dedicado
- **Session** — estado persistente da sessão de jogo

**Por que Relay:**  
No modelo Client Hosted, o Host precisa aceitar conexões. Sem Relay, o Host precisaria abrir porta no roteador (NAT traversal). O Relay age como intermediário — clients conectam ao Relay, que encaminha para o Host.

---

### Multiplayer Play Mode 2.0.2

**O que faz:** Permite rodar múltiplas instâncias do jogo no Editor sem build. Essencial para testar com 2–4 jogadores na mesma máquina.

**Mudança importante na versão 2.0:** A maior parte do código foi migrada para dentro do engine Unity 6.3 (`Play Mode Framework`). Por isso, o namespace `Unity.Multiplayer.Playmode` **não está acessível** via `using` convencional em scripts de usuário. A classe `CurrentPlayer` existe, mas não pode ser referenciada sem Assembly Definition específico.

**Solução adotada no projeto:** Auto-connect via argumentos de linha de comando (ver `05-bootstrap-e-cenas.md`).

**Como configuramos:**

| Instância | Tag MPPM | Comportamento no Bootstrap |
|---|---|---|
| Main Editor | (nenhuma) | `StartHost()` com IP direto |
| Player 2 | `vampire` | `StartClient()` com IP direto |
| Player 3 | `innocent` | `StartClient()` com IP direto |
| Player 4 | `guard` | `StartClient()` com IP direto |

---

### Multiplayer Tools 2.2.8

**O que faz:** Ferramentas de debug e otimização de rede.

| Ferramenta | Quando usar |
|---|---|
| Network Scene Visualization | Ver quais objetos são NetworkObjects na cena |
| Runtime Net Stats Monitor | Monitorar bandwidth, mensagens/s em tempo real |
| Network Simulator | Simular latência e packet loss para testar robustez |
| Network Profiler | Identificar gargalos de bandwidth por mensagem |
| Hierarchy Network Debug View | Ver ownership de cada NetworkObject na Hierarchy |

> **Nota:** O `[Debug Updater]` que aparece na Hierarchy em runtime é gerado automaticamente por este pacote. É normal e esperado.

---

### A* Pathfinder Pro
**O que faz:** Pathfinding e navegação para NPCs usando o algoritmo A*.

**Por que não NavMesh do Unity:**  
NavMesh é 3D-first. A* Pathfinder Pro tem suporte nativo a 2D top-down com grid graphs e point graphs.

**Onde fica:** `Assets/Plugins/AstarPathfinder/` — nunca modificar.

**Regra de rede:** Todo pathfinding roda exclusivamente no Host. Clients recebem apenas a posição do NPC via `NetworkVariable<Vector2>`.

---

## Modelo de Hosting

### Client Hosted

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

**Risco conhecido:**  
Se o Host sair durante a partida, a sessão termina. Mitigação futura: host migration.

---

## Fluxo de Conexão (Produção)

```
HOST
1. Relay allocation → obtém JoinCode (ex: "ABCD12")
2. Cria Lobby com JoinCode nos dados da sala
3. transport.SetRelayServerData(allocation, "dtls")
4. NetworkManager.StartHost()

CLIENT
5. Entra no Lobby pelo código de 6 letras
6. Lê JoinCode do Relay dos dados do Lobby
7. transport.SetRelayServerData(joinAllocation, "dtls")
8. NetworkManager.StartClient()
```

## Fluxo de Conexão (Editor / MPPM)

```
TODOS (sem fluxo de Lobby)
1. Bootstrap detecta tag via -mppmTag nos args
2. transport.SetConnectionData("127.0.0.1", 7777)
3. Main Editor → StartHost() | Virtual Players → StartClient()
```

---

## Decisões Arquiteturais

### Por que Host Autoritativo e não Client-Side Prediction?

Echoes in the Dark é um jogo de ritmo **lento** (social deduction). Latência de 100–300ms não impacta a experiência. Client-side prediction adiciona complexidade enorme sem benefício perceptível.

**Regra:** Toda validação de gameplay acontece no Host. Clients apenas solicitam ações via `ServerRpc`.

---

### NetworkVariable vs RPC

| Usar `NetworkVariable` quando... | Usar RPC quando... |
|---|---|
| Estado persistente que clients novos precisam saber ao conectar | Evento pontual (morte, votação, animação) |
| Valor muda com frequência baixa/média | Ação que acontece uma vez |
| Dado com dirty tracking automático | Precisa enviar parâmetros complexos |

---

### Cenas e DontDestroyOnLoad

```
Bootstrap (nunca descarregada)
 └── Bootstrap.cs (DontDestroyOnLoad)
 └── NetworkManager + UnityTransport (DontDestroyOnLoad)

MainMenu / Lobby / Match (carregadas/descarregadas normalmente)
```

O `NetworkManager` nunca é destruído entre cenas — garante que a conexão persiste durante `Lobby → Match`.

---

## Plataforma Alvo

- **PC — Windows 10/11 (64-bit)**
- Controles: teclado + mouse
- Gamepad: não prioritário no protótipo

---

*[← Voltar ao índice](../README.md)*