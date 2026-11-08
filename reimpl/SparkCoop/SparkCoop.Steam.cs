// SparkCoop - co-op para Spark in the Dark
// Copyright (C) 2026 Celso Barbalho
//
// Este programa é software livre: você pode redistribuí-lo e/ou modificá-lo sob os termos da Licença Pública Geral GNU
// (GNU GPL), conforme publicada pela Free Software Foundation, na versão 3 da Licença ou (a seu critério) qualquer versão
// posterior. Este programa é distribuído na esperança de que seja útil, mas SEM NENHUMA GARANTIA; sem mesmo a garantia
// implícita de COMERCIABILIDADE ou ADEQUAÇÃO A UM PROPÓSITO ESPECÍFICO. Veja a Licença Pública Geral GNU para mais
// detalhes. Você deve ter recebido uma cópia da GNU GPL junto com este programa (arquivo LICENSE); se não, veja
// <https://www.gnu.org/licenses/>.
//
// SparkCoop - sala (lobby) Steam e transporte pela Steam (SteamNetworkingMessages).
// Codigo proprio; usa o Steamworks.NET que o jogo ja traz e a Steam que o Management.SteamManager do jogo inicializa.
//
// Fluxo: o host clica "Co-op: criar sala" no menu inicial -> lobby FriendsOnly de 2 -> "Convidar amigo" abre o overlay.
// O amigo aceita pela Steam: com o jogo aberto chega GameLobbyJoinRequested_t; com o jogo fechado a Steam abre o jogo
// com "+connect_lobby <id>". Ao entrar, o amigo inicia a sessao "client" com o dono do lobby como host.
// O UDP direto (Role=host/client na config) continua valendo; criar/entrar numa sala substitui a sessao UDP.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SparkCoop
{
    public partial class Plugin
    {
        public SteamLobby SteamLobby;

        partial void OnAwakeExtensions()
        {
            try { SteamLobby = gameObject.AddComponent<SteamLobby>(); }
            catch (Exception e) { Log("Steam: falha ao iniciar a parte de sala: " + e.Message); }
            try { gameObject.AddComponent<Dificuldade>(); }
            catch (Exception e) { Log("falha ao iniciar a vida extra dos monstros: " + e.Message); }
            try { gameObject.AddComponent<Portas>(); }
            catch (Exception e) { Log("falha ao iniciar a sincronizacao de portas: " + e.Message); }
            try { gameObject.AddComponent<Mundo>(); }
            catch (Exception e) { Log("falha ao iniciar o envio do mundo: " + e.Message); }
            try { gameObject.AddComponent<Regras>(); }
            catch (Exception e) { Log("falha ao iniciar as regras do host: " + e.Message); }
            try { gameObject.AddComponent<Hud>(); }
            catch (Exception e) { Log("falha ao iniciar o HUD (ping/seta): " + e.Message); }
            try { gameObject.AddComponent<Itens>(); }
            catch (Exception e) { Log("falha ao iniciar os achados compartilhados: " + e.Message); }
            try { Observar.Ligar(); }
            catch (Exception e) { Log("falha ao ligar a observacao de itens: " + e.Message); }
        }
    }

    public class SteamLobby : MonoBehaviour
    {
        public const int MaxPlayers = 2;   // arquitetura pronta para 4: basta subir aqui e no transporte (lista de pares)
        public const int Channel = 7;      // canal fixo do SteamNetworkingMessages
        const int SelfTestChannel = 8;

        public bool Ready;                 // Steam inicializada e callbacks registrados
        public CSteamID Lobby = CSteamID.Nil;
        public CSteamID Me = CSteamID.Nil;
        public string Status = "Steam: ...";
        public SteamTransport Transport;

        Callback<GameLobbyJoinRequested_t> _cbJoinRequested;
        Callback<LobbyEnter_t> _cbEnter;
        Callback<LobbyChatUpdate_t> _cbChatUpdate;
        Callback<SteamNetworkingMessagesSessionRequest_t> _cbSessionRequest;
        Callback<SteamNetworkingMessagesSessionFailed_t> _cbSessionFailed;
        Callback<GameRichPresenceJoinRequested_t> _cbRichJoin;
        CallResult<LobbyCreated_t> _crCreated;
        CallResult<LobbyEnter_t> _crJoin;
        float _nextReadyCheck, _readySince = -1f, _selfTestUntil;
        bool _creating, _testInvite, _selfTestDone;
        string _pendingConnect, _cmdFile;
        float _nextCmd, _nextSessionLog;
        int _sessionLogsSkipped;
        // Somente testes com uma conta Steam so (-coop-steam-mesmaconta 1 nas duas instancias): a segunda instancia entra
        // na sala da primeira como cliente, embora o dono da sala seja a mesma conta.
        public static bool SameAccountTest;

        static Plugin P { get { return Plugin.Inst; } }

        static string Arg(string name)
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        void Update()
        {
            if (!Ready)
            {
                if (Time.realtimeSinceStartup < _nextReadyCheck) return;
                _nextReadyCheck = Time.realtimeSinceStartup + 1f;
                TryInit();
                return;
            }
            float now = Time.realtimeSinceStartup;
            // Convite recebido antes de a Steam ficar pronta ou pela linha de comando: entra quando o menu ja carregou.
            if (_pendingConnect != null && now - _readySince > 2f)
            {
                string id = _pendingConnect; _pendingConnect = null;
                ulong v;
                if (ulong.TryParse(id, out v)) Join(new CSteamID(v)); else P.Log("Steam: +connect_lobby invalido: " + id);
            }
            if (_testInvite && Lobby != CSteamID.Nil && IsOwner()) { _testInvite = false; Invite(); }
            if (_selfTestUntil > 0) SelfTestPoll(now);
            if (_cmdFile != null && now > _nextCmd) { _nextCmd = now + 1f; TestCommands(); }
        }

        // Somente testes: le comandos de BepInEx\<arquivo de -coop-steam-cmd> e chama o mesmo que os botoes.
        //   create | invite | leave | join <id do lobby> | status | lang (alterna PT/EN, como o botao)
        void TestCommands()
        {
            string f = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, _cmdFile);
            if (!System.IO.File.Exists(f)) return;
            string[] lines = System.IO.File.ReadAllLines(f);
            System.IO.File.Delete(f);
            foreach (string line in lines)
            {
                string[] a = line.Trim().Split(' ');
                P.Log("Steam teste: comando " + line.Trim());
                ulong v;
                if (a[0] == "create") CreateLobby();
                else if (a[0] == "invite") Invite();
                else if (a[0] == "leave") Leave();
                else if (a[0] == "lang") { L.Alternar(); P.Log("Steam teste: idioma agora " + L.Sigla); }
                else if (a[0] == "join" && a.Length == 2 && ulong.TryParse(a[1], out v)) Join(new CSteamID(v));
                else if (a[0] == "status")
                    P.Log("Steam teste: sala=" + Lobby.m_SteamID + " dono=" + IsOwner() + " membros=" + string.Join(", ", MemberNames().ToArray()) +
                          " sessao=" + P.Role + " par=" + (P.Net == null ? "-" : P.Net.HasPeer + " " + P.Net.PeerEndPoint) + " nome do par=" + P.PeerName);
            }
        }

        void TryInit()
        {
            bool ok;
            try { ok = Management.SteamManager.Initialized; }
            catch (Exception e) { Status = L.T("Steam: indisponível (", "Steam: unavailable (") + e.Message + ")"; return; }
            if (!ok) { Status = L.T("Steam: não inicializada (jogo aberto fora da Steam?)", "Steam: not initialized (game opened outside Steam?)"); return; }

            Me = SteamUser.GetSteamID();
            _cbJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
            _cbEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
            _cbChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnChatUpdate);
            _cbSessionRequest = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
            _cbSessionFailed = Callback<SteamNetworkingMessagesSessionFailed_t>.Create(OnSessionFailed);
            _cbRichJoin = Callback<GameRichPresenceJoinRequested_t>.Create(OnRichJoin);
            _crCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            _crJoin = CallResult<LobbyEnter_t>.Create(OnJoinResult);
            try { SteamNetworkingUtils.InitRelayNetworkAccess(); } catch (Exception e) { P.Log("Steam: InitRelayNetworkAccess falhou: " + e.Message); }
            Ready = true;
            _readySince = Time.realtimeSinceStartup;
            Status = L.T("Steam: pronta", "Steam: ready");

            string persona = SteamFriends.GetPersonaName();
            // Nome do jogador: se a config estiver vazia ou no padrao (nome do usuario do Windows), usa o nome Steam.
            if (string.IsNullOrEmpty(P.CfgName.Value) || P.CfgName.Value == (string)P.CfgName.DefaultValue)
                P.CfgName.Value = persona;
            P.Log("Steam: pronta; usuario " + persona + " (" + Me.m_SteamID + "), nome no co-op = " + P.CfgName.Value);

            string connect = Arg("+connect_lobby");
            if (connect != null) { P.Log("Steam: aberto pelo convite, +connect_lobby " + connect); _pendingConnect = connect; }
            // Somente testes: -coop-steam-create 1 cria a sala sozinho; -coop-steam-invite 1 abre o convite em seguida;
            // -coop-steam-selftest 1 manda uma mensagem para a propria conta pelo SteamNetworkingMessages.
            if (Arg("-coop-steam-create") == "1") CreateLobby();
            _testInvite = Arg("-coop-steam-invite") == "1";
            if (Arg("-coop-steam-selftest") == "1") StartSelfTest();
            _cmdFile = Arg("-coop-steam-cmd");
            SameAccountTest = Arg("-coop-steam-mesmaconta") == "1";
        }

        // ---------------------------------------------------------------- Acoes (botoes)
        public void CreateLobby()
        {
            if (!Ready || _creating) return;
            if (Lobby != CSteamID.Nil) Leave();
            _creating = true;
            Status = L.T("criando sala...", "creating lobby...");
            P.Log("Steam: criando sala (amigos, " + MaxPlayers + " jogadores)");
            _crCreated.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxPlayers));
        }

        public void Invite()
        {
            if (!Ready || Lobby == CSteamID.Nil) return;
            P.Log("Steam: abrindo convite do overlay para a sala " + Lobby.m_SteamID);
            SteamFriends.ActivateGameOverlayInviteDialog(Lobby);
        }

        public void Join(CSteamID lobby)
        {
            if (!Ready) { _pendingConnect = lobby.m_SteamID.ToString(); return; }
            if (Lobby == lobby) return;
            if (Lobby != CSteamID.Nil) Leave();
            Status = L.T("entrando na sala ", "joining lobby ") + lobby.m_SteamID + "...";
            P.Log("Steam: entrando na sala " + lobby.m_SteamID);
            _crJoin.Set(SteamMatchmaking.JoinLobby(lobby));
        }

        public void Leave()
        {
            if (Lobby != CSteamID.Nil)
            {
                P.Log("Steam: saindo da sala " + Lobby.m_SteamID);
                SteamMatchmaking.LeaveLobby(Lobby);
            }
            Lobby = CSteamID.Nil;
            try { SteamFriends.ClearRichPresence(); } catch { }
            if (Transport != null && P.Net == Transport) P.EndSession();
            else if (Transport != null) Transport.Close();
            Transport = null;
            Status = L.T("Steam: pronta", "Steam: ready");
        }

        void OnDestroy()
        {
            if (Ready && Lobby != CSteamID.Nil) { try { SteamMatchmaking.LeaveLobby(Lobby); } catch { } }
        }

        // ---------------------------------------------------------------- Callbacks
        void OnLobbyCreated(LobbyCreated_t r, bool ioFail)
        {
            _creating = false;
            if (ioFail || r.m_eResult != EResult.k_EResultOK) { Status = L.T("falha ao criar sala: ", "failed to create lobby: ") + (ioFail ? "IO" : r.m_eResult.ToString()); P.Log("Steam: " + Status); return; }
            Lobby = new CSteamID(r.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(Lobby, "game", "sparkcoop");
            SteamMatchmaking.SetLobbyData(Lobby, "mod_version", Plugin.Version);
            SteamMatchmaking.SetLobbyData(Lobby, "protocol", Plugin.Protocol.ToString());
            SteamMatchmaking.SetLobbyData(Lobby, "host_name", P.CfgName.Value);
            // "connect" na rich presence: o amigo tambem pode entrar por "Entrar no jogo" na lista de amigos.
            SteamFriends.SetRichPresence("connect", "+connect_lobby " + Lobby.m_SteamID);
            SteamFriends.SetRichPresence("status", "Co-op: sala aberta");
            Status = L.T("sala criada", "lobby created");
            P.Log("Steam: sala criada " + Lobby.m_SteamID);
            Transport = new SteamTransport(this, true, CSteamID.Nil);
            P.StartSession("host", Transport);
        }

        void OnJoinResult(LobbyEnter_t r, bool ioFail)
        {
            if (ioFail) { Status = L.T("falha ao entrar na sala (IO)", "failed to join lobby (IO)"); P.Log("Steam: " + Status); }
            // O resto e tratado em OnLobbyEnter (que a Steam tambem dispara).
        }

        void OnLobbyEnter(LobbyEnter_t r)
        {
            var lobby = new CSteamID(r.m_ulSteamIDLobby);
            var resp = (EChatRoomEnterResponse)r.m_EChatRoomEnterResponse;
            if (resp != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Status = L.T("não foi possível entrar na sala: ", "could not join lobby: ") + resp;
                P.Log("Steam: " + Status);
                return;
            }
            CSteamID owner = SteamMatchmaking.GetLobbyOwner(lobby);
            if (owner == Me && (Lobby == lobby || !SameAccountTest)) { P.Log("Steam: entrou na propria sala " + lobby.m_SteamID); return; }   // host: ja tratado em OnLobbyCreated
            Lobby = lobby;
            string proto = SteamMatchmaking.GetLobbyData(lobby, "protocol"), ver = SteamMatchmaking.GetLobbyData(lobby, "mod_version");
            P.Log("Steam: entrou na sala " + lobby.m_SteamID + " do host " + SteamFriends.GetFriendPersonaName(owner) + " (" + owner.m_SteamID +
                  "), mod " + ver + " protocolo " + proto);
            if (proto != Plugin.Protocol.ToString())
            {
                Status = L.T("versão diferente: host usa SparkCoop ", "different version: host uses SparkCoop ") + ver + L.T(" (protocolo ", " (protocol ") + proto + L.T("), você ", "), you ") + Plugin.Version;
                P.Log("Steam: " + Status + "; saindo da sala");
                CSteamID l = Lobby; Lobby = CSteamID.Nil;
                SteamMatchmaking.LeaveLobby(l);
                return;
            }
            SteamFriends.SetRichPresence("status", "Co-op: na sala de " + SteamFriends.GetFriendPersonaName(owner));
            Status = L.T("na sala", "in lobby");
            Transport = new SteamTransport(this, false, owner);
            P.StartSession("client", Transport);
        }

        void OnJoinRequested(GameLobbyJoinRequested_t r)
        {
            P.Log("Steam: convite aceito de " + SteamFriends.GetFriendPersonaName(r.m_steamIDFriend) + " para a sala " + r.m_steamIDLobby.m_SteamID);
            Join(r.m_steamIDLobby);
        }

        void OnRichJoin(GameRichPresenceJoinRequested_t r)
        {
            // "Entrar no jogo" pela lista de amigos com o jogo aberto: r.m_rgchConnect = "+connect_lobby <id>".
            string s = r.m_rgchConnect ?? "";
            P.Log("Steam: pedido de entrada pela lista de amigos: " + s);
            string[] a = s.Split(' ');
            ulong v;
            if (a.Length == 2 && a[0] == "+connect_lobby" && ulong.TryParse(a[1], out v)) Join(new CSteamID(v));
        }

        void OnChatUpdate(LobbyChatUpdate_t r)
        {
            if (new CSteamID(r.m_ulSteamIDLobby) != Lobby) return;
            var who = new CSteamID(r.m_ulSteamIDUserChanged);
            var ch = (EChatMemberStateChange)r.m_rgfChatMemberStateChange;
            bool entered = (ch & EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0;
            P.Log("Steam: " + SteamFriends.GetFriendPersonaName(who) + (entered ? " entrou na sala" : " saiu da sala (" + ch + ")"));
            if (entered || Transport == null) return;
            if (Transport.IsHost) Transport.DropPeer(who);
            else if (who == Transport.Host)
            {
                P.Log("Steam: o host saiu; encerrando a sessao");
                Leave();
                Status = L.T("o host fechou a sala", "the host closed the lobby");
            }
        }

        void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t r)
        {
            SteamNetworkingIdentity id = r.m_identityRemote;
            CSteamID who = id.GetSteamID();
            // So aceita quem esta na mesma sala (o cliente, alem disso, so aceita o dono da sala).
            bool ok = Lobby != CSteamID.Nil && IsMember(who) && (Transport == null || Transport.IsHost || who == Transport.Host);
            if ((_selfTestUntil > 0 || SameAccountTest) && who == Me) ok = true;
            if (SessionLogAllowed())
                P.Log("Steam: pedido de sessao de " + SteamFriends.GetFriendPersonaName(who) + " (" + who.m_SteamID + ") -> " + (ok ? "aceito" : "recusado"));
            if (ok) SteamNetworkingMessages.AcceptSessionWithUser(ref id);
        }

        void OnSessionFailed(SteamNetworkingMessagesSessionFailed_t r)
        {
            if (SessionLogAllowed()) P.Log("Steam: sessao falhou com " + r.m_info.m_identityRemote.GetSteamID().m_SteamID + ": " + r.m_info.m_eEndReason + " " + r.m_info.m_szEndDebug);
        }

        // Pedidos/falhas de sessao podem se repetir varias vezes por segundo (AutoRestartBrokenSession); loga no maximo 1 a cada 5 s.
        bool SessionLogAllowed()
        {
            float now = Time.realtimeSinceStartup;
            if (now < _nextSessionLog) { _sessionLogsSkipped++; return false; }
            _nextSessionLog = now + 5f;
            if (_sessionLogsSkipped > 0) P.Log("Steam: (" + _sessionLogsSkipped + " mensagens de sessao omitidas)");
            _sessionLogsSkipped = 0;
            return true;
        }

        // ---------------------------------------------------------------- Consultas
        public bool IsOwner() { return Lobby != CSteamID.Nil && SteamMatchmaking.GetLobbyOwner(Lobby) == Me; }

        public bool IsMember(CSteamID who)
        {
            if (Lobby == CSteamID.Nil) return false;
            int n = SteamMatchmaking.GetNumLobbyMembers(Lobby);
            for (int i = 0; i < n; i++) if (SteamMatchmaking.GetLobbyMemberByIndex(Lobby, i) == who) return true;
            return false;
        }

        public List<string> MemberNames()
        {
            var l = new List<string>();
            if (Lobby == CSteamID.Nil) return l;
            CSteamID owner = SteamMatchmaking.GetLobbyOwner(Lobby);
            int n = SteamMatchmaking.GetNumLobbyMembers(Lobby);
            for (int i = 0; i < n; i++)
            {
                CSteamID m = SteamMatchmaking.GetLobbyMemberByIndex(Lobby, i);
                l.Add(SteamFriends.GetFriendPersonaName(m) + (m == owner ? " (host)" : ""));
            }
            return l;
        }

        // ---------------------------------------------------------------- Autoteste (uma conta so)
        void StartSelfTest()
        {
            _selfTestUntil = Time.realtimeSinceStartup + 15f;
            var id = new SteamNetworkingIdentity();
            id.SetSteamID(Me);
            byte[] b = Msg.Hello("autoteste").Write();
            EResult r = SteamTransport.SendRaw(ref id, b, Constants.k_nSteamNetworkingSend_Reliable | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession, SelfTestChannel);
            P.Log("Steam autoteste: SendMessageToUser(propria conta) = " + r);
        }

        void SelfTestPoll(float now)
        {
            var ptrs = new IntPtr[8];
            int n = SteamNetworkingMessages.ReceiveMessagesOnChannel(SelfTestChannel, ptrs, ptrs.Length);
            for (int i = 0; i < n; i++)
            {
                CSteamID from; byte[] data = SteamTransport.Take(ptrs[i], out from);
                Msg m = null; try { m = Msg.Read(data); } catch { }
                P.Log("Steam autoteste: recebido " + data.Length + " bytes de " + from.m_SteamID + " -> " + (m != null && m.Name == "autoteste" ? "OK" : "conteudo inesperado"));
                _selfTestDone = true;
            }
            if (now > _selfTestUntil)
            {
                _selfTestUntil = 0;
                if (!_selfTestDone) P.Log("Steam autoteste: nada recebido em 15 s");
            }
        }

        // ---------------------------------------------------------------- UI (IMGUI, so no menu inicial)
        void OnGUI()
        {
            if (P == null || SceneManager.GetActiveScene().name != "StartMenu") return;
            float w = 380, x = Screen.width - w - 10, y = 10;
            var lines = new List<string>();
            lines.Add("<b>SparkCoop " + Plugin.Version + L.T(" - co-op pela Steam</b>", " - Steam co-op</b>"));
            // "pronta" acompanha a troca de idioma; os demais estados ficam no idioma em que aconteceram.
            string st = Status == "Steam: pronta" || Status == "Steam: ready" ? L.T("Steam: pronta", "Steam: ready") : Status;
            if (!Ready || Lobby == CSteamID.Nil) lines.Add(st);
            else
            {
                List<string> ms = MemberNames();
                lines.Add(L.T("sala: ", "lobby: ") + Lobby.m_SteamID + (IsOwner() ? L.T("  (você é o host)", "  (you are the host)") : ""));
                lines.Add(L.T("membros ", "members ") + ms.Count + "/" + MaxPlayers + ": " + string.Join(", ", ms.ToArray()));
                bool conectado = Transport != null && P.Net == Transport && Transport.HasPeer && Time.realtimeSinceStartup - P.LastRecv < 3f;
                lines.Add(L.T("conectado: ", "connected: ") + (conectado ? L.T("sim, com ", "yes, with ") + P.PeerName : L.T("não", "no") + (IsOwner() ? L.T(" (aguardando amigo)", " (waiting for friend)") : L.T(" (conectando ao host...)", " (connecting to host...)"))));
            }
            if (P.Net != null && !(P.Net is SteamTransport)) lines.Add(L.T("sessão UDP direta ativa (", "direct UDP session active (") + P.Role + ")");
            // Amigo com o mundo do host recebido: botoes para entrar nele com um heroi seu.
            List<string> saves = null;
            if (Mundo.Inst != null && P.Role == "client" && Mundo.Inst.JsonDoHost != null)
            {
                lines.Add(L.T("Mundo do host recebido: ", "Host world received: ") + Mundo.Inst.ResumoDoHost);
                lines.Add(L.T("Continuar co-op com o seu herói:", "Continue co-op with your hero:"));
                saves = Mundo.Inst.SavesDoAmigo();
                if (saves.Count > 5) saves = saves.GetRange(0, 5);
            }
            float h = 22 + lines.Count * 20 + 34 + (Dificuldade.Inst != null ? 62 : 0) + (saves != null ? saves.Count * 28 : 0);
            GUI.Box(new Rect(x, y, w, h), "");
            float ly = y + 6;
            foreach (string s in lines) { GUI.Label(new Rect(x + 8, ly, w - 16, 20), s); ly += 20; }
            // Idioma dos textos do mod (PT-BR / EN); grava em [Coop] Language.
            if (GUI.Button(new Rect(x + w - 66, y + 5, 58, 20), L.Sigla)) L.Alternar();
            ly += 4;
            GUI.enabled = Ready;
            // Com conexao direta (UDP) ativa, criar sala trocaria a sessao e derrubaria a conexao: botao desligado.
            bool udpAtivo = P.Net != null && !(P.Net is SteamTransport);
            if (Lobby == CSteamID.Nil)
            {
                GUI.enabled = Ready && !_creating && !udpAtivo;
                if (GUI.Button(new Rect(x + 8, ly, w - 16, 28), L.T("Co-op: criar sala", "Co-op: create lobby"))) CreateLobby();
            }
            else
            {
                float bw = (w - 24) / 2;
                GUI.enabled = Ready && IsOwner();
                if (GUI.Button(new Rect(x + 8, ly, bw, 28), L.T("Convidar amigo", "Invite friend"))) Invite();
                GUI.enabled = true;
                if (GUI.Button(new Rect(x + 16 + bw, ly, bw, 28), L.T("Sair da sala", "Leave lobby"))) Leave();
            }
            GUI.enabled = true;
            // Vida extra dos monstros: quem escolhe e o host; o amigo ve o valor recebido.
            Dificuldade d = Dificuldade.Inst;
            if (d != null)
            {
                ly += 34;
                bool cliente = P.Role == "client";
                int atual = cliente ? d.BonusDoHost : Dificuldade.Normalizar(d.CfgVidaMonstros.Value);
                GUI.Label(new Rect(x + 8, ly + 3, 130, 20), L.T("Vida dos monstros:", "Monster health:"));
                float bx = x + 138, bw2 = (w - 146 - 12) / 4;
                for (int i = 0; i < Dificuldade.Opcoes.Length; i++)
                {
                    int o = Dificuldade.Opcoes[i];
                    GUI.enabled = !cliente;
                    GUI.color = o == atual ? new Color(1f, 0.8f, 0.3f) : Color.white;
                    if (GUI.Button(new Rect(bx + i * (bw2 + 4), ly, bw2, 24), (o == atual ? "> " : "") + d.Rotulo(o))) d.Escolher(o);
                }
                // Queda no abismo: mantem ou perde os itens (o host escolhe).
                ly += 30;
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 8, ly + 3, 130, 20), L.T("Queda no abismo:", "Abyss fall:"));
                float bw3 = (w - 146 - 8) / 2;
                bool perde = d.AbismoPerdeItens;
                GUI.enabled = !cliente;
                GUI.color = !perde ? new Color(1f, 0.8f, 0.3f) : Color.white;
                if (GUI.Button(new Rect(bx, ly, bw3, 24), (!perde ? "> " : "") + L.T("mantém itens", "keep items"))) d.EscolherAbismo(false);
                GUI.color = perde ? new Color(1f, 0.8f, 0.3f) : Color.white;
                if (GUI.Button(new Rect(bx + bw3 + 4, ly, bw3, 24), (perde ? "> " : "") + L.T("perde tudo", "lose all"))) d.EscolherAbismo(true);
                GUI.color = Color.white;
                GUI.enabled = true;
            }
            if (saves != null)
            {
                ly += 30;
                foreach (string s in saves)
                {
                    if (GUI.Button(new Rect(x + 8, ly, w - 16, 24), Mundo.Inst.Rotulo(s))) Mundo.Inst.EntrarComHeroi(s);
                    ly += 28;
                }
            }
        }
    }

    // ---------------------------------------------------------------- Transporte Steam
    // SteamNetworkingMessages: sem porta aberta nem IP; a Steam resolve NAT/relay. STATE vai sem garantia, o resto com garantia.
    public class SteamTransport : ITransport
    {
        readonly SteamLobby _lobby;
        public readonly bool IsHost;
        public readonly CSteamID Host;   // cliente: dono do lobby
        CSteamID _peer = CSteamID.Nil;   // host: convidado atual (1 por enquanto); cliente: host depois do 1o pacote
        readonly Queue<Msg> _inbox = new Queue<Msg>();
        readonly IntPtr[] _ptrs = new IntPtr[32];
        bool _closed;
        float _nextErrLog;

        public SteamTransport(SteamLobby lobby, bool isHost, CSteamID host)
        {
            _lobby = lobby; IsHost = isHost; Host = host;
        }

        public bool HasPeer { get { return _peer != CSteamID.Nil; } }
        public string PeerEndPoint { get { return _peer == CSteamID.Nil ? "?" : "steam:" + _peer.m_SteamID; } }
        public string StatusText
        {
            get { return IsHost ? L.T("sala Steam aberta, aguardando amigo", "Steam lobby open, waiting for friend") : L.T("conectando ao host pela Steam (", "connecting to host via Steam (") + SteamFriends.GetFriendPersonaName(Host) + ")"; }
        }

        public void DropPeer(CSteamID who)
        {
            if (who != _peer) return;
            Plugin.Inst.Log("Steam: convidado " + who.m_SteamID + " desconectado");
            var id = Identity(who);
            SteamNetworkingMessages.CloseSessionWithUser(ref id);
            _peer = CSteamID.Nil;
        }

        public bool TryReceive(out Msg m)
        {
            if (_inbox.Count == 0 && !_closed) Pump();
            if (_inbox.Count == 0) { m = null; return false; }
            m = _inbox.Dequeue();
            return true;
        }

        void Pump()
        {
            int n = SteamNetworkingMessages.ReceiveMessagesOnChannel(SteamLobby.Channel, _ptrs, _ptrs.Length);
            for (int i = 0; i < n; i++)
            {
                CSteamID from;
                byte[] data = Take(_ptrs[i], out from);
                Msg m = null;
                try { m = Msg.Read(data); } catch { }
                if (m == null) continue;
                if (IsHost)
                {
                    // Igual ao UDP: HELLO de um membro da sala define o convidado; outros pacotes so do convidado atual.
                    if (m.Type == Msg.HELLO && _lobby.IsMember(from) && (from != _lobby.Me || SteamLobby.SameAccountTest)) _peer = from;
                    else if (from != _peer) continue;
                }
                else
                {
                    if (from != Host) continue;
                    _peer = from;
                }
                if (_inbox.Count < 256) _inbox.Enqueue(m);
            }
        }

        public void Send(Msg m)
        {
            if (_closed) return;
            CSteamID to = IsHost ? _peer : Host;
            if (to == CSteamID.Nil) return;
            int flags = (Msg.SemGarantia(m.Type) ? Constants.k_nSteamNetworkingSend_UnreliableNoNagle : Constants.k_nSteamNetworkingSend_Reliable)
                        | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession;
            var id = Identity(to);
            EResult r = SendRaw(ref id, m.Write(), flags, SteamLobby.Channel);
            if (r != EResult.k_EResultOK && Time.realtimeSinceStartup > _nextErrLog)
            {
                _nextErrLog = Time.realtimeSinceStartup + 5f;
                Plugin.Inst.Log("Steam: SendMessageToUser(" + to.m_SteamID + ") = " + r);
            }
        }

        public void Close()
        {
            if (_closed) return;
            _closed = true;
            CSteamID to = IsHost ? _peer : Host;
            if (to != CSteamID.Nil) { var id = Identity(to); try { SteamNetworkingMessages.CloseSessionWithUser(ref id); } catch { } }
        }

        static SteamNetworkingIdentity Identity(CSteamID s)
        {
            var id = new SteamNetworkingIdentity();
            id.SetSteamID(s);
            return id;
        }

        public static EResult SendRaw(ref SteamNetworkingIdentity to, byte[] data, int flags, int channel)
        {
            GCHandle h = GCHandle.Alloc(data, GCHandleType.Pinned);
            try { return SteamNetworkingMessages.SendMessageToUser(ref to, h.AddrOfPinnedObject(), (uint)data.Length, flags, channel); }
            finally { h.Free(); }
        }

        // Copia os bytes de uma mensagem recebida e a libera.
        public static byte[] Take(IntPtr p, out CSteamID from)
        {
            SteamNetworkingMessage_t msg = SteamNetworkingMessage_t.FromIntPtr(p);
            var data = new byte[msg.m_cbSize];
            Marshal.Copy(msg.m_pData, data, 0, msg.m_cbSize);
            from = msg.m_identityPeer.GetSteamID();
            SteamNetworkingMessage_t.Release(p);
            return data;
        }
    }
}
