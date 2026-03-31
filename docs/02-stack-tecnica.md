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
| New Input System | mais recente | Package Manager |
| A* Pathfinder Pro | — | Asset Store (`Plugins/AstarPathfinder/`) |

> **Pacotes removidos intencionalmente:** Vivox foi removido — não utilizamos chat de voz e o pacote gerava erros de inicialização sem configuração no Unity Dashboard.

---

## Netcode for GameObjects (NGO)

**O que faz:** Camada de alto nível de networking. Gerencia sincronização de `NetworkObject`, `NetworkVariable`, RPCs e spawn/despawn pela rede.

**Por que NGO:**
- Integração nativa com Unity 6.3
- `NetworkVariable` com dirty tracking eficiente
- Suficiente para jogo de ritmo lento (social deduction não precisa de rollback)

**O que usamos:**
- `NetworkManager` — gerencia conexões e ciclo de vida
- `NetworkObject` — componente em todo prefab sincronizado
- `NetworkVariable<T>` — estados sincronizados com dirty tracking
- `[ServerRpc]` — client envia pedido para o servidor
- `[ClientRpc]` — servidor envia dado para clientes
- `NetworkBehaviour` — base de todos os scripts de rede

---

## Unity Transport 2.6.0

**Interação:** Indireta — o NGO abstrai o Transport. Configurado no `NetworkManager`:
- Prefab: `Relay Unity Transport` (modo produção)
- Editor: dados de Relay configurados pelo `LobbyController` antes de `StartHost/Client`

**Mudança importante no 2.x:** O construtor de conveniência `RelayServerData(Allocation, "dtls")` foi **removido**. É necessário passar todos os campos manualmente (ver `RelayNetworkService.cs`).

---

## Unity Multiplayer Services SDK

Três serviços integrados:

**Lobby:** Criar/entrar em salas, ver lista de jogadores, dados por player, heartbeat.  
**Relay:** Conexão peer-to-peer sem expor IP. Relay age como intermediário entre Host e Clients.  
**Authentication:** Login anônimo via `SignInAnonymouslyAsync()`. Necessário para usar Relay e Lobby.

### Autenticação — decisão de perfil por PID

Sem perfis separados, todas as instâncias MPPM compartilham a mesma sessão de autenticação. Isso corrompre o `JoinAllocationAsync` no Relay (retorna "Not Found" mesmo com código válido).

**Solução:**
```csharp
var options = new InitializationOptions();
#if UNITY_EDITOR
options.SetProfile($"Player_{System.Diagnostics.Process.GetCurrentProcess().Id}");
#endif
await UnityServices.InitializeAsync(options);
```

Cada processo MPPM tem PID único → sessão de autenticação isolada → Relay funciona independentemente em cada instância.

### Custos (camada gratuita)

| Serviço | Limite gratuito | Status do projeto |
|---|---|---|
| Relay | 50 CCU + 50 GB/mês | Bem dentro do limite |
| Lobby | 200 lobbies + 2.000 membros/mês | Bem dentro do limite |
| Authentication | Ilimitado (anônimo) | — |

---

## Multiplayer Play Mode 2.0.2

**O que faz:** Múltiplas instâncias do jogo no Editor sem build.

**Mudança no 2.0:** `Unity.Multiplayer.Playmode` não está acessível via `using` convencional. A API `CurrentPlayer.ReadOnlyTags()` foi descontinuada.

**Configuração atual:**

| Instância | Comportamento |
|---|---|
| Main Editor | Bootstrap → MainMenu → Lobby → Criar Sala (Host via Relay) |
| Virtual Player 2/3/4 | Bootstrap → MainMenu → Lobby → Entrar com código (Client via Relay) |

> O auto-connect via `-mppmTag` foi **removido** quando o Lobby passou a gerenciar toda a conexão.

---

## New Input System

**O que faz:** Sistema moderno de input da Unity, baseado em `InputActionAsset` e bindings.

**Por que não o Input System antigo (`Input.GetKey`):**
- Rebind de teclas sem código adicional via `PerformInteractiveRebinding()`
- Serialização de overrides como JSON
- Separação clara entre input e lógica de jogo

