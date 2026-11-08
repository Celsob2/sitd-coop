# SparkCoop

**Co-op para 2 jogadores em [Spark in the Dark](https://store.steampowered.com/app/1315180/) — mod BepInEx 6 (Unity Mono).**
**2-player co-op for Spark in the Dark — BepInEx 6 (Unity Mono) mod.**

[Português](#português) · [English](#english)

> Mod experimental e não oficial, sem ligação com os desenvolvedores do jogo. Este repositório tem só código próprio;
> nenhum arquivo do jogo é distribuído. Cada jogador precisa da sua própria cópia do jogo na Steam.
>
> Experimental, unofficial mod, not affiliated with the game's developers. This repository contains only original code;
> no game files are distributed. Each player needs their own copy of the game on Steam.

---

## Português

### O que faz

- **Sala pela Steam:** o host cria a sala e convida um amigo pelo overlay da Steam; sem IP e sem abrir porta
  (`SteamNetworkingMessages`). Também há conexão direta por UDP para testes em rede local.
- **Mesmo mundo:** os dois jogos geram o mesmo mapa e as mesmas criaturas a partir da semente do host, mesmo com
  classes diferentes. Um hash do mundo confirma (`mundo: IGUAL`).
- **O outro jogador aparece no seu jogo** com a classe, a aparência e o equipamento dele, animado.
- **Criaturas com dono:** cada criatura é controlada pelo jogador mais próximo; no outro jogo ela só repete os movimentos.
  Golpes e mortes valem para os dois.
- **Caído e reviver:** se você morre e o amigo está de pé, fica caído; ele segura **E** por 5 s perto de você para te levantar.
  Os dois caídos = fim de jogo normal.
- **Dormir juntos:** o descanso só termina quando os dois confirmam; aí o dia passa e cada jogo salva.
- **Monstros mais fortes:** o host escolhe vida extra dos monstros (+50%, +75% ou +100%).
- **Idioma:** textos do mod em português do Brasil ou inglês (segue o idioma do jogo; botão no painel para trocar).

### Instalar (jogadores)

1. Baixe o `.zip` da página de **Releases** (já vem com o BepInEx 6).
2. Na Steam: botão direito em *Spark in the Dark* → *Gerenciar* → *Ver arquivos locais*.
3. Faça backup dos saves: `%USERPROFILE%\AppData\LocalLow\Stellar Fish\Spark in the Dark`.
4. Extraia o conteúdo do `.zip` dentro da pasta do jogo (`winhttp.dll` ao lado de `Spark in the Dark.exe`).
5. Abra o jogo pela Steam. No menu aparece o painel **SparkCoop** no canto superior direito.

Os dois jogadores precisam da **mesma versão** do mod (o protocolo é conferido ao conectar).

### Jogar

1. Host: **Co-op: criar sala** → **Convidar amigo**.
2. Amigo: aceita o convite na Steam (ou *Entrar no jogo* na lista de amigos). Com o jogo fechado, a Steam abre o jogo e
   ele entra na sala sozinho.
3. Esperem `conectado: sim` e cliquem os dois em **Novo Jogo**.

Conexão direta (sem Steam), em `BepInEx\config\local.sparkcoop.cfg`, seção `[Coop]`: host `Role = host`;
amigo `Role = client` e `HostAddress = <IP do host>` (porta UDP 27450).

### Idioma

`Language = auto | pt | en` em `[Coop]`. `auto` segue o idioma do jogo (português → PT-BR, qualquer outro → inglês).
O botão **PT-BR / EN** no painel do menu troca e grava a escolha. Os logs ficam sempre em português.

### Remover

Apague `BepInEx\plugins\SparkCoop\` (só o mod) ou `winhttp.dll`, `doorstop_config.ini` e a pasta `BepInEx` (mod e BepInEx).

### Compilar

Requisitos: Windows e o `csc.exe` do .NET Framework 4 (já vem no Windows; não precisa de SDK .NET), e uma pasta do jogo
com BepInEx 6 Unity Mono (as DLLs do jogo são usadas só como referência e não ficam no repositório).

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1 -Jogo "C:\Program Files (x86)\Steam\steamapps\common\Spark in the Dark"
powershell -ExecutionPolicy Bypass -File instalar.ps1 -Jogo "<pasta do jogo>"            # copia bin\SparkCoop.dll
powershell -ExecutionPolicy Bypass -File instalar.ps1 -Jogo "<pasta do jogo>" -Remover   # remove o que instalou
```

O código é C# 5 (limite do compilador do .NET Framework). Arquivos:

| Arquivo | Conteúdo |
|---|---|
| `SparkCoop.cs` | plugin, sessão, protocolo (`Msg`), transporte UDP, semente do mundo, fantasma |
| `SparkCoop.Steam.cs` | sala Steam, convite, `SteamTransport` |
| `SparkCoop.Criaturas.cs` | dono das criaturas, cópias, golpes encaminhados |
| `SparkCoop.Vida.cs` | caído/reviver, dormir juntos |
| `SparkCoop.Aparencia.cs` | aparência do outro jogador |
| `SparkCoop.Idioma.cs` | textos em PT-BR / EN |
| `SparkCoop.Dificuldade.cs` | vida extra dos monstros (+0/50/75/100%), escolhida pelo host |
| `SparkCoop.Mundo.cs` | continuar em co-op: host envia o save, amigo junta com o próprio herói |
| `SparkCoop.Portas.cs` | portas sincronizadas (e diagnóstico de golpes) |
| `SparkCoop.Regras.cs` | dificuldade/caminho definidos pelo host |

Mais detalhes, opções de teste e o que já foi testado: [LEIA-ME.md](LEIA-ME.md).

### Situação

Testado com duas instâncias no mesmo PC (conexão direta) e com a sala Steam usando uma conta só.
**O convite entre duas contas Steam ainda precisa de teste real.** Relatos de bugs: abra uma *issue* com os arquivos
`BepInEx\SparkCoop-*.log` e `BepInEx\LogOutput.log` dos dois PCs.

---

## English

### Features

- **Steam lobby:** the host creates a lobby and invites a friend through the Steam overlay; no IP or port forwarding
  (`SteamNetworkingMessages`). A direct UDP connection is also available for LAN testing.
- **Same world:** both games generate the same map and creatures from the host's seed, even with different classes.
  A world hash confirms it (`world: SAME`).
- **The other player shows up in your game**, animated, with their own class, look and gear.
- **Creature ownership:** each creature is driven by the nearest player; in the other game it just mirrors the movement.
  Hits and kills count for both.
- **Downed and revive:** if you die while your friend is standing, you go down; they hold **E** next to you for 5 s to
  revive you. Both down = normal game over.
- **Sleeping together:** resting only ends when both players confirm; then the day passes and each game saves.
- **Tougher monsters:** the host picks extra monster health (+50%, +75% or +100%).
- **Language:** mod texts in Brazilian Portuguese or English (follows the game's language; button on the panel to switch).

### Install (players)

1. Download the `.zip` from the **Releases** page (BepInEx 6 included).
2. In Steam: right-click *Spark in the Dark* → *Manage* → *Browse local files*.
3. Back up your saves: `%USERPROFILE%\AppData\LocalLow\Stellar Fish\Spark in the Dark`.
4. Extract the `.zip` contents into the game folder (`winhttp.dll` next to `Spark in the Dark.exe`).
5. Start the game through Steam. A **SparkCoop** panel appears at the top right of the main menu.

Both players need the **same mod version** (the protocol is checked on connect).

### Play

1. Host: **Co-op: create lobby** → **Invite friend**.
2. Friend: accepts the invite in Steam (or *Join Game* from the friends list). If the game is closed, Steam starts it and
   joins the lobby automatically.
3. Wait for `connected: yes`, then both click **New Game**.

Direct connection (no Steam), in `BepInEx\config\local.sparkcoop.cfg`, section `[Coop]`: host `Role = host`;
friend `Role = client` and `HostAddress = <host IP>` (UDP port 27450).

### Language

`Language = auto | pt | en` under `[Coop]`. `auto` follows the game's language (Portuguese → PT-BR, anything else →
English). The **PT-BR / EN** button on the menu panel switches and saves the choice. Logs are always in Portuguese.

### Uninstall

Delete `BepInEx\plugins\SparkCoop\` (mod only) or `winhttp.dll`, `doorstop_config.ini` and the `BepInEx` folder
(mod and BepInEx).

### Build

Requirements: Windows with the .NET Framework 4 `csc.exe` (ships with Windows; no .NET SDK needed), and a game folder with
BepInEx 6 Unity Mono (the game's DLLs are only referenced and are not part of this repository).

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1 -Jogo "C:\Program Files (x86)\Steam\steamapps\common\Spark in the Dark"
powershell -ExecutionPolicy Bypass -File instalar.ps1 -Jogo "<game folder>"            # copies bin\SparkCoop.dll
powershell -ExecutionPolicy Bypass -File instalar.ps1 -Jogo "<game folder>" -Remover   # removes what it installed
```

The code is C# 5 (limit of the .NET Framework compiler); see the file table in the Portuguese section above.
Code comments and detailed docs ([LEIA-ME.md](LEIA-ME.md)) are in Portuguese.

### Status

Tested with two instances on one PC (direct connection) and with the Steam lobby using a single account.
**Inviting between two different Steam accounts still needs a real-world test.** Bug reports: open an *issue* with
`BepInEx\SparkCoop-*.log` and `BepInEx\LogOutput.log` from both PCs.

---

## Licença / License

Copyright (C) 2026 Celso Barbalho.

SparkCoop é software livre, distribuído sob a **GNU General Public License v3.0 ou posterior** — veja [LICENSE](LICENSE).
Você pode usar, estudar, modificar e redistribuir, desde que versões distribuídas continuem sob a GPL e com o código-fonte.

SparkCoop is free software released under the **GNU General Public License v3.0 or later** — see [LICENSE](LICENSE).

Créditos / credits: [BepInEx](https://github.com/BepInEx/BepInEx), [HarmonyX](https://github.com/BepInEx/HarmonyX),
[Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) (já incluído no jogo / shipped with the game).
