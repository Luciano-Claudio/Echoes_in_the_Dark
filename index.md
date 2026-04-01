# 🌑 Echoes in the Dark — Documentação Técnica

> Multiplayer de dedução social 2D Top-Down em Unity 6.3  
> Stack: Netcode for GameObjects · Unity Multiplayer Services · A* Pathfinder Pro

---

## 📋 Status do Projeto

| Etapa | Status | Descrição |
|---|---|---|
| ✅ Estrutura de pastas | Concluído | Pastas criadas no projeto Unity |
| ✅ Multiplayer Center | Concluído | NGO + Multiplayer Services + Play Mode instalados |
| ✅ GDD v2.0 | Concluído | Game Design Document finalizado |
| ✅ Documentação de sistemas | Concluído | Personagem, Iluminação, Papéis, Missões, NPC |
| ✅ Cenas e Bootstrap | Concluído | 4 cenas criadas, Bootstrap.cs refatorado com fluxo Logo→Intro→Loading |
| ✅ NetworkManager real | Concluído | Prefab configurado com Relay Unity Transport |
| ✅ SceneLoader | Concluído | Navegação centralizada entre cenas |
| ✅ MainMenu | Concluído | UI funcional com 4 botões (Jogar, Configurações, Shopping, Sair) |
| ✅ Lobby — Conexão Relay | Concluído | Host cria sala via Relay, Client conecta com código |
| ✅ Autenticação Unity Services | Concluído | SignInAnonymously com perfil por PID (MPPM safe) |
| ✅ Sistema de Settings | Concluído | 4 abas: Geral, Gráficos, Som, Controles |
| ✅ AudioMixer | Concluído | 4 canais expostos: VolGeral, VolMusica, VolSFX, VolChat |
| ✅ Sistema de Rebind | Concluído | Rebind de teclas com detecção de conflito e bloqueio de ESC |
| ✅ Lobby — Interface completa | Concluído | Lista de players, loading, código mascarado, botão Pronto |
| ⏳ Player base | Pendente | NetworkObject + movimento sincronizado |
| ⏳ Match scene | Pendente | Gameplay completo |

---

## 📁 Índice da Documentação

### Fundação do Projeto
- [01 · Estrutura de Pastas](docs/01-estrutura-de-pastas.md)
- [02 · Stack Técnica](docs/02-stack-tecnica.md)

### Arquitetura Multiplayer
- [03 · Arquitetura Multiplayer](docs/03-arquitetura-multiplayer.md)

### Implementações Realizadas
- [05 · Bootstrap e Cenas](docs/05-bootstrap-e-cenas.md) — Fluxo Logo → Intro → Loading → MainMenu
- [07 · MainMenu e Settings](docs/07-mainmenu-e-settings.md) — Tudo implementado na sessão atual

### Planejamento
- [06 · Sessão B — Lobby Conexão](docs/06-planejamento-mainmenu-lobby.md) — Relay + Lobby implementados, problemas conhecidos
- [08 · Sessão C — Lobby Completo](docs/08-planejamento-lobby-completo.md) — ✅ Concluído: PlayerListItem prefab, PanelSala unificado, relay retry fix, botão Pronto

### Sistemas do Jogo
- [Sistema · Personagem](docs/sistemas/sistema-personagem.md)
- [Sistema · Iluminação](docs/sistemas/sistema-iluminacao.md)
- [Sistema · Papéis](docs/sistemas/sistema-papeis.md)
- [Sistema · Missões](docs/sistemas/sistema-missoes.md)
- [Sistema · NPC](docs/sistemas/sistema-npc.md)

### Padrões e Convenções
- [04 · Convenções de Código](docs/04-convencoes-de-codigo.md)

---

## 🏗️ Visão Geral da Arquitetura

```
Unity 6.3 LTS
│
├── Netcode for GameObjects 2.11.0   → sincronização de estado
├── Unity Transport 2.6.0            → camada UDP
├── Unity Multiplayer Services       → Lobby + Relay + Authentication
├── Multiplayer Play Mode 2.0.2      → teste local com até 4 instâncias
├── Multiplayer Tools 2.2.8          → debug de rede
├── New Input System                 → controles + rebind
└── A* Pathfinder Pro                → IA dos NPCs (Host only)
```

### Fluxo de Cenas

```
Bootstrap.unity (índice 0 — nunca descarregada)
  └── DontDestroyOnLoad: Bootstrap + NetworkManager + SceneLoader
                       + SettingsManager + InputManager
       │
       ▼  [Logo do estúdio → Intro (vídeo) → Loading (serviços)]
       │
MainMenu.unity (índice 1)
  └── Botões: Jogar, Configurações, Shopping, Sair
  └── SettingsPanel: Geral | Gráficos | Som | Controles
       │
       ▼
Lobby.unity (índice 2)
  ├── Host: Criar Sala → Relay allocation → código mascarado → lista de players
  └── Client: Entrar com código → painel compartilhado → botão Pronto
       │
       ▼
Match.unity (índice 3)
  └── Gameplay completo
```

### Modelo de Hosting

```
Host (um dos jogadores)
 ├── Autoridade sobre: papéis, missões, mortes, votação
 ├── Roda toda a IA dos NPCs
 └── Clients conectam via Relay (sem IP exposto)
```

### DontDestroyOnLoad — Singletons permanentes

```
DontDestroyOnLoad
 ├── Bootstrap          → inicialização, fluxo de intro
 ├── NetworkManager     → conexão NGO
 ├── SceneLoader        → navegação entre cenas
 ├── SettingsManager    → persistência de configurações (PlayerPrefs)
 └── InputManager       → InputActionAsset + overrides de bindings
```

---

## 📌 Decisões Arquiteturais Vigentes

| Decisão | Escolha |
|---|---|
| Netcode | Netcode for GameObjects |
| Hosting | Client Hosted via Unity Relay |
| Autoridade | Host autoritativo |
| NPC IA | A* Pathfinder Pro, Host only |
| Iluminação | `NetworkVariable<bool>` — visual é local |
| Colisão | Só com cenário |
| Sorteio de papéis | Host sorteia, envia via TargetClientRpc |
| Personagem | Marshmallow 4 partes, 13 cores, acessórios |
| Auto-connect editor | Removido — Lobby gerencia toda conexão |
| Transport produção | Relay Unity Transport via `SetRelayServerData()` |
| Autenticação | `SignInAnonymouslyAsync()` com perfil por PID do processo |
| Persistência de settings | `PlayerPrefs` via `SettingsManager` |
| Rebind de teclas | `InputActionRebindingExtensions` + `PlayerPrefs` JSON |
| Localização | `LocalizationManager` com JSONs (scaffold — implementação futura) |

---

*Documentação mantida junto ao repositório. Atualizar a cada nova implementação.*
