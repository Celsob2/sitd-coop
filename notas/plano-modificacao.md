# Plano de modificação: Spark in the Dark — co-op online (mod SparkCoop)

Evidências da arquitetura: [mapa.md](mapa.md). Triagem: [triagem.md](triagem.md).

## Objetivo

- Resultado esperado: dois jogadores, cada um com sua cópia, explorando **a mesma masmorra** e vendo um ao outro.
- Critério de sucesso observável do protótipo: (1) as duas instâncias geram o mesmo mundo (hash igual);
  (2) cada uma mostra o herói da outra na posição certa; (3) uma criatura morta por um jogador morre no jogo do outro.
- Fora do escopo do protótipo: inimigos perseguirem/atacarem o outro jogador, inventário/itens compartilhados,
  NPCs/quests, saves compartilhados, split-screen.

## Precondições

| Item | Valor |
| --- | --- |
| Build suportado | `Assembly-CSharp.dll` SHA-256 `d086fa00…854215b` (Unity 2022.3.62f3, Steam) |
| Plataforma | Windows x64 nativo |
| Engine | Unity Mono → mods em C# com Harmony, sem patch de binário |
| Proteções | nenhuma detectada; o mod não toca na cifra dos saves nem na Steam |
| Loader | **BepInEx 6 (Unity Mono)** — já presente na instalação; o mod só adiciona `BepInEx\plugins\SparkCoop\` |
| Backup | `backup\dados-do-usuario-antes-do-teste\` (saves + metaprogresso); `backup\copia-trabalho\boot.config` |

## Passo zero: já existe co-op?

Busca na web (31/10/2026) não encontrou mod de co-op nem multiplayer oficial para este jogo. Resultado limitado a
uma busca: vale perguntar no Discord/Steam do jogo antes de investir nos próximos marcos.

## Viabilidade

| Pergunta | Resposta | Evidência | Status |
| --- | --- | --- | --- |
| Onde fica o estado autoritativo? | Tudo local por instância; herói em `MainChar`, criaturas em `Creature`, mundo em `SaveableObject`s | mapa §Estado/§Salvamento | respondida |
| Há determinismo? | RNG não semeado, mas **semeável**: com semente igual o mundo sai igual | 4/4 testes, hash igual | respondida (nível 1, mesma classe) |
| Entrada por ponto único? | Callbacks `MainChar.On*` do Input System | assinaturas | parcial |
| Segundo jogador ou fantoche? | Segundo `MainChar` real é inviável a curto prazo (517 métodos assumem um só); **fantoche visual** funciona | xref; teste visual | respondida |
| Menus pausam? | Pausa local (`Game.SetGamePause`) | nomes | parcial |
| Suposições de jogador único | Singleton `MainChar.Instance`, câmera única, IA mira o herói local, UI | xref 223 tipos | respondida |
| Save serializa o estado? | Sim (`GetLevelData`/`ApplyLevelData`, registro por ID) → snapshot para "entrar no meio" é possível | IL/nomes | parcial |
| Anti-cheat / online oficial? | Não | triagem | respondida |

**Modelo escolhido: "mundos espelhados + fantoches"** — cada jogador roda o jogo inteiro localmente; o mod garante
que os mundos nasçam iguais (semente do host) e replica eventos (presença, mortes). É o único modelo que não exige
reescrever o singleton `MainChar`, e já entrega a sensação de co-op. Evolução prevista: host autoritativo sobre as
criaturas (marco M3).

## Pontos de intervenção (implementados)

| Ponto | Técnica | Efeito | Evidência |
| --- | --- | --- | --- |
| `Management.LevelManager.LevelGeneration` (prefixo) | Harmony | `UnityEngine.Random.InitState(s)` e `Game.Random = new System.Random(s)` com `s = semente ^ (nível*7919)` | gerador usa os dois RNGs; nenhum `InitState` no jogo |
| `LevelManager.LevelGeneration` (pós-fixo) | Harmony | registra criaturas com chave `família/rank@x,y,z` e calcula hash FNV do mundo | `Guid.NewGuid` nos IDs do jogo |
| `Creatures.Creature.Death` (pós-fixo) | Harmony | se `IsDead` ficou verdadeiro, envia a chave da criatura | IL de `Death` (retornos antecipados) |
| `SaveGameSystem.SaveGame`, `SyncLocalAndCloudSaveFiles` (prefixo) | Harmony | **só no modo de teste** (`-coop-nosave 1`): não grava nem sincroniza | proteger saves e Steam Cloud nos testes |
| Fantasma | Clone do `MainChar` instanciado sob pai **inativo** (evita `Awake` sobrescrever `MainChar.Instance`), sem scripts/colisores/áudio, com `Animator` + luz própria | presença visual | runtime: 106 renderers, controller com 7 camadas |

## Rede e sincronização

- Transporte: **UDP** puro (`System.Net.Sockets`), porta padrão **27450**, IP direto. Sem dependências externas.
- Handshake: `HELLO` (cliente, versão de protocolo, nome) → `WELCOME` (host, nome, **semente**). Cliente reenvia HELLO a cada 1 s até receber.
- `STATE` a **20 Hz** nos dois sentidos: cena, hash do mundo, posição/rotação do herói, 82 parâmetros do Animator,
  estado/tempo/peso de até 8 camadas, e as últimas 32 mortes (idempotente: perda de pacote não perde mortes).
- Fantasma: interpolação (lerp 12/s; teleporta se > 5 m), `CrossFade` quando o estado da camada muda.
- Morte remota: `Death(isPassiveDeath: true)` com guarda anti-eco; se a criatura estiver inativa, fica pendente e é reaplicada.
- Autoridade: cada jogador é autoridade do próprio herói e das mortes que causa. IA de criaturas continua local.

## Marcos

| Marco | Conteúdo | Status |
| --- | --- | --- |
| M0 | Triagem, mapa, workspace, manifesto, backups | **feito** |
| M1 | Conexão UDP, semente compartilhada, mundo idêntico verificado por hash | **feito e testado** |
| M2 | Fantasma do outro jogador (posição + animação), mortes de criaturas replicadas | **feito e testado**; T-pose corrigida na v0.1.1 |
| M3 | Host autoritativo das criaturas: host envia posição/alvo/vida das criaturas próximas; cliente vira fantoche; criaturas passam a mirar o jogador mais próximo (`SetEnemy` com o `CharacterBase` do fantasma) | a fazer |
| M4 | Itens/containers/portas sincronizados por evento (abrir, pegar, destrancar) usando os IDs do `SaveGameManager` | a fazer |
| M5 | Entrar no meio da partida via `GetLevelData` do host; troca de nível em conjunto; UI de conectar | a fazer |

## Testes executados (31/10/2026, duas instâncias na mesma máquina, cópia de trabalho)

| Cenário | Esperado | Observado | Status |
| --- | --- | --- | --- |
| Conexão | cliente recebe semente | `conectado ao host; semente = …` em < 1 s | ok |
| Mundo igual | hash igual | `246 criaturas`, hash igual nos dois lados (9f477210, e142c54b, 370159a4, fd74e552, 11a9af25) — 5/5 | ok |
| Fantasma | herói remoto visível e na posição | captura `testes\capturas\final-*.png`: dois heróis em cada janela, rótulo "Celso"; posição remota seguiu teleporte (−100,60 → −99,10) | ok |
| Morte host → cliente | criatura morre no cliente | aranha: aplicada em 28 ms; ratazana: ver abaixo | ok (aranha) |
| Morte cliente → host | criatura morre no host | aranha `spider/0@-153,0,-213` aplicada em 16 ms | ok |
| Morte de ratazana por chamada direta | `IsDead` | **não morreu nem localmente** (retorno antecipado de `Death`) → o mod corretamente não enviou | limitação |
| Saves | nada gravado no teste | 0 saves alterados; `.heroes`/`.settings` mudaram e foram restaurados do backup | ok |
| Original intacto | manifesto idêntico | `548 idênticos, 0 alterados` | ok |

### Correção da animação do fantasma (v0.1.1, 31/10/2026)

| Cenário | Esperado | Observado | Status |
| --- | --- | --- | --- |
| Causa da T-pose | — | O `Animator` do clone se ligava ao esqueleto **de dentro da armadura** (`Armor/.../Armor_DungeonGuard_P_Skin/Base/Hips`), que tem os mesmos nomes de ossos; as malhas visíveis usam `Bones/Base`. No herói local o Animator usa `Player/Bones/Base/Hips`. | diagnosticado |
| Correção | Animator no esqueleto principal | Esqueletos duplicados sem malha visível são renomeados antes de ativar o clone (97 ossos); Animator passa a `SparkCoop_Ghost/Bones/Base/Hips` e o osso `Spine1` muda de rotação com o tempo | ok |
| Andar | fantasma anda com animação | host andou de verdade (via `MainChar.x/y`, mesma entrada do controle); captura `testes\capturas\volta-zoom-cliente.png` mostra o fantasma no meio da passada | ok |

Não testado: jogo com teclado/controle real por duas pessoas, latência de internet/NAT, níveis 2–4, classes diferentes
entre os jogadores, combate real (as mortes de teste foram chamadas diretas a `Death`).

## Limitações conhecidas do protótipo

1. Inimigos **não** veem nem atacam o fantasma; cada um enfrenta as criaturas do seu mundo (só as mortes são compartilhadas).
2. O fantasma usa a aparência e o equipamento do **seu** herói (é um clone local), não os do outro jogador.
3. Os dois precisam começar **jogo novo** com o mod ativo e o cliente conectado **antes** de gerar o nível; jogos carregados
   de save não têm co-op. Classes/progresso diferentes podem desalinhar o RNG — o HUD mostra `mundo: DIFERENTE` nesse caso.
4. Espécies cuja morte não passa por `Creature.Death` (ex.: ratazanas, a confirmar) não sincronizam.
5. Itens, portas, armadilhas, NPCs e troca de nível não são sincronizados.
6. O jogo força instância única (`single-instance=` no `boot.config`); para testar duas instâncias num só PC isso foi
   removido **apenas na cópia de trabalho**. Para jogar de verdade, cada pessoa usa seu PC e nada disso é necessário.
7. Sem criptografia/autenticação no UDP: use só em rede confiável/VPN (Tailscale, ZeroTier, Radmin) ou porta liberada para o amigo.

## Próximas funções — desenho combinado com o usuário (31/10/2026)

Começar com **2 jogadores**; a rede guarda os jogadores numa lista, para chegar a 4 depois sem reescrever.

### 1. Sala e convite pela Steam
- O jogo já traz Steamworks.NET (`com.rlabrecque.steamworks.net.dll`) e o `Management.SteamManager` já inicializa a Steam [O].
- Menu inicial ganha **"Co-op: criar sala"**. O host cria um lobby `SteamMatchmaking.CreateLobby(FriendsOnly, 2)` e abre
  `SteamFriends.ActivateGameOverlayInviteDialog`. O amigo aceita pela Steam (callback `GameLobbyJoinRequested_t`, ou
  `+connect_lobby <id>` quando o jogo é aberto pelo convite).
- O transporte troca o UDP/IP por **`SteamNetworkingMessages`**: sem abrir porta, sem IP e com o NAT resolvido pela Steam.
  O UDP direto fica como alternativa para testes locais.
- Fluxo: host cria sala → escolhe "novo jogo" ou "continuar save co-op" → amigo entra → o host aperta **Iniciar**.
  O amigo escolhe a classe na tela do jogo, e os dois entram juntos.

### 2. Save co-op (o host guarda tudo)
- O save do jogo continua sendo o do host (mundo + herói do host), gravado pelo próprio `SaveGameSystem.SaveGame`.
- Ao lado dele, o mod grava `<save>.coop.json` com os dados de **cada convidado** (por SteamID), obtidos no PC do
  convidado com `SaveGameSystem.GetPlayerData()` (inventário, equipamento, atributos, buffs) e enviados ao host.
- **Continuar** no dia seguinte: o host carrega o save; quando o amigo entra, o host envia o estado do nível
  (`GetLevelData`) e o `PlayerData` dele. O cliente aplica com `ApplyLevelData` e `ApplyPlayerData`. Isso deixa os mundos
  iguais **sem** depender da semente, e permite entrar no meio da partida.
- O convidado também guarda uma cópia local da sua parte, como backup.

### 3. Dormir juntos = salvar — **implementado na v0.2.0** (`reimpl/SparkCoop/SparkCoop.Vida.cs`)
- Fluxo do jogo [O: IL]: botão de sair da cama → `SleepingScreen.HideScreen` (tela preta) → `EndingRest` → `EndRest`, que faz
  `Game.AddHours(6)` e **`SaveGameSystem.SaveGame(true)`**.
- O mod segura `HideScreen` até os dois confirmarem ("Pronto para dormir. Aguardando <amigo>..."); quando o segundo
  confirma, os dois seguem juntos e cada jogo salva. Na cama, cada jogador vê só o próprio herói (o fantasma some).
- Se o amigo está caído ou no abismo, ninguém espera por ele.
- Pendente: save co-op (item 2) — hoje cada um salva o próprio jogo.

### 4. Herói caído e reviver — **implementado na v0.2.0**
- Fluxo do jogo [O: IL]: `MainChar.Death` marca morto, solta as armas e liga a animação `IsDead`; o estado
  `DeathAnimatorState.OnStateEnter` desliga o Animator (corpo congelado), escurece a tela e chama
  `UserInterface.ShowAfterDeathEvent` (apanhador de sonhos / sobreviveu / recarregar save / tela de morte).
- O mod intercepta `DeathAnimatorState.OnStateEnter`: com o amigo de pé, o herói fica **caído** (corpo no chão, sem tela
  preta, invisível para inimigos via `MainChar.IgnoredForEnemy`). O amigo segura **E** (ou X/quadrado no controle) por **5 s**
  perto do corpo; o herói levanta com **15% da vida máxima** e sem debuffs temporários (`RemoveAllDebuffs(true)`), animação
  de volta (`IsDead=false`, estado idle, gatilho `Getup`).
- **Abismo** (decisão do usuário): não dá para reviver no local; o herói volta, com 15% da vida, ao lado do amigo quando ele
  termina um descanso numa cama. Qualquer herói caído também volta assim.
- **Os dois caídos**: fim de jogo normal para os dois.
- Correção importante feita junto: o fantasma usa o controlador de animação do herói, que tem 97 scripts de estado
  (`StateMachineBehaviour`) que agem sobre `MainChar.Instance`. Antes, o fantasma "morrendo" disparava a morte do herói
  local. Agora nenhum desses scripts roda no fantasma.

| Teste (31/10/2026, duas instâncias) | Observado | Status |
| --- | --- | --- |
| Cliente morre com o host de pé | cliente caído, corpo no chão (y=0), host recebe aviso; host **não** cai junto | ok |
| Host segura E 5 s perto do corpo | cliente levanta com 31/208 de vida (15%), vida volta a regenerar, anda normalmente | ok |
| Cliente cai no abismo; host descansa | host não espera; save do host disparado; cliente volta ao lado do host com 15% | ok |
| Os dois descansam | host fica "pronto, aguardando"; quando o cliente confirma, os dois saem e salvam juntos | ok |
| Os dois caem | tela de morte do jogo ("MORRESTE") | ok |

Não testado: morte por criatura/armadilha/veneno de verdade (os testes usaram `MainChar.Death` direto), tecla E com uma
pessoa real, controle, descanso em camas diferentes com duas pessoas, e o caso do apanhador de sonhos.

### 5. Bugs relatados pelo usuário em 31/10/2026 (teste real) — v0.2.0

| Bug | Causa | Correção | Teste |
| --- | --- | --- | --- |
| Fantasma com a classe/equipamento de quem olha | o fantasma era um clone do herói local | cada jogo envia a "receita" do próprio visual (classe, cabelo/barba/bigode/cor, armadura e itens visíveis com o ponto do corpo e as partes ligadas/desligadas); o outro monta com as funções do jogo (`TryResolveDynamicPrefab`, ossos por nome como `SetArmorToSkeleton`, `PlayerHead.SelectHead`, `HeroBodyShape.Apply`) — `SparkCoop.Aparencia.cs` | Guerreiro×Caçador e Guerreiro×Ladino: cada lado vê a classe, armadura, elmo, escudo, espada/besta do outro (`testes\capturas\armadura-zoom-*.png`) |
| Fantasma caído "cai e levanta sem parar" | no herói o jogo congela o corpo desligando o Animator no estado de morte; esse script não roda no fantasma | o fantasma congela igual enquanto o outro está caído e volta quando ele levanta | osso parado por 3 s caído; uma única religação ao reviver |
| Mesma aranha em lugares diferentes | (a) com **classes diferentes** o gerador consumia aleatórios diferentes antes de espalhar criaturas → mundos diferentes; (b) cada jogo roda a própria IA das criaturas | (a) cada etapa do gerador que define criaturas e mapa (19 etapas) recebe semente própria → mundo igual com qualquer classe; (b) **pendente — ver item 6** | (a) hash IGUAL com Guerreiro×Caçador e Guerreiro×Ladino |
| (achado nos testes) crash nativo ao gerar o nível | trocar `Game.Random` (`static readonly`) por reflexão deixa o código JIT do jogo apontando para um objeto coletado | o objeto não é mais trocado; só o estado interno é copiado de um `System.Random` novo | 4 gerações seguidas sem crash |

### 6. Criaturas sincronizadas — **implementado na v0.3.0** (`reimpl/SparkCoop/SparkCoop.Criaturas.cs`)
- Índice numérico de cada criatura = posição na lista ordenada de chaves (igual nos dois jogos quando o hash do mundo bate).
- Host atribui o dono a cada 0,1 s (raio 40 m, folga 3 m); mensagem nova `Msg.CRIATURAS` (sem garantia de entrega) com
  dono + posição/rotação/vida/estado e parâmetros do Animator; o cliente manda o estado das que são dele.
- Cópia: `Creature` e `DetectionZone` desligados, NavMesh desligada, root motion desligado, posição suavizada; 30 métodos
  `Attack*` bloqueados em cópias e dano vindo de cópia nunca fere o herói local (`MainChar.Damaged` com guarda).
- Golpes: as 5 funções de dano da criatura (`Damaged(...)`, `DamagedThrowingWeapon`) são monitoradas; na chamada mais
  externa numa cópia, a vida perdida vira evento "golpe" para o dono. Armadilhas do cenário não contam (existem nos dois
  mundos); efeitos contínuos (sangramento, veneno) não passam por essas funções.
- Só liga quando o hash do mundo é IGUAL.

| Teste (31/10/2026, Guerreiro×Caçador) | Observado | Status |
| --- | --- | --- |
| Host perto da aranha, cliente longe | dono = host; no cliente vira cópia (IA desligada) na **mesma posição** (-75,58; -106,87) e mesmo estado de animação | ok |
| Cliente golpeia a cópia (40) | 1 golpe de -33,6 (armadura da aranha) aplicado no host; vida volta à cópia | ok |
| Cliente perto, host se afasta (~44 m) | dono passa a cliente; IA religada no cliente, cópia no host | ok |
| Aranha do cliente ataca com o host a ~5 m | cliente 184→23 de vida; host fica 244/244; a cópia no host anima os ataques sem ferir | ok |
| Golpes falsos (efeitos contínuos, armadilhas do cenário) | eliminados depois de 2 correções | ok |

Limitações: uma criatura só ataca o herói do seu dono; criaturas ainda não registradas (geradas depois do nível, ex.:
invocações) não sincronizam; não testado com duas pessoas reais nem pela internet; reações visuais do golpe na cópia
são locais.

#### Desenho original
Hoje cada jogo roda a IA de todas as criaturas e só as mortes são compartilhadas; por isso a mesma aranha pode estar
perseguindo um herói em cada mundo. Proposta: **"dono" por proximidade**.
- Cada criatura ativa tem um dono: o jogador mais próximo dela (com folga para não trocar de dono a todo instante).
  O dono roda a IA e o combate normais do jogo **contra o próprio herói** — exatamente o que o jogo já sabe fazer, sem
  precisar ensinar a IA a enxergar um segundo herói.
- No outro jogo a criatura vira fantoche: IA e NavMesh desligadas, posição/rotação/animação/vida vindas do dono a ~15 Hz.
- Golpe no fantoche: o atacante envia "golpe" (criatura, arma, dano) ao dono, que aplica o dano na criatura real; o
  resultado (vida, morte, reação) volta pelo estado.
- Troca de dono quando o outro jogador fica bem mais perto (ou quando a criatura passa a atacá-lo); o novo dono recebe o
  estado atual (posição, vida, alvo) antes de assumir.
- Limitação aceita no início: uma criatura ataca só o herói do seu dono; com os dois juntos, o dono é quem ela está atacando.
- Passos: (1) chave estável + estado das criaturas no fio; (2) fantoches no lado não dono; (3) golpes encaminhados;
  (4) troca de dono; (5) teste com dois jogadores lutando juntos.

### Ordem proposta
M3a sala Steam + transporte Steam (**feito**; testado até onde uma conta permite — ver `LEIA-ME.md`; falta teste com duas contas) → M3b save co-op e "continuar" →
M3e inimigos atacarem o outro jogador (`Creature.SetEnemy`) → aparência real do outro jogador no fantasma.

## Alertas sobre a instalação

- `DWrite.dll` (BG3 Script Extender Auto-Updater) na pasta do jogo é carregada pelo processo do jogo e não pertence a ele.
  Sugestão: confirmar de onde veio; se não foi intencional, removê-la (guardando uma cópia) ou usar "Verificar integridade"
  da Steam — que também removeria BepInEx/xmod, então só com o seu acordo. **Nada foi alterado na instalação original.**
- O `xmod` (ModFramework + host gRPC externo) não foi analisado. Ele pode coexistir com o SparkCoop, mas o teste foi feito sem ele.

## Distribuição e reversão

- Publicável: só `reimpl/SparkCoop/` (código próprio). Nenhum arquivo do jogo é redistribuído.
- Instalar/remover: ver `reimpl/SparkCoop/LEIA-ME.md`. A remoção apaga só `BepInEx\plugins\SparkCoop\`, a config
  `local.sparkcoop.cfg` e os logs `SparkCoop-*.log`.
- Build não suportado: os patches Harmony falham com erro no log do BepInEx se os métodos mudarem; o jogo continua sem co-op.
  O protocolo tem número de versão no HELLO (cliente de outra versão é ignorado).
