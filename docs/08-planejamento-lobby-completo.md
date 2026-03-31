# 08 · Sessão C — Lobby Completo

[← Voltar ao índice](../README.md)

> Plano detalhado da próxima sessão: transformar o Lobby funcional em uma sala completa com lista de players, código mascarado, limite de 15, botão Pronto e tela de carregamento.  
> **Pré-requisito:** Conexão Relay + Lobby funcionando (ver [06](06-planejamento-mainmenu-lobby.md)).

---

## Objetivo da Sessão

```
Estado atual do Lobby:
  ├── Host: cria sala, exibe código → ✅
  └── Client: digita código, conecta → ⚠️ (Join code not found intermitente)

Estado alvo após esta sessão:
  ├── Host: tela de loading ao criar → código mascarado "******"
  │         botões: olho (revelar) + copiar
  │         lista de players com X/15
  │         StartPartida ativo só quando todos deram Pronto
  └── Client: mesma tela do Host (shared room view)
              botão Pronto (em vez de StartPartida)
              lista de players igual ao Host
```

---

## 1. Tela de Carregamento ao Criar Sala

Ao clicar "Criar Sala", o processo de `CreateRelayAndGetJoinCode` + `CreateLobby` demora 1–2 segundos. A UI deve refletir esse estado.

**Implementação planejada:**

```
PanelHost
├── PanelLoading (ativo durante criação)
│   ├── Spinner / barra animada
│   └── TextMeshProUGUI "Criando sala..."
└── PanelSalaAberta (ativo após criação)
    ├── CodigoRow
    │   ├── TextCodigo "******"
    │   ├── BotaoOlho  (toggle revelar/ocultar)
    │   └── BotaoCopiar
    ├── ListaPlayers (ScrollView)
    │   ├── Header "X/15 jogadores"
    │   └── [PlayerListItem] × N
    └── BotaoIniciarPartida (inativo por padrão)
```

---

## 2. Código Mascarado — Segurança para Streamers

O código da sala (`LobbyCode`) fica oculto por padrão como `******`. O jogador pode revelar ou copiar conforme necessário.

**Por que isso importa:** Um streamer transmitindo ao vivo não quer que viewers entrem na sua sala privada.

**Implementação planejada em `LobbyController.cs`:**

```csharp
private bool _codigoVisivel = false;
private string _lobbyCode;

private void OnOlhoClicked()
{
    _codigoVisivel = !_codigoVisivel;
    _textCodigo.text = _codigoVisivel ? _lobbyCode : "******";
    // Atualizar ícone do botão (olho aberto / fechado)
}

private void OnCopiarClicked()
{
    GUIUtility.systemCopyBuffer = _lobbyCode;
    // Feedback visual breve: "Copiado!"
}
```

---

## 3. Lista de Players Conectados

A lista deve atualizar em tempo real conforme players entram ou saem.

**Fonte de dados:** Unity Lobby API — `LobbyService.Instance.GetLobbyAsync(lobbyId)` retorna a lista atual de players com seus dados.

**Polling:** A cada 2s o Host (e também os Clients) fazem polling do Lobby para atualizar a lista. Em uma implementação mais sofisticada, usaríamos Lobby Callbacks (WebSockets) — para esta sessão, polling é suficiente.

**Estrutura planejada:**

```csharp
// LobbyNetworkService.cs — novo método
public async Task<Lobby> RefreshLobby()
{
    if (_currentLobby == null) return null;
    _currentLobby = await LobbyService.Instance.GetLobbyAsync(_currentLobby.Id);
    return _currentLobby;
}
```

```csharp
// LobbyController.cs — Update com polling
private float _refreshTimer;
private const float REFRESH_INTERVAL = 2f;

private void Update()
{
    // Heartbeat (Host only)
    if (NetworkManager.Singleton.IsHost)
    {
        _heartbeatTimer += Time.deltaTime;
        if (_heartbeatTimer >= HEARTBEAT_INTERVAL)
        {
            _heartbeatTimer = 0f;
            _ = _lobbyService.SendHeartbeat();
        }
    }

    // Refresh de lista (Host e Client)
    if (_salaAberta)
    {
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= REFRESH_INTERVAL)
        {
            _refreshTimer = 0f;
            _ = RefreshPlayerListAsync();
        }
    }
}
```

**PlayerListItem:** Prefab simples com `TextMeshProUGUI` (nome do player) e um ícone de status (Pronto / Aguardando).

---

## 4. Limite de 15 Players

O Lobby já está criado com `maxPlayers: 16` (15 players + 1 host). A lista exibe `X/15` onde X é `_currentLobby.Players.Count`.

```csharp
_headerText.text = $"{_currentLobby.Players.Count}/15 jogadores";
```

Quando a sala atinge 15 players, o botão "Entrar com Código" do PanelEscolha deve ficar inativo — verificado ao entrar no Lobby antes de exibir o InputField.

---

## 5. Painel Compartilhado — Host e Client veem a mesma UI