**Asset criado:** `Assets/_EchoesInTheDark/Input/EchoesInputActions.inputactions`

**Persistência de rebinds:** `PlayerPrefs["InputBindingOverrides"]` — JSON serializado do asset completo. Carregado no `InputManager.Awake()` e salvo via `InputManager.SalvarOverrides()`.

**Teclas protegidas:**
- `ESC` — nunca pode ser atribuída; ao pressionar durante rebind, cancela e fecha o painel

---

## A* Pathfinder Pro

**O que faz:** Pathfinding para NPCs usando algoritmo A*.  
**Por que não NavMesh:** NavMesh é 3D-first; A* tem suporte nativo a 2D top-down.  
**Onde fica:** `Assets/Plugins/AstarPathfinder/` — nunca modificar.  
**Regra de rede:** Todo pathfinding roda exclusivamente no Host. Clients recebem posição via `NetworkVariable<Vector2>`.

---

## AudioMixer — GameAudioMixer

**Localização:** `Assets/_EchoesInTheDark/Audio/GameAudioMixer`

```
Master (VolGeral — parâmetro exposto)
├── Musica (VolMusica — parâmetro exposto)
├── SFX    (VolSFX   — parâmetro exposto)
└── Chat   (VolChat  — parâmetro exposto)
```

O `SettingsManager` controla o volume via `AudioMixer.SetFloat(nome, valorEmDécibeis)`.

Conversão: `linear > 0.001 ? Log10(linear) * 20 : -80` — valor 0 → -80 dB (silêncio), valor 1 → 0 dB (máximo).

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
  ├── Conectam ao Relay com o Join Code do Lobby
  ├── Recebem estado via NetworkVariable
  └── Enviam ações via ServerRpc
```

**Risco conhecido:** Se o Host sair, a sessão termina. Mitigação futura: host migration.

---

## Fluxo de Conexão (Produção)

```
HOST
1. SignInAnonymouslyAsync()
2. RelayService.CreateAllocationAsync(15)       → allocation
3. RelayService.GetJoinCodeAsync(allocationId)  → joinCode (6 chars)
4. transport.SetRelayServerData(allocation)
5. LobbyService.CreateLobbyAsync(name, 16, { RelayJoinCode: joinCode })
6. NetworkManager.StartHost()

CLIENT
7. SignInAnonymouslyAsync()
8. LobbyService.JoinLobbyByCodeAsync(lobbyCode) → lobby
9. relayCode = lobby.Data["RelayJoinCode"].Value
10. RelayService.JoinAllocationAsync(relayCode)  → joinAllocation
11. transport.SetRelayServerData(joinAllocation)
12. NetworkManager.StartClient()
```

---

## Decisões Arquiteturais

### Por que Host Autoritativo e não Client-Side Prediction?

Echoes in the Dark é um jogo de ritmo **lento**. Latência de 100–300ms não impacta. Client-side prediction adiciona complexidade enorme sem benefício.

**Regra:** Toda validação de gameplay acontece no Host. Clients apenas solicitam ações via `ServerRpc`.

---

### NetworkVariable vs RPC

| Usar `NetworkVariable` quando... | Usar RPC quando... |
|---|---|
| Estado persistente que clients novos precisam ao conectar | Evento pontual (morte, votação, animação) |
| Valor muda com frequência baixa/média | Ação que acontece uma vez |
| Dirty tracking automático | Precisa enviar parâmetros complexos |

---

### Cenas e DontDestroyOnLoad

```
Bootstrap (nunca descarregada)
 ├── Bootstrap.cs
 ├── NetworkManager + UnityTransport
 ├── SceneLoader
 ├── SettingsManager
 └── InputManager

MainMenu / Lobby / Match (carregadas/descarregadas normalmente)
```

---

## Plataforma Alvo

- **PC — Windows 10/11 (64-bit)**
- Controles: teclado + mouse (rebindável)
- Gamepad: não prioritário no protótipo

---

*[← Voltar ao índice](../README.md)*
