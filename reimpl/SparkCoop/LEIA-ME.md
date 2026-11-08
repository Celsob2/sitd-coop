# SparkCoop 0.5.0 — co-op experimental para Spark in the Dark

Mod BepInEx 6 (Unity Mono). Cada jogador precisa da **própria cópia** do jogo, no mesmo build, e do mesmo `SparkCoop.dll`.

O que faz: mundo igual para os dois (semente do host), o herói do outro aparece no seu jogo como "fantasma" com nome
e luz, e criaturas mortas por um morrem no jogo do outro. Limitações: veja `notas/plano-modificacao.md`.

## Requisitos

- BepInEx 6 para Unity Mono na pasta do jogo (já existe na sua instalação: `BepInEx\core\BepInEx.Unity.Mono.dll`).
  Em outro PC sem BepInEx: instale o BepInEx 6 "Unity (Mono) x64" do GitHub oficial do BepInEx.
- Windows, PowerShell. Para recompilar: nada além do Windows (usa o `csc.exe` do .NET Framework 4).

## Compilar (opcional; `bin\SparkCoop.dll` já está pronto)

```bash
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Jogo "C:\Program Files (x86)\Steam\steamapps\common\Spark in the Dark"
```

## Instalar

Recomendado testar primeiro na cópia de trabalho (já instalado lá). Para a instalação da Steam (pode pedir
PowerShell como administrador, por estar em `Program Files`):

```bash
powershell -ExecutionPolicy Bypass -File .\instalar.ps1 -Jogo "C:\Program Files (x86)\Steam\steamapps\common\Spark in the Dark"
```

