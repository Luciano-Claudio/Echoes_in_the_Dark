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
| ✅ Cenas e Bootstrap | Concluído | 4 cenas criadas, Bootstrap.cs funcional |
| ✅ NetworkManager real | Concluído | Prefab configurado com Relay Unity Transport |
| 🔄 MainMenu | Em andamento | Próxima sessão |
| ⏳ Lobby (criação de sala) | Pendente | Após MainMenu |
| ⏳ Lobby (entrada por código) | Pendente | — |
| ⏳ Player base | Pendente | — |
| ⏳ Match scene | Pendente | — |

---

## 📁 Índice da Documentação

### Fundação do Projeto
- [01 · Estrutura de Pastas](docs/01-estrutura-de-pastas.md)
- [02 · Stack Técnica](docs/02-stack-tecnica.md)

### Arquitetura Multiplayer
- [03 · Arquitetura Multiplayer](docs/03-arquitetura-multiplayer.md)

### Implementações Realizadas
- [05 · Bootstrap e Cenas](docs/05-bootstrap-e-cenas.md) — O que foi implementado, decisões tomadas, problemas resolvidos

### Planejamento
- [06 · MainMenu → Lobby](docs/06-planejamento-mainmenu-lobby.md) — Próximos passos detalhados com arquitetura e código planejado

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
├── Unity Multiplayer Services       → Lobby + Relay + Session
├── Multiplayer Play Mode 2.0.2      → teste local com até 4 instâncias
├── Multiplayer Tools 2.2.8          → debug de rede
└── A* Pathfinder Pro                → IA dos NPCs (Host only)
```

### Fluxo de Cenas

```
Bootstrap.unity (índice 0 — nunca descarregada)
  └── DontDestroyOnLoad: Bootstrap + NetworkManager
       │
       ▼
MainMenu.unity (índice 1)
  └── Botões: Jogar, Configurações, Sair
       │
       ▼
Lobby.unity (índice 2)
  ├── Host: Criar Sala → Relay allocation → código de 6 dígitos
  └── Client: Entrar com código → conectar via Relay
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
| Auto-connect editor | `-mppmTag` via args de linha de comando |
| Transport no editor | IP direto (127.0.0.1:7777) — Relay só em produção |

---

*Documentação mantida junto ao repositório. Atualizar a cada nova implementação.*