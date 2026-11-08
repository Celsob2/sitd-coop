# Mapa de arquitetura — Spark in the Dark (subsistemas relevantes para co-op)

Investigação estática por IL (Mono.Cecil, scripts em `ferramentas/`), sem decompilar para C# e sem executar o
alvo durante a análise. Depois, observação dinâmica só na **cópia de trabalho** (`trabalho/`), com o mod.

Legenda: **[O]** observado · **[D]** derivado · **[I]** inferido · **[?]** desconhecido.

## Identidade do alvo

| Item | Valor |
| --- | --- |
| Instalação | `C:\Program Files (x86)\Steam\steamapps\common\Spark in the Dark` (Steam AppID 1315180) |
| Editora/produto (`app.info`) | Stellar Fish / Spark in the Dark |
| Engine | Unity **2022.3.62f3**, scripting **Mono** (não IL2CPP) [O: `triagem.md`, log do BepInEx] |
| Lógica do jogo | `Spark in the Dark_Data\Managed\Assembly-CSharp.dll` — 1.454.080 bytes, SHA-256 `d086fa00699a7cc6f88291bb67cf184922b09e98c41629541f1318149854215b` [O] |
| Rede embutida | **nenhuma** (sem Mirror/Netcode/Photon; só Steamworks.NET e GOG Galaxy para conquistas/nuvem) [O: lista de assemblies] |
| Proteções | nenhum marcador de DRM/anti-cheat; Steamworks é integração, não DRM [O: triagem] |
| Manifesto | `manifesto-original.json`, 548 arquivos, 6,7 GiB; reverificado no fim: **548 idênticos, 0 alterados** [O] |

### Estado inicial da instalação (já modificada antes deste trabalho)

[O] A pasta do jogo **já tinha** mods instalados por outra pessoa/ferramenta:

- **BepInEx 6** (build de desenvolvimento, "0.0.0", Unity Mono) via Doorstop (`winhttp.dll`, `doorstop_config.ini`).
- **`BepInEx\plugins\xmod\`**: `ModFramework.dll` (10,5 MB) + `SparkInTheDark.dll` + locales `zh-CN`/`en`. Configuração em
  chinês; abre named pipes `\\.\pipe\ModFrameworkPipe(-json)` e lança `grpc\ExternalGrpcHost.exe` (ASP.NET Core 8 / gRPC,
  uma pasta `grpc\` inteira de runtime .NET 8). Aparenta ser um framework de trainer/painel de cheats controlado por
  processo externo [I: nomes, config e log; o código não foi lido].
- **`DWrite.dll`** na raiz do jogo, com metadados "BG3 Script Extender Auto-Updater DLL / Norbyte" (do Baldur's Gate 3).
  Por estar com o nome de uma DLL do sistema na pasta do executável, **é carregada pelo jogo** (DLL proxy). Não faz sentido
  num jogo Unity; pode ser sobra de outra instalação ou algo indesejado [I]. **Recomendo verificar a origem** (ver
  "Alertas" no plano). Na cópia de trabalho ela e o `xmod` foram movidos para `trabalho\_desativados-na-copia\`.

## Camadas

```
Spark in the Dark.exe → UnityPlayer.dll → Mono → Assembly-CSharp.dll (toda a lógica)
                                             ↳ BepInEx (Doorstop) → plugins (xmod, SparkCoop)
