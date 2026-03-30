# 05 · Bootstrap e Cenas

[← Voltar ao índice](../README.md)

> O que foi implementado, decisões tomadas durante a implementação e problemas resolvidos.  
> Sessão concluída em 28/03/2026.

---

## O que foi Implementado

### 4 Cenas Criadas

| Cena | Índice no Build Settings | Função |
|---|---|---|
| `Bootstrap.unity` | 0 (obrigatório primeiro) | Entry point — nunca descarregada |
| `MainMenu.unity` | 1 | Tela inicial |
| `Lobby.unity` | 2 | Criação e entrada de sala |
| `Match.unity` | 3 | Gameplay da partida |

> **Regra:** A Bootstrap.unity DEVE ser índice 0 no Build Settings. O Unity carrega a primeira cena automaticamente ao iniciar a build.

---

### NetworkManager Prefab

**Localização:** `Assets/_EchoesInTheDark/Prefabs/Network/NetworkManager.prefab`

**Componentes configurados:**

| Componente | Campo | Valor |
|---|---|---|
| Network Manager | Network Transport | NetworkManager (Unity Transport) |
| Network Manager | Enable Scene Management | ✅ |
| Network Manager | Load Scene Time Out | 120 |
| Network Manager | Default Player Prefab | None (configurar na sessão do Player) |
| Unity Transport | Protocol Type | **Relay Unity Transport** |
| Unity Transport | Allow Remote Connections | ☐ (desabilitado — só para testes locais) |

> **Decisão arquitetural:** O prefab fica configurado com `Relay Unity Transport` permanentemente. No editor (MPPM), o `Bootstrap.cs` sobrescreve o protocolo para IP direto em runtime via `SetConnectionData()`. Em produção, o fluxo de Lobby/Relay configura os dados corretos antes de `StartHost/Client`.

---

### Hierarquia da Cena Bootstrap

```
Bootstrap.unity
├── Bootstrap          ← Bootstrap.cs (DontDestroyOnLoad)
└── NetworkManager     ← Network Manager + Unity Transport (DontDestroyOnLoad)
```

> **Sem câmera:** A Bootstrap não renderiza nada. A câmera fica na MainMenu e demais cenas.

---

### Bootstrap.cs

**Localização:** `Assets/_EchoesInTheDark/Scripts/Core/Bootstrap.cs`  
**Namespace:** `EchoesInTheDark.Core`

**Responsabilidades em ordem de execução:**

```
Awake()
  │
  ├── DontDestroyOnLoad(gameObject)
  │
  ├── InitializeServicesAsync()
  │     ├── Verifica se já inicializado (idempotente)
  │     └── UnityServices.InitializeAsync()
  │
  ├── AutoConnectInEditor()        ← só compila em #if UNITY_EDITOR
  │     ├── Lê args de linha de comando
  │     ├── Detecta -mppmTag <valor>
  │     ├── SetConnectionData("127.0.0.1", 7777)  ← IP direto no editor
  │     ├── Virtual Player (tag conhecida) → StartClient()
  │     └── Main Editor (sem tag) → StartHost()
  │
  └── LoadMainMenu()
        └── SceneManager.LoadScene("MainMenu", Single)
```

---

### Solução do Auto-Connect (MPPM 2.x)

**Problema encontrado:** O pacote Multiplayer Play Mode 2.0.2 migrou o código `CurrentPlayer` para dentro do engine Unity 6.3. O namespace `Unity.Multiplayer.Playmode` não está mais acessível via `using` convencional em scripts de usuário sem Assembly Definition configurado.

**Solução adotada:** Leitura de argumentos de linha de comando injetados pelo MPPM:

```csharp
// O MPPM injeta -mppmTag <valor> em cada Virtual Player
string[] args = Environment.GetCommandLineArgs();
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "-mppmTag" && i + 1 < args.Length)
    {
        string tag = args[i + 1].ToLower();
        isVirtualPlayer = tag == "vampire" || tag == "innocent" || tag == "guard";
        break;
    }
}
```

**Por que funciona:** O MPPM injeta `-mppmTag <valor>` nos argumentos de cada Virtual Player desde a versão 1.0.0 e esse mecanismo nunca foi removido (verificado no CHANGELOG até 2.0.2). Não depende de namespace externo — apenas `System.Environment`.

---

### Solução do Relay no Editor

**Problema encontrado:** Com `Protocol Type = Relay Unity Transport`, o `StartHost()` lança exceção imediatamente:
```
Exception: You must call SetRelayServerData() before calling StartClient() or StartServer()
```

**Solução adotada:** Sobrescrever o protocolo em runtime apenas no editor:

```csharp
#if UNITY_EDITOR
Unity.Netcode.Transports.UTP.UnityTransport transport =
    NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();

transport.SetConnectionData("127.0.0.1", 7777);
// SetConnectionData() muda internamente o protocol para IP direto
#endif
```

**Decisão arquitetural:** O prefab permanece configurado como Relay (para produção). O Bootstrap só altera isso em runtime no editor. Em produção, o `RelayService.cs` (sessão futura) chamará `SetRelayServerData()` com os dados reais antes de `StartHost/Client`.

---

## Problemas Resolvidos

| Problema | Causa | Solução |
|---|---|---|
| Namespace `Unity.Multiplayer.Playmode` não encontrado | MPPM 2.0 migrou código para o engine | Usar `Environment.GetCommandLineArgs()` |
| `ReadOnlyTags()` obsoleto | API atualizada no MPPM 2.x | Substituído por leitura de args |
| `SetRelayServerData()` obrigatório | Transport em modo Relay exige dados antes de Start | `SetConnectionData()` no editor sobrescreve para IP direto |
| `NetworkPrefab cannot be null` | Entrada vazia na lista de prefabs | Remover entrada vazia no Inspector → Apply All |
| `[Vivox] server is null or empty` | Vivox instalado sem projeto configurado | Remover pacote Vivox pelo Package Manager |
| Warning `[SerializeReference] Serializable` | Bug interno do pacote Multiplayer Center | Ignorar — não tem solução do lado do desenvolvedor |
| `var` com `GetComponent<>` em linha quebrada | C# 9.0 não suporta `target-typed new` com tipo genérico multi-linha | Usar tipo explícito em linha única |

---

## Resultado Final do Console (Play Mode)

```
✅ [Bootstrap] Inicializando Unity Services...
✅ [Bootstrap] Unity Services prontos.
✅ [Bootstrap] Main Editor → StartHost (IP direto)
✅ [Bootstrap] Carregando MainMenu...
```

Hierarchy em runtime:
```
MainMenu
DontDestroyOnLoad
  ├── Bootstrap
  ├── NetworkManager
  └── [Debug Updater]   ← gerado pelo Multiplayer Tools, normal
```

---

*[← Voltar ao índice](../README.md)*