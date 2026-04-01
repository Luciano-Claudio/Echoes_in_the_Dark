# 08 · Sessão C — Lobby Completo

[← Voltar ao índice](../README.md)

> **Status: ✅ Concluído** — Sessão realizada em 01/04/2026  
> Lobby funcional com lista de players em tempo real, código mascarado, botão Pronto, polling e relay retry fix.

---

## O que foi implementado

### Prefab `PlayerListItem`

Criado em `Assets/_EchoesInTheDark/Prefabs/UI/PlayerListItem.prefab`:

```
PlayerListItem (GameObject)
├── PlayerListItemController.cs   ← script de controle
├── TextNome (TextMeshProUGUI)    ← nome do player + tag (Host)
└── ImagemStatus (Image)         ← verde = pronto, cinza = aguardando
```

`Setup(string nomeDisplay, bool pronto, bool ehHost)` configura o item:
- Host: rich text `$"{nome}  <size=70%><color=#AAAAAA>(Host)</color></size>"` + sempre verde
- Client: cor verde se pronto, cinza se aguardando

### Hierarquia final — `Lobby.unity`

Adotado um único `PanelSala` compartilhado (Host e Client), configurado por `ConfigureSalaParaHost()` / `ConfigureSalaParaClient()`:

```
Canvas
├── PanelEscolha
│   ├── ButtonCriarSala
│   └── ButtonAbrirEntrarCodigo
│
├── PanelLoading
│   └── TextLoading (TMP)
│
├── PanelEntrarCodigo
│   ├── InputCodigo (TMP_InputField)
│   ├── ButtonEntrar
│   └── ButtonVoltarEntrar
│
└── PanelSala                       ← compartilhado Host + Client
    ├── CodigoRow
    │   ├── TextCodigo              ← "••••••" ou código real
    │   ├── ButtonOlho              ← toggle visibilidade
    │   └── ButtonCopiar            ← copia para clipboard
    ├── TextJogadoresCount          ← "X/15 jogadores"
    ├── PlayerListContainer         ← filhos são instâncias de PlayerListItem
    ├── ButtonIniciarPartida        ← Host only, ativo quando todos prontos
    ├── ButtonPronto                ← Client only (fora do container!)
    └── ButtonSair

LobbyManager
└── LobbyController.cs
```

> **Detalhe crítico:** `ButtonPronto` deve ser filho direto de `PanelSala`, **nunca** filho de `PlayerListContainer`. `AtualizarListaJogadores()` destrói todos os filhos do container a cada poll — botão dentro do container seria destruído em 2s.

### `LobbyController.cs` — funcionalidades adicionadas

| Funcionalidade | Implementação |
|---|---|
| Tela de loading | `ShowPanel(_panelLoading)` antes de ops assíncronas |
| Código mascarado | `new string('•', _lobbyCodeReal.Length)` por padrão |
| Toggle olho | `_codigoVisivel` bool + `AtualizarDisplayCodigo()` |
| Copiar para clipboard | `GUIUtility.systemCopyBuffer = _lobbyCodeReal` |
| Lista de players | `AtualizarListaJogadores(Lobby)` — destroy + re-instantiate |
| Polling | `PollLobbyCoroutine()` — WaitForSeconds(2f) com guard `_isRefreshing` |
| Heartbeat | `Update()` — somente Host, a cada 15s |
| Botão Pronto | Toggle otimista + `UpdatePlayerReadyAsync()` + rollback em falha |
| Gating IniciarPartida | `TodosClientesProntos() && lobby.Players.Count >= 2` |
| PanelSala unificado | `ConfigureSalaParaHost()` / `ConfigureSalaParaClient()` |

### `LobbyNetworkService.cs` — métodos utilizados

- `UpdatePlayerReadyAsync(bool isReady)` — armazena `"IsReady": "1"/"0"` nos dados do player
- `TodosClientesProntos()` — percorre players, pula o Host, exige pelo menos 1 client com `"IsReady"=="1"`
- `RefreshLobbyAsync()` — `GetLobbyAsync(id)` retorna snapshot atual
- `SendHeartbeat()` — mantém o Lobby ativo (evita expiração em 30s)

---

## Correção — "Join Code Not Found" ✅ Resolvido

**Causa:** Atraso de propagação do Relay no MPPM. Quando VP2 tenta `JoinAllocationAsync`, o join code ainda não está disponível nos servidores Relay (problema exclusivo de teste local com Virtual Players — em builds de produção não ocorre).

**Solução implementada** em `RelayNetworkService.cs`:

```csharp
private const int JOIN_MAX_RETRIES   = 3;
private const int JOIN_RETRY_BASE_MS = 1500; // tentativa 1: imediata, 2: +1.5s, 3: +3s

// JoinRelay() faz até 3 tentativas com backoff exponencial
// Normaliza joinCode: .Trim().ToUpper() antes de qualquer tentativa
```

**Resultado nos testes MPPM:** Segunda execução conectou com sucesso na tentativa 3 (~6s após a primeira tentativa).

---

## Checklist — Sessão C

- [x] Corrigir `Join Code Not Found` (retry exponencial — resolvido)
- [x] Tela de loading ao criar/entrar na sala
- [x] Código mascarado `••••••` com botão olho e copiar
- [x] Lista de players com polling a cada 2s
- [x] Header `X/15 jogadores`
- [x] `PanelSala` unificado para Host e Client
- [x] Botão `Pronto` para Clients (com toggle otimista + rollback)
- [x] `ButtonIniciarPartida` ativo apenas quando todos prontos
- [x] `PlayerListItem.prefab` criado e configurado
- [x] Wire-up completo do `LobbyController` no Inspector

---

## Estado do Lobby após esta sessão

```
Host flow:
  PanelEscolha → [PanelLoading "Criando sala..."] → PanelSala
    PanelSala: código mascarado, lista players, ButtonIniciarPartida (desabilitado até todos prontos)

Client flow:
  PanelEscolha → PanelEntrarCodigo → [PanelLoading "Entrando na sala..."] → PanelSala
    PanelSala: código mascarado, lista players, ButtonPronto (toggle Pronto/Aguardando)
```

**Testado com MPPM** (Main Player = Host, VP2 = Client): ambos veem a lista atualizada em tempo real com cores de status corretas.

---

*[← Voltar ao índice](../README.md)*
