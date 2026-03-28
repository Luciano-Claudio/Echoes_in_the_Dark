# 🌑 Echoes in the Dark — Documentação Técnica

> Multiplayer de dedução social 2D Top-Down em Unity 6.3  
> Stack: Netcode for GameObjects · Unity Multiplayer Services · A* Pathfinder Pro

---

## 📋 Status do Projeto

| Etapa | Status | Descrição |
|---|---|---|
| ✅ Estrutura de pastas | Concluído | Pastas criadas no projeto Unity |
| ✅ Multiplayer Center | Concluído | NGO + Multiplayer Services + Play Mode instalados |
| ✅ GDD v2.0 | Concluído | Game Design Document atualizado |
| 🔄 Documentação de código | Em andamento | Este repositório |
| ⏳ Cenas Bootstrap / MainMenu | Pendente | — |
| ⏳ NetworkManager real | Pendente | — |
| ⏳ Player base | Pendente | — |

---

## 📁 Índice da Documentação

### Fundação do Projeto
- [01 · Estrutura de Pastas](docs/01-estrutura-de-pastas.md) — Organização completa do projeto Unity, regras por pasta e o que evitar
- [02 · Stack Técnica](docs/02-stack-tecnica.md) — Todas as tecnologias, versões, pacotes e decisões arquiteturais

### Arquitetura Multiplayer
- [03 · Arquitetura Multiplayer](docs/03-arquitetura-multiplayer.md) — Host vs Client, NetworkVariable vs RPC, autoridade, fluxo de conexão

### Sistemas do Jogo
- [Sistema · Personagem](docs/sistemas/sistema-personagem.md) — Montagem visual (cor + partes + acessórios), visibilidade na escuridão
- [Sistema · Iluminação](docs/sistemas/sistema-iluminacao.md) — Tochas, escuridão, sincronização de estado de luz
- [Sistema · Papéis](docs/sistemas/sistema-papeis.md) — Guarda, Vampiro, Inocente — sorteio, dados, habilidades
- [Sistema · Missões](docs/sistemas/sistema-missoes.md) — Banco de missões, validação, progresso individual e global
- [Sistema · NPC](docs/sistemas/sistema-npc.md) — IA com A* Pathfinder Pro, comportamentos, sincronização

### Padrões e Convenções
- [04 · Convenções de Código](docs/04-convencoes-de-codigo.md) — Nomenclatura, padrões C#, separação de responsabilidades, regras da equipe

---

## 🏗️ Visão Geral da Arquitetura

```
Unity 6.3 LTS
│
├── Netcode for GameObjects 2.11.0   → sincronização de estado (NetworkVariable + RPC)
├── Unity Transport 2.6.0            → camada de transporte (UDP)
├── Unity Multiplayer Services       → Lobby + Relay + Session (sem servidor dedicado)
├── Multiplayer Play Mode            → teste local com até 4 instâncias
├── Multiplayer Tools                → debug de rede, profiler, simulação de latência
└── A* Pathfinder Pro                → navegação e IA dos NPCs (roda apenas no Host)
```

### Modelo de Hosting
```
Host (um dos jogadores)
 ├── Autoridade total sobre: papéis, validação de ações, missões, mortes, votação
 ├── Roda toda a IA dos NPCs
 └── Clients se conectam via Relay (sem IP exposto)
```

---

## 🗂️ Estrutura de Pastas (resumo)

```
Assets/
├── _EchoesInTheDark/
│   ├── Scripts/
│   │   ├── Core/          → Bootstrap, SceneLoader, eventos globais
│   │   ├── Network/       → conexão, spawn, relay
│   │   ├── Gameplay/
│   │   │   ├── Character/ → visual e animação compartilhados (player + NPC)
│   │   │   ├── Player/    → input humano, movimento, interação
│   │   │   ├── NPC/       → IA, pathfinding, comportamentos
│   │   │   ├── Roles/     → Guarda, Vampiro, Inocente
│   │   │   ├── Tasks/     → banco de missões, validação, progresso
│   │   │   ├── Lighting/  → tochas, estado de luz, visibilidade
│   │   │   ├── Meeting/   → reunião, votação, resolução
│   │   │   └── Match/     → state machine da partida
│   │   ├── UI/            → HUD, menus, lobby, votação
│   │   └── Services/      → Lobby, Relay, Session (Unity Services)
│   ├── Art/
│   │   ├── Characters/
│   │   │   ├── Variants/  → 13 pastas de cor → Head/Body/Hands/Feet
│   │   │   ├── Eyes/      → olhos visíveis no escuro
│   │   │   └── Guard/     → skins exclusivas do Guarda
│   │   ├── Environment/
│   │   ├── Animations/
│   │   └── UI/
│   ├── Prefabs/
│   ├── Scenes/            → Bootstrap · MainMenu · Lobby · Match
│   ├── ScriptableObjects/
│   ├── Audio/
│   └── Settings/
└── Plugins/
    └── AstarPathfinder/
```

---

## 📌 Decisões Arquiteturais Chave

| Decisão | Escolha | Motivo |
|---|---|---|
| Hosting | Client Hosted | Sem custo de servidor dedicado no protótipo |
| Conexão | Relay (sem IP direto) | Privacidade e facilidade de conexão |
| Autoridade | Host autoritativo | Toda validação de gameplay no host |
| NPC | IA apenas no Host | Consistência de estado, sem dessincronização |
| Iluminação | Estado sincronizado, visual local | Não networkar efeito de renderização |
| Colisão | Apenas com cenário | Players e NPCs se sobrepõem sem colisão |
| Sorteio de papéis | Host sorteia e envia via RPC | Apenas o host conhece todos os papéis |

---

*Documentação mantida junto ao repositório. Atualizar a cada nova implementação.*