Cenas: StartMenu → CharacterSelection → Level1..Level4 (level2-4 têm 0,7–1 GB cada)
```

## Subsistemas

### Loop principal e cenas
- Não há "GameLoop" central; cada `MonoBehaviour` tem seu `Update` [O]. Estado global em estáticos de `Game`
  (`LoadingType`, `CharPreset`, `Mode`, `Random`, `IsPaused`...) [O].
- Fluxo de novo jogo: `StartMenu.NewGame()` → cena `CharacterSelection` → `CharacterSelection.Go()` grava
  `Game.CharPreset`, `Game.LoadingType = NewGame (1)`, zera `SaveGameSystem.CurrentSaveFile` → tela
  `NewGameLoadingScreen` (espera espaço) → `Level1` [O: IL de `Go`, `NewGame`, `LoadCharacterSelection`].
- `LevelManager.OnLevelFinishedLoading` → `LoadFromSave()` **ou** `LevelGeneration()` conforme `Game.LoadingType` [O].
- Próximo nível: `LevelManager.LoadNextLevel("Level" + (n+1))`, `LoadingType = NextLevel (3)` [O].

### Geração procedural (crítico para co-op)
- `LevelManager.LevelGeneration()` → `InitCharacter` → `Generator.Instance.Generate()` (1.693 bytes de IL) que
  cria população de criaturas, itens, armadilhas, cordas/grades aleatórias, livros etc. [O].
- Aleatoriedade: `UnityEngine.Random` (79 usos em 50 métodos) e `Game.Random` (`System.Random` estático
  **readonly**, criado sem semente em `Game..cctor`) [O]. **Nenhum `Random.InitState` no jogo** [O: xref] →
  cada partida é diferente.
- [D→O] Semeando ambos antes de `LevelGeneration`, duas instâncias geraram o **mesmo mundo** (246 criaturas,
  mesmo hash) em 4 de 4 execuções.
- IDs de objetos dinâmicos vêm de `Guid.NewGuid()` em `SaveableObject.GenerateUniqueID` [O] → não servem como ID de
  rede; o mod usa chave própria (família/rank/posição pós-geração).

### Estado do jogador
- `MainChar` (MonoBehaviour, singleton `MainChar.Instance`) concentra corpo, mãos, inventário, buffs, luz, áudio,
  ações e entrada [O: 150+ campos, ~330 métodos].
- **Suposição de jogador único é massiva**: `MainChar::Instance` é lido em **517 métodos de 223 tipos**
  (1.782 ocorrências) — UI, criaturas, NPCs, gerador, inventário, saves [O: `xref.ps1`].
- Animação: `Animator` na raiz do herói, controller `MainCharAnimatorController`, 7 camadas, 82 parâmetros [O: runtime].

### Entrada
- Unity **Input System** via `PlayerInput`/mensagens: `MainChar.OnMove(InputValue)`, `OnAttack()`, `OnRun`, `OnJump`,
  `OnSneak`, `OnDoAction`, `OnChangeWeapon`... [O: assinaturas]. `GamepadController` (singleton) só gerencia
  bindings/vibração [O]. Ponto único injetável: as callbacks `On*` do `MainChar` [I].

### Inimigos / IA
- Base `Creatures.Creature` (NavMeshAgent + Animator), subclasses por espécie (`RatController`, `SpiderController`,
  `CorpseController`...). Alvo em `EnemyCharacter : CharacterBase`, definido por `SetEnemy(CharacterBase)` [O].
- Visão/audição comparam com o herói (`IsTargetDetectedInVisionSector(CharacterBase)`, `PlayerAvailabilityCalculation`)
  e `Creature` lê `MainChar.Instance` em 8 métodos [O].
- Morte: `Creature.Death(isPassiveDeath, isSilent, skipDeathAnimation)` — **retorna sem efeito** se o Animator não tem o
  parâmetro `Dead` ou se `Character.IsDead`; senão marca `IsDead`, chama `DeathCreature()` (virtual), `Character.Dead()`
  e corrotina `CallingDeath` [O: IL].
- [O: runtime] Chamar `Death` diretamente funcionou para aranhas, mas **não** para a ratazana testada (`IsDead` ficou
  falso): as ratazanas devem morrer por outro caminho/parâmetro [?].

### Câmera
- Câmera de cima/isométrica seguindo o herói; componentes auxiliares `CameraAdjuster` (FOV por gatilhos),
  `RestCamera`, `TransitionWayCamera`, `TeralixorCamera` [O]. Uma câmera por instância — sem split-screen [I].

### Salvamento
- `SaveGameSystem` (estático): `SaveGame`, `LoadGame`, `GetLevelData/ApplyLevelData`, `GetPlayerData/ApplyPlayerData` [O].
- Pasta: `Application.persistentDataPath\saves` = `%USERPROFILE%\AppData\LocalLow\Stellar Fish\Spark in the Dark\saves`
  [O: IL de `GetSaveFolderPath` + disco]. Saves cifrados (AES + HMAC, `EncryptSavePayload`) e sincronizados com a Steam
  Cloud (`SyncLocalAndCloudSaveFiles`) [O: nomes]. A cifra **não foi estudada nem contornada**: não é necessária,
  porque o mod usaria as APIs em memória do próprio jogo.
- Metaprogresso fora do save: `spark_in_the_dark.heroes`, `.creatures`, `.achievements`, `.settings` [O].
- `SaveGameManager` registra todo `SaveableObject` por ID → snapshot completo do mundo existe [O].

### Pausa
- `Game.SetGamePause` / `FreezeCreatures` [O]; menus pausam a instância local [I]. Em co-op, pausas não são sincronizadas.

## Cobertura e lacunas
- Lido em IL: `LevelManager` (Start, LevelGeneration, OnLevelFinishedLoading, LoadFromSave, LoadNextLevel),
  `Creature.Death`, `CharacterSelection.Go/OnStartGame`, `StartMenu.NewGame`, `Game..cctor`, `SaveGameSystem.GetSaveFolderPath`.
- Só assinaturas: combate do herói (`MainChar.OnAttack`, `Damaged`), IA de criaturas, inventário, NPCs.
- Não lidos: `xmod/ModFramework.dll`, `DWrite.dll`, `ExternalGrpcHost`.