Só cria `BepInEx\plugins\SparkCoop\` (DLL + `instalacao.json`). Nenhum arquivo existente é sobrescrito.
**Faça backup dos saves antes de jogar** (`%USERPROFILE%\AppData\LocalLow\Stellar Fish\Spark in the Dark`).

## Jogar

1. Abra o jogo uma vez para gerar `BepInEx\config\local.sparkcoop.cfg` e feche.
2. Edite a seção `[Coop]`:
   - Host: `Role = host` (opcional `Seed = 12345`, `Port = 27450`). Libere a porta UDP 27450 ou use uma VPN de jogos.
   - Amigo: `Role = client`, `HostAddress = <IP do host>`.
   - `PlayerName` = nome mostrado sobre o seu herói.
   Alternativa sem editar: opções de inicialização da Steam `-coop-role host` ou `-coop-role client -coop-host 1.2.3.4`.
3. Os dois ficam no menu até o painel no canto superior esquerdo mostrar `conectado a …`.
4. Os dois começam **Novo jogo** (de preferência a mesma classe). O painel deve mostrar `mundo: IGUAL …`.
   Se mostrar `DIFERENTE`, voltem ao menu e comecem de novo depois de conectados.

### Aparência e mundo (v0.2.0)

- Cada jogador vê o outro com a classe, o cabelo, a armadura e as armas **dele** (atualiza quando ele troca de equipamento).
- Classes diferentes geram o mesmo mapa e as mesmas criaturas nos dois jogos (painel: `mundo: IGUAL`).
- **Criaturas (v0.3.0):** cada criatura perto de algum jogador tem um "dono", o jogador mais próximo. A criatura age e
  ataca no jogo do dono; no jogo do outro ela é uma cópia que só repete os movimentos (não fere ninguém). Golpes na cópia
  são enviados ao dono. O painel mostra "criaturas: minhas X, do outro Y".
- Limitação: uma criatura ataca só o jogador que é dono dela (o mais próximo).
- Os dois precisam do **mesmo SparkCoop.dll** (protocolo 3); versões diferentes não se conectam.

### Morte, reviver e descanso (v0.2.0)

- **Caído:** se você morre e seu amigo está de pé, seu herói fica caído no chão, sem tela de morte.
  Seu amigo chega perto e **segura E** (no controle, o botão de ação) **por 5 segundos**; você levanta com 15% da vida e sem
  veneno, sangramento etc.
- **Abismo:** quem cai no abismo volta ao lado do amigo quando ele terminar um descanso numa cama.
- **Os dois caídos:** fim de jogo normal.
- **Dormir:** o botão de sair da cama só termina o descanso quando os dois confirmarem ("Pronto para dormir"). Aí o dia passa
  e cada jogo salva. Se o amigo estiver caído, não precisa esperar por ele.

Opções de teste (só para depuração): `-coop-autostart 1` atravessa os menus sozinho; `-coop-nosave 1` impede gravar
saves e sincronizar a Steam Cloud.

## Jogar pela Steam (sala e convite) — sem IP e sem abrir porta

Os dois precisam do jogo aberto **pela Steam** (biblioteca → Jogar), conectados à Steam e amigos na Steam.
Deixe `Role = off` na config (o padrão); a sala Steam substitui o modo UDP acima.

1. **Host:** no menu inicial, o painel do canto superior direito mostra `Steam: pronta`. Clique **Co-op: criar sala**.
   O painel mostra o número da sala e `membros 1/2`.
2. Clique **Convidar amigo**: abre o overlay da Steam; escolha o amigo e envie o convite.
   (Também funciona: o amigo clica com o botão direito no seu nome na lista de amigos → **Entrar no jogo**.)
3. **Amigo:** aceite o convite na Steam.
   - Jogo já aberto: entra direto na sala.
   - Jogo fechado: a Steam abre o jogo e ele entra na sala sozinho quando o menu carrega (`+connect_lobby`).
4. Os dois esperam `conectado: sim, com <nome>` no painel e então começam **Novo jogo**, como no passo 4 acima.
5. **Sair da sala** encerra a sessão. Se o host sair, o amigo volta para fora da sala automaticamente.

Detalhes:
- A sala é "só amigos", com 2 vagas. Guarda a versão do mod e do protocolo; versão diferente → o amigo sai da sala e o
  painel mostra a versão do host.
- Tráfego pela Steam (`SteamNetworkingMessages`, canal 7): posição/animação sem garantia de entrega, o resto com garantia.
  Só mensagens de quem está na sala são aceitas.
- Nome do jogador: se `PlayerName` estiver vazio ou no padrão (nome do usuário do Windows), o mod grava ali o seu nome Steam.
- O painel aparece só no menu inicial. O registro fica em `BepInEx\SparkCoop-<papel>.log` (linhas `Steam: …`).

Opções de teste (só depuração): `-coop-steam-create 1` cria a sala ao abrir; `-coop-steam-invite 1` abre o convite logo
depois; `-coop-steam-selftest 1` manda uma mensagem para a própria conta; `-coop-steam-cmd <arquivo>` lê comandos
(`create`, `invite`, `leave`, `join <sala>`, `status`, `lang`) de `BepInEx\<arquivo>`; `-coop-steam-mesmaconta 1` deixa uma
segunda instância da mesma conta entrar como cliente (só para testar a entrada na sala; a troca de mensagens não funciona
com a mesma conta).

## Continuar em co-op (v0.5.0)

- O host carrega um save. Com o amigo conectado, o mod envia esse save (decifrado com a função do próprio jogo, comprimido,
  em partes de 6 KB com confirmação). 1,1 MB de JSON viram ~200 KB; em rede local leva menos de 1 s.
- No amigo aparece **Mundo do host recebido**. Ao escolher um herói (botão do painel **ou** "Continuar jogo" do próprio
  jogo), o mod junta o mundo do host com o `PlayerData` do herói do amigo e os objetos que ele carrega (inventário,
  equipamento), põe o herói ao lado do host, grava `coop_<save do amigo>.save` (JSON puro; o jogo cifra no próximo save)
  e carrega. O save solo do amigo não é alterado.
- Depois de carregar, as criaturas são identificadas pelo **ID de save** do jogo (`SaveableObject`), igual nos dois — o
  hash do mundo bate e criaturas/mortes/portas voltam a sincronizar.
- Clique repetido enquanto carrega é ignorado (antes, carregar duas vezes travava o jogo).
- Limitação: vai o **último save** do host; o que ele fez depois de salvar ainda não é enviado.
- Testado (03/11/2026, duas instâncias): host carregou `thief_3`, amigo entrou com `blacksmith_0` → `coop_blacksmith_0.save`,
  `mundo: IGUAL` (246 criaturas por ID), item do amigo mantido, aranhas mortas, criaturas atacando o outro quando um caiu.

## Portas sincronizadas (v0.5.0)

`DoorAction.OpenDoor/CloseDoor/UnlockDoor` viram evento `porta` (chave = posição arredondada da porta). Só com mundos iguais
e 3 s depois de carregar a fase. Testado: 2 portas abertas e fechadas no host → aplicadas no amigo.

## Dificuldade definida pelo host (v0.5.0)

`Game.Mode` (História/Normal/Pesadelo), `Game.Path` (Sobrevivente/Explorador) e `Game.CalendarEvent` mudam a geração do
mapa. O host envia (evento `regras`); no amigo os seletores da tela de personagem ficam travados com o aviso
"Dificuldade definida pelo host", os valores são aplicados antes de gerar a fase e **Começar** espera o host estar na fase.
Testado: envio/recebimento das regras. **Ainda não testado:** o fluxo completo na tela de personagem.

## Correções (v0.5.0)

- Aranhas "imortais" com vida extra: o jogo corta a vida atual ao máximo sem bônus em cada `CalculateSpecs`; o mod agora
  guarda a vida antes do recálculo e a devolve (antes, uma regra minha enchia a vida de novo).
- Criaturas paradas depois que um jogador caía: herói caído não é mais dono de criaturas.
- Botão "Co-op: criar sala" desligado durante conexão direta (trocava a sessão e derrubava a conexão).
- `-coop-nocloud 1`: bloqueia toda a Steam Cloud (para testes com saves reais).
- Log `DIAG vida subiu` acompanha 10 s a vida de criaturas golpeadas e avisa se ela subir de repente.

## Vida extra dos monstros (v0.4.0)

Com dois jogadores o jogo fica fácil; o host escolhe quanto de vida a mais os monstros têm: **normal, +50%, +75% ou +100%**.

- Onde: linha **Vida dos monstros** no painel do menu inicial (só o host muda; o amigo vê o valor do host com os botões
  desligados), ou `[Coop] MonsterHealthBonus = 0 | 50 | 75 | 100`, ou `-coop-vida-monstros <valor>`.
- O host manda o valor ao amigo ao conectar e a cada troca (evento confiável `vida-monstros`); protocolo 4.
- Como: postfix em `CharacterBase.CalculateSpecs` (onde o jogo calcula vida máxima = base + Constituição × 12)
  multiplica a vida máxima só de criaturas. Quem nasce já nasce com a vida cheia aumentada; trocar no meio da partida
  ajusta a vida atual na mesma proporção. Sem sessão co-op não muda nada.
- O painel do jogo (canto superior esquerdo) mostra `vida dos monstros: +X%` quando o bônus está ligado.
- Testado (03/11/2026, duas instâncias): +0% → aranha 280/280, niss 392/392; +50% → 420/420 e 588/588 nos **dois**
  jogos; +100% → vida máxima dobrada. Criaturas desativadas recebem o bônus quando acordam (o jogo só recalcula nelas aí).

## Itens (limitação)

Cada jogador tem o próprio mundo: baús, corpos e itens no chão **não são sincronizados**. Os dois podem pegar o mesmo item
do mesmo corpo (confirmado em teste pelo usuário). Com classes diferentes o conteúdo dos baús também pode variar, porque
só as etapas do gerador que definem mapa e criaturas têm semente própria (as de itens, como `PutItemsIntoContainers` e
`GenerateCorpseWeapon`, não). Próximo passo possível: semente por etapa de itens + evento "item pego" que remove o item
no jogo do outro.

## Idioma (v0.3.1)

Os textos que o jogador vê (painéis, avisos de caído/reviver/dormir, botões) aparecem em **português do Brasil** ou
**inglês**. Os logs continuam em português.

- Padrão `Language = auto` (seção `[Coop]` da config): segue o idioma escolhido no jogo
  (`UI.LocalizationManager.CurrentLanguage`); português → PT-BR, qualquer outro → inglês.
- Botão **PT-BR / EN** no canto do painel do menu inicial: troca na hora e grava `Language = pt` ou `en`.
- Código: `SparkCoop.Idioma.cs`, função `L.T("texto pt", "text en")`. Texto novo visível ao jogador deve passar por ela.
- Testado (02/11/2026): painel do menu e painel do canto superior esquerdo nos dois idiomas, com acentos, trocando pelo
  botão e pelo comando `lang`. Os avisos dentro da fase (caído, reviver, dormir) usam o mesmo mecanismo, mas não foram
  vistos na tela nesta rodada.

### O que foi testado (31/10/2026, uma conta Steam, Windows 11)

| Item | Resultado |
|---|---|
| Compila junto com o resto (`build.ps1`) | ok, sem avisos |
| Steam detectada (`SteamManager.Initialized`), nome Steam como nome do jogador | ok |
| Criar sala (FriendsOnly, 2) e gravar dados da sala; rich presence `connect` | ok (sala criada em ~0,3 s) |
| Painel no menu (sala, membros, conectado, botões) | ok, visto em captura de tela |
| `Convidar amigo` → `ActivateGameOverlayInviteDialog` | chamada feita sem erro; overlay carregado no processo; o diálogo **não** aparece na captura de tela (o PrintWindow não mostra o overlay) — confirmar a olho |
| Entrar pela linha de comando `+connect_lobby <id>` (2ª instância, mesma conta, `-coop-steam-mesmaconta 1`) | ok: entrou na sala, leu versão/protocolo, iniciou sessão `client` com o dono da sala como host |
| Sair da sala (cliente e host) | ok: sessão encerrada, sala vazia |
| Transporte: enviar e receber pela Steam para a própria conta no mesmo processo | ok (`autoteste ... OK`) |
| Transporte entre duas instâncias da mesma conta | **não funciona** (limitação da Steam: as duas instâncias são o mesmo usuário; a sessão é recriada ~40 vezes por segundo) |
| UDP direto (`rodar-par.ps1`) depois da mudança | ok: 2 fantasmas, mundo IGUAL |
| Clique real nos botões IMGUI | **não testado** automaticamente (os botões chamam os mesmos métodos testados via `-coop-steam-cmd`) |
| Convite aceito por outra conta (`GameLobbyJoinRequested_t`), "Entrar no jogo" pela lista de amigos, troca de mensagens entre duas contas, host saindo com amigo dentro | **não testado** (precisa de 2 contas) |

### Roteiro de teste com dois PCs / duas contas

1. Nos dois PCs: mesmo build do jogo, BepInEx 6, mesmo `SparkCoop.dll`, `Role = off`. Abrir o jogo pela Steam.
2. PC A: **Co-op: criar sala** → painel `membros 1/2`. Log: `Steam: sala criada <id>`.
3. PC A: **Convidar amigo** → o overlay abre com a lista de amigos? Convidar B.
4. PC B (jogo aberto): aceitar → log `Steam: convite aceito de …`, `entrou na sala`, `sessao iniciada: client via SteamTransport`.
   Painel A: `membros 2/2`. Em ~1 s os dois mostram `conectado: sim, com <nome>`; log A: `cliente conectou: <nome> de steam:<id>`.
5. Os dois **Novo jogo** → painel do jogo `mundo: IGUAL`; ver o fantasma do outro andando; matar uma criatura e ver
   morrer no outro jogo.
6. PC B: **Sair da sala** → A volta a `membros 1/2` e `conectado: nao`. Convidar de novo, mas desta vez com o jogo de B
   **fechado**: aceitar o convite deve abrir o jogo e entrar na sala sozinho (log `aberto pelo convite, +connect_lobby`).
7. Com B na sala, A clica **Sair da sala** → B: `o host saiu; encerrando a sessao`.
8. Repetir o passo 4 usando "Entrar no jogo" na lista de amigos de B.
9. Se algo falhar, mandar os dois `BepInEx\SparkCoop-*.log` e `BepInEx\LogOutput.log`. Atenção a muitas linhas
   `mensagens de sessao omitidas` (sessão caindo e voltando).

## Remover

```bash
powershell -ExecutionPolicy Bypass -File .\instalar.ps1 -Jogo "C:\Program Files (x86)\Steam\steamapps\common\Spark in the Dark" -Remover
```

Apaga só o que o mod criou (pasta do plugin, `local.sparkcoop.cfg`, `SparkCoop-*.log`). Quem usa o workspace de
engenharia reversa pode conferir que a instalação voltou ao estado registrado com `manifesto.py verificar
manifesto-original.json` (o manifesto fica fora do Git).

Para desfazer o teste na cópia de trabalho: apagar `spark-coop-ws\trabalho\` inteira (é uma cópia; o original nunca foi editado).
