<div align="center">

# 🔥 SparkCoop

### Co-op para 2 jogadores em *Spark in the Dark*
### 2-player co-op for *Spark in the Dark*

[![Versão](https://img.shields.io/badge/vers%C3%A3o-0.6.0-orange)](https://github.com/Celsob2/sitd-coop/releases)
[![Licença: GPL v3](https://img.shields.io/badge/licen%C3%A7a-GPLv3-blue.svg)](LICENSE)
[![BepInEx 6](https://img.shields.io/badge/BepInEx-6%20(Unity%20Mono)-6e40c9)](https://github.com/BepInEx/BepInEx)
[![Steam](https://img.shields.io/badge/Steam-sala%20%26%20convite-1b2838?logo=steam)](https://store.steampowered.com/app/1315180/)
[![Feito no Brasil](https://img.shields.io/badge/feito%20no-Brasil%20%F0%9F%87%A7%F0%9F%87%B7-009c3b)](#-sobre-o-projeto)
[![Claude Code](https://img.shields.io/badge/desenvolvido%20com-Claude%20Code-d97757)](https://claude.com/claude-code)

**[🇧🇷 Português](#-português)** · **[🇺🇸 English](#-english)**

</div>

> [!WARNING]
> Mod **experimental e não oficial**, sem ligação com os desenvolvedores do jogo. Este repositório tem só código próprio;
> nenhum arquivo do jogo é distribuído. Cada jogador precisa da **sua própria cópia** do jogo na Steam.
> **Faça backup dos seus saves antes de jogar.**
>
> **Experimental, unofficial** mod, not affiliated with the game's developers. Only original code lives here; no game
> files are distributed. Each player needs **their own copy** of the game on Steam. **Back up your saves before playing.**

---

## 🇧🇷 Português

### 💬 Sobre o projeto

Sou um desenvolvedor brasileiro e gosto muito de *Spark in the Dark*. Faltava uma coisa: **jogar com um amigo**.
O jogo é só single-player, então criei o modo cooperativo.

O SparkCoop foi desenvolvido em conjunto com o **[Claude Code](https://claude.com/claude-code)**, usando
**engenharia reversa** para entender como o jogo funciona por dentro (mapa, criaturas, morte, descanso, itens) sem ter o
código-fonte, e testado parte por parte com duas cópias do jogo abertas lado a lado.

### 📦 Instalação (2 minutos)

1. Baixe o **`SparkCoop-x.y.z.zip`** na página de **[Releases](https://github.com/Celsob2/sitd-coop/releases)** (já vem com o BepInEx 6).
2. Na Steam: botão direito em *Spark in the Dark* → **Gerenciar** → **Ver arquivos locais**.
3. **Faça backup dos saves:** copie `%USERPROFILE%\AppData\LocalLow\Stellar Fish\Spark in the Dark` para outro lugar.
4. Extraia **o conteúdo** do zip nessa pasta: `winhttp.dll` fica **ao lado** de `Spark in the Dark.exe`.
5. Abra o jogo **pela Steam**. No menu aparece o painel **SparkCoop** no canto superior direito. ✅

> [!IMPORTANT]
> Os dois jogadores precisam da **mesma versão** do SparkCoop. Versões diferentes não se conectam.
> Sem o painel no menu? O antivírus pode ter apagado o `winhttp.dll` — libere o arquivo e extraia de novo.

**Remover:** apague `winhttp.dll`, `doorstop_config.ini` e a pasta `BepInEx` da pasta do jogo. Seus saves continuam lá.

### 🕹️ Como jogar

**Novo jogo**
1. **Host:** no painel do menu escolha a **vida dos monstros** e a regra da **queda no abismo**, clique em
   **Co-op: criar sala** → **Convidar amigo** e escolha o amigo na Steam.
2. **Amigo:** aceite o convite (ou *Entrar no jogo* na lista de amigos). Com o jogo fechado, a Steam abre e entra sozinha.
3. Esperem **"conectado: sim"** nos dois painéis.
4. **Host:** *Novo Jogo* → escolha a dificuldade e a classe → comece.
5. **Amigo:** *Novo Jogo* → escolha a classe (a dificuldade fica travada na do host) → comece **depois** do host.
6. O painel do canto superior esquerdo deve mostrar **`mundo: IGUAL`**.

**Continuar outro dia**
1. **Host:** *Continuar jogo* → o save dele.
2. **Amigo:** espere aparecer **"Mundo do host recebido"** no painel e clique **uma vez** em *Continuar jogo* → o herói dele.
   O mod cria `coop_<save>.save` (mundo do host + herói do amigo). O save solo do amigo não é alterado.

### ✨ Funcionalidades

#### 🌍 Mundo e conexão
| | O que faz |
|---|---|
| 🎮 **Sala pela Steam** | Criar sala, convidar e entrar pelo convite. Sem IP, sem abrir porta. |
| 🗺️ **Mesmo mundo** | Mesmo mapa, criaturas, baús e portas para os dois, mesmo com classes diferentes. |
| ⚖️ **Regras do host** | Dificuldade (História/Normal/Pesadelo), caminho (Sobrevivente/Explorador) e Halloween são do host. |
| 💾 **Continuar em co-op** | O host envia o mundo salvo; o amigo entra com o próprio herói e itens. |
| 📶 **Ping** | Mostrado embaixo do painel (verde/amarelo/vermelho). |
| 🧭 **Seta do amigo** | Quando ele sai da tela, uma seta na borda mostra a direção e a distância ("no abismo" se ele caiu). |

#### 🧍 O outro jogador
| | O que faz |
|---|---|
| 👤 **Herói do amigo no seu jogo** | Animado, com a classe, cabelo, armadura e armas **dele** (inclusive sem armadura). |
| 🏷️ **Nome** | Aparece sobre o herói (vem da Steam). |

#### 🕷️ Monstros
| | O que faz |
|---|---|
| 🤝 **Criaturas compartilhadas** | Cada criatura é controlada pelo jogador mais próximo; no jogo do outro ela repete os movimentos e ataques. |
| ⚔️ **Golpes e mortes** | Golpe numa criatura vale para os dois; matou num jogo, morre no outro. |
| 💪 **Vida dos monstros** | O host escolhe **normal, +50%, +75% ou +100%** de vida. Com dois heróis o jogo fica fácil demais — assim volta o desafio. O bônus aparece no painel do jogo. |
| 🎯 **Quem está caído não é alvo** | Se um cai, as criaturas vão atrás de quem está de pé. |

#### ❤️ Morte, abismo e descanso
| | O que faz |
|---|---|
| 🩸 **Caído e reviver** | Se você morre com o amigo de pé, **fica caído** (sem fim de jogo). O amigo chega perto e **segura E por 5 s**: você levanta com 15% da vida e sem veneno/sangramento. **Os dois caídos = fim de jogo.** |
| 🕳️ **Queda no abismo** | Quem cai no abismo **volta quando o outro descansar numa cama** (qualquer descanso, inclusive com pesadelo), ao lado dele. |
| 🎒 **Abismo: com ou sem itens** | O host escolhe no painel: **"mantém itens"** (volta com tudo) ou **"perde tudo"** (volta sem nenhum item, como a morte no abismo do jogo, mas vivo). |
| 🛏️ **Dormir juntos** | O descanso termina quando **os dois** confirmam *Terminar de descansar*; aí o dia passa e os dois jogos salvam. |
| 🚪 **Descansar sozinho** | Esperando o amigo na cama? Aperte *Terminar de descansar* **de novo** para sair sozinho. |
| ⏱️ **Sem bloqueio de "descansei há pouco"** | Se o amigo está esperando na cama ou caiu no abismo, você pode deitar mesmo com descanso recente. |

#### 📜 Achados compartilhados
| | O que faz |
|---|---|
| 🪨 **Tabuletas** | Um lê, o **poema vai para o diário dos dois** e a tabuleta fica lida para os dois. |
| 📚 **Itens únicos** | **Livros, anéis, amuletos, chaves, artefatos, diários, livros de conhecimento e notas de missão**: um pega, o item some do mapa e **aparece no inventário dos dois**. Usou/consumiu, **some dos dois**. |
| 🗡️ **Itens individuais** | Armas, armaduras e consumíveis (bandagens, garrafas, ferramentas…) são de cada um. |
| 🚪 **Portas e trancas** | Abrir, fechar ou destrancar (chave, gazua ou ação) vale para os dois. No início da fase as trancas são igualadas às do host. |

#### 🌐 Outros
| | O que faz |
|---|---|
| 🇧🇷/🇺🇸 **Idioma** | Textos do mod em português ou inglês, seguindo o idioma do jogo; botão **PT-BR / EN** no painel. |

### ⚙️ Configuração

Arquivo `BepInEx\config\local.sparkcoop.cfg` (criado na primeira vez que o jogo abre com o mod).
Os itens marcados com **(host)** são decididos pelo host e enviados ao amigo.

| Chave | Padrão | Descrição |
|---|---|---|
| `[Coop] MonsterHealthBonus` | `0` | Vida extra dos monstros em %: `0` · `50` · `75` · `100` **(host)** |
| `[Coop] AbyssLoseItems` | `false` | `true` = quem cai no abismo volta sem itens **(host)** |
| `[Coop] Language` | `auto` | `auto` (segue o jogo) · `pt` · `en` |
| `[Coop] PlayerName` | nome Steam | Nome mostrado sobre o seu herói |
| `[Coop] ShowHud` | `true` | Mostra o painel de status |
| `[Coop] Role` | `off` | `off` (sala Steam) · `host` · `client` (conexão direta, rede local/VPN) |
| `[Coop] HostAddress` / `Port` | `127.0.0.1` / `27450` | Conexão direta (UDP) |

**Opções de inicialização** (Steam → Propriedades → *Opções de inicialização*): `-coop-role host|client`,
`-coop-host <IP>`, `-coop-port <porta>`, `-coop-vida-monstros <0|50|75|100>`, `-coop-janela 1920x1080` (força a janela
nesse tamanho), `-coop-nocloud 1` (não mexe na Steam Cloud), `-coop-nosave 1` (não grava saves).

### 🧪 Situação

Testado com duas cópias do jogo no mesmo PC. **O convite entre duas contas Steam diferentes ainda precisa de testes reais
— é aí que vocês entram!** Recém-adicionado na 0.6.0 e ainda não testado em partida: trancas igualadas com classes
diferentes, retorno do abismo após pesadelo e ataques de criaturas em todas as camadas de animação.

**Limitações conhecidas**
- Baús e corpos não são compartilhados: armas/armaduras/consumíveis podem ser pegos pelos dois.
- No *continuar*, o amigo recebe o **último save** do host (o que o host fez depois de salvar não vai junto).
- Zumbis deitados às vezes se movem sem levantar no jogo de quem não os despertou.
- 2 jogadores por enquanto (4 está nos planos).

**Encontrou um bug?** Abra uma *issue* com os arquivos `BepInEx\SparkCoop-*.log` e `BepInEx\LogOutput.log` **dos dois PCs**
e conte o que aconteceu.

### 🛠️ Para desenvolvedores

```
reimpl/SparkCoop/   código do mod (C# 5) + build.ps1 + instalar.ps1 + LEIA-ME.md (detalhes técnicos)
ferramentas/        scripts de teste: duas instâncias, comandos, capturas, janelas lado a lado, dump de tipos, xref por IL
notas/              investigação, mapa da arquitetura do jogo e plano/tabela de testes
```

Compilar (Windows, sem SDK .NET — usa o `csc.exe` do .NET Framework 4 e as DLLs de uma instalação do jogo com BepInEx 6):

```powershell
powershell -ExecutionPolicy Bypass -File reimpl\SparkCoop\build.ps1 -Jogo "C:\Program Files (x86)\Steam\steamapps\common\Spark in the Dark"
```

---

## 🇺🇸 English

### 💬 About

I'm a Brazilian developer and I really like *Spark in the Dark*. One thing was missing: **playing with a friend**.
The game is single-player only, so I built the co-op mode myself.

SparkCoop was developed together with **[Claude Code](https://claude.com/claude-code)**, using **reverse engineering**
to understand how the game works under the hood (map, creatures, death, resting, items) without its source code, and
tested piece by piece with two copies of the game running side by side.

### 📦 Installation (2 minutes)

1. Download **`SparkCoop-x.y.z.zip`** from the **[Releases](https://github.com/Celsob2/sitd-coop/releases)** page (BepInEx 6 included).
2. In Steam: right-click *Spark in the Dark* → **Manage** → **Browse local files**.
3. **Back up your saves:** copy `%USERPROFILE%\AppData\LocalLow\Stellar Fish\Spark in the Dark` somewhere safe.
4. Extract **the contents** of the zip into that folder: `winhttp.dll` goes **next to** `Spark in the Dark.exe`.
5. Start the game **through Steam**. A **SparkCoop** panel shows up at the top right of the main menu. ✅

> [!IMPORTANT]
> Both players need the **same SparkCoop version**. Different versions will not connect.
> No panel on the menu? Your antivirus may have removed `winhttp.dll` — allow it and extract again.

**Uninstall:** delete `winhttp.dll`, `doorstop_config.ini` and the `BepInEx` folder from the game folder. Your saves stay.

### 🕹️ How to play

**New game**
1. **Host:** on the menu panel pick the **monster health** and the **abyss fall** rule, click **Co-op: create lobby** →
   **Invite friend** and pick your friend on Steam.
2. **Friend:** accept the invite (or *Join Game* from the friends list). If the game is closed, Steam starts it and joins.
3. Wait for **"connected: yes"** on both panels.
4. **Host:** *New Game* → pick difficulty and class → start.
5. **Friend:** *New Game* → pick a class (difficulty is locked to the host's) → start **after** the host.
6. The top-left panel should show **`world: SAME`**.

**Continue another day**
1. **Host:** *Continue* → their save.
2. **Friend:** wait for **"Host world received"** on the panel, then click *Continue* → their hero **once**.
   The mod creates `coop_<save>.save` (host's world + friend's hero). The friend's solo save is not changed.

### ✨ Features

#### 🌍 World and connection
| | What it does |
|---|---|
| 🎮 **Steam lobby** | Create a lobby, invite and join from the invite. No IP, no port forwarding. |
| 🗺️ **Same world** | Same map, creatures, chests and doors for both players, even with different classes. |
| ⚖️ **Host rules** | Difficulty (Story/Normal/Nightmare), path (Survivor/Explorer) and Halloween come from the host. |
| 💾 **Co-op continue** | The host sends the saved world; the friend joins with their own hero and items. |
| 📶 **Ping** | Shown below the panel (green/yellow/red). |
| 🧭 **Friend arrow** | When your friend is off-screen, an arrow at the edge shows direction and distance ("in the abyss" if they fell). |

#### 🧍 The other player
| | What it does |
|---|---|
| 👤 **Your friend in your game** | Animated, with **their** class, hair, armor and weapons (also when they have no armor). |
| 🏷️ **Name** | Shown above the hero (from Steam). |

#### 🕷️ Monsters
| | What it does |
|---|---|
| 🤝 **Shared creatures** | Each creature is driven by the nearest player; in the other game it mirrors moves and attacks. |
| ⚔️ **Hits and kills** | A hit counts for both; killed in one game, dead in the other. |
| 💪 **Monster health** | The host picks **normal, +50%, +75% or +100%** health. Two heroes make the game too easy — this brings the challenge back. The bonus is shown on the in-game panel. |
| 🎯 **Downed players aren't targets** | If one player goes down, creatures go after whoever is standing. |

#### ❤️ Death, abyss and resting
| | What it does |
|---|---|
| 🩸 **Downed and revive** | If you die while your friend is standing, you're **downed** (no game over). Your friend comes close and **holds E for 5 s**: you get up with 15% health, poison/bleeding cleared. **Both down = game over.** |
| 🕳️ **Abyss fall** | Whoever falls into the abyss **comes back when the other player rests in a bed** (any rest, nightmares included), right next to them. |
| 🎒 **Abyss: with or without items** | The host picks on the panel: **"keep items"** (you come back with everything) or **"lose all"** (you come back with nothing, like the game's abyss death, but alive). |
| 🛏️ **Sleeping together** | Resting ends when **both** confirm *End rest*; then the day passes and both games save. |
| 🚪 **Rest alone** | Waiting for your friend in bed? Press *End rest* **again** to leave alone. |
| ⏱️ **No "rested recently" lock** | If your friend is waiting in bed or fell into the abyss, you can lie down even with a recent rest. |

#### 📜 Shared discoveries
| | What it does |
|---|---|
| 🪨 **Stone tablets** | One player reads it, the **poem goes into both journals** and the tablet counts as read for both. |
| 📚 **Unique items** | **Books, rings, amulets, keys, artifacts, diaries, knowledge books and quest notes**: one player picks it up, it disappears from the map and **shows up in both inventories**. Used/consumed, **gone for both**. |
| 🗡️ **Individual items** | Weapons, armor and consumables (bandages, bottles, tools…) belong to each player. |
| 🚪 **Doors and locks** | Opening, closing or unlocking (key, lockpick or action) applies to both. At level start, locks are matched to the host's. |

#### 🌐 Other
| | What it does |
|---|---|
| 🇺🇸/🇧🇷 **Language** | Mod texts in English or Portuguese, following the game's language; **PT-BR / EN** button on the panel. |

### ⚙️ Configuration

File `BepInEx\config\local.sparkcoop.cfg` (created the first time the game starts with the mod).
Items marked **(host)** are decided by the host and sent to the friend.

| Key | Default | Description |
|---|---|---|
| `[Coop] MonsterHealthBonus` | `0` | Extra monster health in %: `0` · `50` · `75` · `100` **(host)** |
| `[Coop] AbyssLoseItems` | `false` | `true` = abyss fall brings you back with no items **(host)** |
| `[Coop] Language` | `auto` | `auto` (follows the game) · `pt` · `en` |
| `[Coop] PlayerName` | Steam name | Name shown above your hero |
| `[Coop] ShowHud` | `true` | Shows the status panel |
| `[Coop] Role` | `off` | `off` (Steam lobby) · `host` · `client` (direct connection, LAN/VPN) |
| `[Coop] HostAddress` / `Port` | `127.0.0.1` / `27450` | Direct connection (UDP) |

**Launch options** (Steam → Properties → *Launch options*): `-coop-role host|client`, `-coop-host <IP>`,
`-coop-port <port>`, `-coop-vida-monstros <0|50|75|100>`, `-coop-janela 1920x1080` (forces that window size),
`-coop-nocloud 1` (leaves Steam Cloud alone), `-coop-nosave 1` (no saving).

### 🧪 Status

Tested with two copies of the game on one PC. **Inviting between two different Steam accounts still needs real-world
testing — that's where you come in!** New in 0.6.0 and not yet tested in a real session: lock matching with different
classes, abyss return after a nightmare, and creature attacks on every animation layer.

**Known limitations**
- Chests and corpses are not shared: weapons/armor/consumables can be taken by both players.
- On *continue*, the friend gets the host's **last save** (what the host did after saving is not sent).
- Lying zombies sometimes move without standing up in the game of the player who didn't wake them.
- 2 players for now (4 is planned).

**Found a bug?** Open an *issue* with `BepInEx\SparkCoop-*.log` and `BepInEx\LogOutput.log` **from both PCs** and
describe what happened.

### 🛠️ For developers

```
reimpl/SparkCoop/   mod source (C# 5) + build.ps1 + instalar.ps1 + LEIA-ME.md (technical details, Portuguese)
ferramentas/        test scripts: two instances, commands, screenshots, side-by-side windows, type dump, IL xref
notas/              investigation, game architecture map and plan/test table
```

Build (Windows, no .NET SDK — uses the .NET Framework 4 `csc.exe` and the DLLs from a game install with BepInEx 6):

```powershell
powershell -ExecutionPolicy Bypass -File reimpl\SparkCoop\build.ps1 -Jogo "C:\Program Files (x86)\Steam\steamapps\common\Spark in the Dark"
```

---

<div align="center">

### 📜 Licença / License

Copyright (C) 2026 Celso Barbalho — **[GNU GPL v3.0](LICENSE)** ou posterior / or later.

Créditos / credits: [BepInEx](https://github.com/BepInEx/BepInEx) · [HarmonyX](https://github.com/BepInEx/HarmonyX) ·
[Unity Doorstop](https://github.com/NeighTools/UnityDoorstop) · [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET)

Feito com ❤️ no Brasil 🇧🇷 · Made with ❤️ in Brazil

</div>