Após conectar (tanto Host quanto Client), ambos veem o mesmo `PanelSalaAberta`.

**Diferenças por papel:**

| Elemento | Host | Client |
|---|---|---|
| BotaoIniciarPartida | Visível (ativo quando todos prontos) | Oculto |
| BotaoProto | Oculto | Visível |
| BotaoOlho / BotaoCopiar | Visível | Visível |
| TextCodigo | `******` por padrão | `******` por padrão |
| ListaPlayers | Visível, atualiza em tempo real | Visível, atualiza em tempo real |

```csharp
// Ao montar o painel após conexão:
_botaoIniciarPartida.gameObject.SetActive(NetworkManager.Singleton.IsHost);
_botaoProto.gameObject.SetActive(!NetworkManager.Singleton.IsHost);
```

---

## 6. Botão Pronto e Validação para Iniciar

O Host só pode iniciar a partida quando **todos** os players clicaram "Pronto".

**Dados do Lobby:** O estado de "pronto" de cada player é armazenado nos dados do player no Lobby (`PlayerDataObject`).

```csharp
// Client clica Pronto
public async Task SetPlayerReady(bool isReady)
{
    var data = new Dictionary<string, PlayerDataObject>
    {
        { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member,
            isReady ? "1" : "0") }
    };
    await LobbyService.Instance.UpdatePlayerAsync(
        _currentLobby.Id,
        AuthenticationService.Instance.PlayerId,
        new UpdatePlayerOptions { Data = data }
    );
}
```

```csharp
// Host verifica se todos estão prontos (no refresh da lista)
private bool TodosProntos()
{
    foreach (var player in _currentLobby.Players)
    {
        if (!player.Data.ContainsKey("IsReady")) return false;
        if (player.Data["IsReady"].Value != "1") return false;
    }
    return true;
}

// Atualiza interatividade do botão
_botaoIniciarPartida.interactable = TodosProntos() && _currentLobby.Players.Count >= 2;
```

---

## 7. Corrigir "Join Code Not Found" — Prioridade #1

O problema será investigado com a lista de players visível. A hipótese mais provável no estado atual é que a **alocação Relay expira** antes do Client tentar se conectar — isso acontece se o Host demora muito para chamar `StartHost()` após `CreateAllocationAsync()`.

**Investigação planejada:**
1. Adicionar log com timestamp entre `CreateAllocationAsync()` e `StartHost()`
2. Verificar se o tempo entre criação e tentativa do Client é maior que o TTL da alocação Relay
3. Se confirmado: mover `StartHost()` para imediatamente após `SetRelayServerData()`, antes de criar o Lobby

**Possível causa secundária:** O Virtual Player faz `ShutdownIfRunningAsync()` que pode interferir com o estado interno do Transport antes de `JoinAllocationAsync`. Investigar se o Transport precisa de reinicialização após Shutdown.

---

## Estrutura de UI Alvo (Lobby.unity)

```
Lobby.unity
├── Canvas
│   ├── PanelEscolha                  ← "Criar Sala" ou "Entrar com Código"
│   │   ├── ButtonCriarSala
│   │   └── ButtonEntrarCodigo
│   │
│   ├── PanelHost                     ← substituir pelo novo design
│   │   ├── PanelLoading              ← spinner durante criação
│   │   └── PanelSalaAberta           ← sala pronta
│   │       ├── CodigoRow
│   │       │   ├── TextCodigo        ← "******" por padrão
│   │       │   ├── BotaoOlho
│   │       │   └── BotaoCopiar
│   │       ├── TextHeader            ← "X/15 jogadores"
│   │       ├── ScrollView            ← lista de players
│   │       │   └── Content (PlayerListItem × N)
│   │       ├── BotaoIniciarPartida   ← só Host, só ativo quando todos prontos
│   │       └── BotaoVoltar
│   │
│   ├── PanelClient                   ← InputField + botão Entrar
│   │
│   └── PanelSalaCliente              ← NOVO: mesmo visual que PanelSalaAberta
│       ├── CodigoRow (idêntico)
│       ├── TextHeader (idêntico)
│       ├── ScrollView (idêntico)
│       ├── BotaoProto                ← só Client
│       └── BotaoVoltar
│
└── LobbyManager
    └── LobbyController.cs
```

---

## Checklist da Próxima Sessão

- [ ] Corrigir `Join Code Not Found` (investigar + resolver)
- [ ] Tela de loading ao criar sala
- [ ] Código mascarado `******` com botão olho e copiar
- [ ] Lista de players com polling a cada 2s
- [ ] Header `X/15 jogadores`
- [ ] `PanelSalaCliente` com mesmo layout do Host
- [ ] Botão `Pronto` para Clients
- [ ] `BotaoIniciarPartida` ativo apenas quando todos prontos
- [ ] Limite: bloquear entrada quando sala tiver 15 players

---

*[← Voltar ao índice](../README.md)*
