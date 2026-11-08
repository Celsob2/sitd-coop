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
// SparkCoop - prototipo de co-op para "Spark in the Dark" (BepInEx 6 / Unity Mono).
// Codigo proprio; nao contem codigo do jogo. Escrito em C# 5 para compilar com o csc do .NET Framework.
//
// O que sincroniza (v0.1):
//  1. Semente do mundo: o host envia uma semente; os dois lados semeiam UnityEngine.Random e Game.Random
//     antes de LevelManager.LevelGeneration, para gerar a mesma masmorra. Um hash do mundo confirma.
//  2. Presenca: posicao, rotacao e parametros/estados do Animator do heroi de cada lado, a 20 Hz.
//     Do outro lado aparece um "fantasma" (copia visual do heroi, sem scripts nem colisao).
//  3. Mortes de criaturas: quem mata envia a chave da criatura; o outro lado chama Creature.Death.
//
// Rede: UDP simples, sem dependencias. Host escuta em Port; cliente conecta em HostAddress:Port.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using Creatures;
using Management;

namespace SparkCoop
{
    [BepInPlugin(Guid, "SparkCoop", Version)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string Guid = "local.sparkcoop";
        public const string Version = "0.6.0";
        public const int Protocol = 6;

        public static Plugin Inst;
        public ConfigEntry<string> CfgRole, CfgHost, CfgName;
        public ConfigEntry<int> CfgPort, CfgSeed;
        public ConfigEntry<bool> CfgAutoStart, CfgHud;

        public string Role = "off";
        public bool NoSave, NoCloud;
        public ITransport Net;
        public int Seed;
        public bool HasSeed;
        public string LocalWorldHash = "", RemoteWorldHash = "";
        public float LastRecv = -999f;
        public string PeerName = "";
        public string PeerScene = "";
        public Ghost Ghost;
        public CreatureRegistry Registry = new CreatureRegistry();
        public readonly List<string> KillsToSend = new List<string>();
        public readonly HashSet<string> KnownKills = new HashSet<string>();
        public int RemoteKillsApplied;
        StreamWriter _log;
        float _nextSend, _nextHello, _nextAuto, _nextGhostLog;
        int _autoSteps;
        uint _seq;

        void Awake()
        {
            Inst = this;
            CfgRole = Config.Bind("Coop", "Role", "off", "off | host | client. O argumento de linha de comando -coop-role tem prioridade.");
            CfgHost = Config.Bind("Coop", "HostAddress", "127.0.0.1", "Endereco do host (usado pelo cliente).");
            CfgPort = Config.Bind("Coop", "Port", 27450, "Porta UDP do host.");
            CfgSeed = Config.Bind("Coop", "Seed", 0, "Semente do mundo no host; 0 = aleatoria a cada sessao.");
            CfgName = Config.Bind("Coop", "PlayerName", Environment.UserName, "Nome exibido sobre o seu heroi no jogo do outro jogador.");
            CfgAutoStart = Config.Bind("Debug", "AutoStartNewGame", false, "Inicia um jogo novo sozinho (para testes automatizados).");
            CfgHud = Config.Bind("Coop", "ShowHud", true, "Mostra o painel de status do co-op.");

            Role = Arg("-coop-role", CfgRole.Value).ToLowerInvariant();
            if (Arg("-coop-autostart", "") == "1") CfgAutoStart.Value = true;
            NoSave = Arg("-coop-nosave", "") == "1";
            NoCloud = NoSave || Arg("-coop-nocloud", "") == "1";
            string hostArg = Arg("-coop-host", CfgHost.Value);
            int port = CfgPort.Value;
            int.TryParse(Arg("-coop-port", port.ToString()), out port);

            _log = new StreamWriter(Path.Combine(Paths.BepInExRootPath, "SparkCoop-" + Role + ".log"), false, Encoding.UTF8);
            _log.AutoFlush = true;
            Log("SparkCoop " + Version + " role=" + Role + " host=" + hostArg + " port=" + port);
            // Os patches sao inofensivos sem sessao (cada um checa Net/HasSeed); a sessao pode comecar depois (ex.: sala Steam).
            var harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(Patches));
            harmony.PatchAll(typeof(VidaPatches));
            Log("etapas do gerador com semente propria: " + Patches.PatchEtapasDoMundo(harmony));
            Log("scripts de estado protegidos contra o fantasma: " + VidaPatches.PatchStateBehaviours(harmony));
            gameObject.AddComponent<CoopVida>();
            harmony.PatchAll(typeof(CriaturaPatches));
            Log("ataques de criatura protegidos nas copias: " + CriaturaPatches.PatchAtaques(harmony));
            Log("funcoes de dano de criatura monitoradas: " + CriaturaPatches.PatchDanos(harmony));
            gameObject.AddComponent<CreatureSync>();
            GameEvent += OnEventoAparencia;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Log("patches aplicados");
            OnAwakeExtensions();
            if (Role != "host" && Role != "client") { Log("co-op desligado ate uma sessao comecar (Role=off)"); return; }
            StartSession(Role, new Net(Role == "host", hostArg, port));
        }

        // Pontos de extensao implementados em outros arquivos do mesmo plugin (ex.: SparkCoop.Steam.cs).
        partial void OnAwakeExtensions();

        // Inicia uma sessao co-op com qualquer transporte (UDP direto ou Steam).
        public void StartSession(string role, ITransport transport)
        {
            if (Net != null) Net.Close();
            Role = role;
            Net = transport;
            HasSeed = false; PeerName = ""; LastRecv = -999f;
            if (Role == "host")
            {
                Seed = CfgSeed.Value != 0 ? CfgSeed.Value : new System.Random().Next(1, int.MaxValue);
                HasSeed = true;
                Log("semente do mundo = " + Seed);
            }
            Log("sessao iniciada: " + Role + " via " + transport.GetType().Name);
        }

        public void EndSession()
        {
            if (Net != null) Net.Close();
            Net = null; Role = "off"; PeerName = "";
            if (Ghost != null) { Ghost.Destroy(); Ghost = null; }
            Log("sessao encerrada");
        }

        static string Arg(string name, string def)
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return def;
        }

        public void Log(string s)
        {
            Logger.LogInfo(s);
            if (_log != null) _log.WriteLine(DateTime.Now.ToString("HH:mm:ss.fff") + " " + s);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log("cena carregada: " + scene.name);
            if (Ghost != null) { Ghost.Destroy(); Ghost = null; }
            if (CoopVida.Inst != null) CoopVida.Inst.NovoNivel();
            if (CreatureSync.Inst != null) CreatureSync.Inst.NovoNivel();
            _apEnviada = null; // reenvia a aparencia na cena nova
            _autoSteps = 0;
        }

        void OnDestroy()
        {
            if (Net != null) Net.Close();
        }

        void Update()
        {
            if (Net == null) return;
            float now = Time.realtimeSinceStartup;
            Msg m;
            while (Net.TryReceive(out m)) Handle(m, now);

            if (Role == "client" && (!Net.HasPeer || !HasSeed) && now > _nextHello)
            {
                _nextHello = now + 1f;
                Net.Send(Msg.Hello(CfgName.Value));
            }
            if (Net.HasPeer) { ResendEvents(now); TickAparencia(now); }
            if (Net.HasPeer && now > _nextSend)
            {
                _nextSend = now + 0.05f;
                SendState();
                if (PendingKills.Count > 0 && (_seq % 20) == 0) RetryPendingKills();
            }
            if (CfgAutoStart.Value && now > _nextAuto) { _nextAuto = now + 3f; AutoStart(); TestCommands(); }
            if (Ghost != null) Ghost.Tick(Time.deltaTime);
            if (_walkUntil > 0 && MainChar.Instance != null)
            {
                // Mesmo efeito de OnMove(InputValue): o jogo le MainChar.x/y no Update.
                bool ativo = now < _walkUntil;
                MainChar.Instance.x = ativo ? _walk.x : 0f;
                MainChar.Instance.y = ativo ? _walk.y : 0f;
                if (!ativo) { _walkUntil = 0; Log("teste: parou de andar em " + MainChar.Instance.transform.position); }
            }
        }

        bool _classeEscolhida;
        Vector2 _walk;
        float _walkUntil;

        void Handle(Msg m, float now)
        {
            LastRecv = now;
            switch (m.Type)
            {
                case Msg.HELLO:
                    if (Role != "host") return;
                    if (m.Protocol != Protocol) { Log("cliente com protocolo " + m.Protocol + " ignorado"); return; }
                    if (PeerName != m.Name) Log("cliente conectou: " + m.Name + " de " + Net.PeerEndPoint);
                    PeerName = m.Name;
                    Net.Send(Msg.Welcome(CfgName.Value, Seed));
                    break;
                case Msg.WELCOME:
                    if (Role != "client") return;
                    if (!HasSeed || Seed != m.Seed) Log("conectado ao host " + m.Name + "; semente do mundo = " + m.Seed);
                    PeerName = m.Name; Seed = m.Seed; HasSeed = true;
                    break;
                case Msg.CRIATURAS:
                    if (CreatureSync.Inst != null) CreatureSync.Inst.Receber(m);
                    break;
                case Msg.EVENT:
                    if (m.Name == "ack") { _pendingEvents.Remove(m.Seq); return; }
                    Net.Send(Msg.Event(m.Seq, "ack", ""));
                    if (!_seenEvents.Add(m.Seq)) return;
                    if (!m.Name.StartsWith("mundo-") && m.Name != "ping" && m.Name != "pong") Log("evento recebido: " + m.Name + " " + (m.Text.Length > 300 ? m.Text.Substring(0, 300) + "..." : m.Text));
                    if (GameEvent != null) GameEvent(m.Name, m.Text);
                    break;
                case Msg.STATE:
                    PeerScene = m.Scene;
                    if (m.WorldHash != RemoteWorldHash)
                    {
                        RemoteWorldHash = m.WorldHash;
                        Log("hash do mundo remoto = " + RemoteWorldHash + " (local = " + LocalWorldHash + ")" +
                            (RemoteWorldHash == LocalWorldHash ? " -> IGUAL" : " -> DIFERENTE"));
                    }
                    foreach (string k in m.Kills) ApplyRemoteKill(k);
                    UpdateGhost(m);
                    break;
            }
        }

        // ---- Eventos de jogo confiaveis: reenvia a cada 0,25 s ate o outro lado confirmar ("ack").
        public event Action<string, string> GameEvent;
        readonly Dictionary<uint, Msg> _pendingEvents = new Dictionary<uint, Msg>();
        readonly HashSet<uint> _seenEvents = new HashSet<uint>();
        uint _nextEventId = (uint)new System.Random().Next(1, 1 << 30);
        float _nextEventResend;

        public void SendEvent(string name, string text)
        {
            if (Net == null) return;
            var m = Msg.Event(++_nextEventId, name, text);
            _pendingEvents[m.Seq] = m;
            Net.Send(m);
            if (!name.StartsWith("mundo-") && name != "ping" && name != "pong") Log("evento enviado: " + name + " " + (text.Length > 300 ? text.Substring(0, 300) + "..." : text));
        }

        void ResendEvents(float now)
        {
            if (_pendingEvents.Count == 0 || now < _nextEventResend) return;
            _nextEventResend = now + 0.25f;
            foreach (Msg m in _pendingEvents.Values) Net.Send(m);
        }

        void SendState()
        {
            var m = new Msg();
            m.Type = Msg.STATE;
            m.Seq = ++_seq;
            m.Scene = SceneManager.GetActiveScene().name;
            m.WorldHash = LocalWorldHash;
            m.Kills = KillsToSend.Count > 32 ? KillsToSend.GetRange(KillsToSend.Count - 32, 32) : KillsToSend;
            MainChar mc = MainChar.Instance;
            if (mc != null && mc.isActiveAndEnabled)
            {
                m.HasHero = true;
                m.Pos = mc.transform.position;
                m.Rot = mc.transform.rotation;
                Animator an = mc.GetComponent<Animator>();
                if (an != null) AnimSnapshot.Capture(an, m);
            }
            Net.Send(m);
        }

        // ---- Aparencia: envia a receita do proprio visual quando muda; recebe a do outro e remonta o fantasma.
        public Aparencia RemoteAparencia;
        string _apEnviada, _apRemotaTexto;
        float _nextAparencia, _nextPedirAparencia;

        void TickAparencia(float now)
        {
            if (now < _nextAparencia || MainChar.Instance == null || !MainChar.Instance.isActiveAndEnabled) return;
            _nextAparencia = now + 1f;
            Aparencia a = null;
            try { a = Aparencia.DoHeroiLocal(); } catch (Exception e) { Log("aparencia: falha ao descrever o heroi local: " + e.Message); }
            if (a == null) return;
            string assinatura = a.Assinatura();
            if (assinatura == _apEnviada) return;
            _apEnviada = assinatura;
            SendEvent("aparencia", a.Serializar());
        }

        void OnEventoAparencia(string nome, string texto)
        {
            if (nome == "pedir-aparencia") { _apEnviada = null; _nextAparencia = 0; return; }
            if (nome != "aparencia") return;
            try
            {
                Aparencia nova = Aparencia.Ler(texto);
                if (RemoteAparencia != null && nova.Assinatura() == _apRemotaTexto) return;
                RemoteAparencia = nova;
                _apRemotaTexto = nova.Assinatura();
                Log("aparencia do outro jogador: " + RemoteAparencia.Resumo());
                if (Ghost != null) { Ghost.Destroy(); Ghost = null; } // remonta no proximo STATE
            }
            catch (Exception e) { Log("aparencia: texto invalido: " + e.Message); }
        }

        void LateUpdate() { if (Ghost != null) Ghost.LateTick(); }

        void UpdateGhost(Msg m)
        {
            bool sameScene = m.Scene == SceneManager.GetActiveScene().name;
            // Na cama cada jogador ve so o proprio heroi.
            bool naCama = UI.UserInterface.Instance != null && UI.UserInterface.Instance.SleepingScreen != null &&
                          UI.UserInterface.Instance.SleepingScreen.gameObject.activeSelf;
            if (!m.HasHero || !sameScene || MainChar.Instance == null || naCama)
            {
                if (Ghost != null) Ghost.SetVisible(false);
                return;
            }
            if (Ghost == null)
            {
                // Espera a "receita" do visual do outro jogador antes de montar o fantasma.
                if (RemoteAparencia == null)
                {
                    if (Time.realtimeSinceStartup > _nextPedirAparencia) { _nextPedirAparencia = Time.realtimeSinceStartup + 3f; SendEvent("pedir-aparencia", ""); }
                    return;
                }
                Ghost = Ghost.Create(MainChar.Instance.gameObject, PeerName, RemoteAparencia);
                if (Ghost == null) return;
                Log("fantasma do outro jogador criado na cena " + m.Scene + " (" + RemoteAparencia.Resumo() + ")");
            }
            Ghost.SetVisible(true);
            Ghost.Apply(m);
            if (Time.realtimeSinceStartup > _nextGhostLog)
            {
                _nextGhostLog = Time.realtimeSinceStartup + 5f;
                Log("fantasma: alvo " + m.Pos + " estado camada0 " + (m.LayerStates.Count > 0 ? m.LayerStates[0] : 0) + " params " + m.AnimFloats.Count);
            }
        }

        public void OnLocalCreatureDeath(Creature c)
        {
            string key = Registry.KeyOf(c);
            if (key == null || KnownKills.Contains(key)) return;
            KnownKills.Add(key);
            KillsToSend.Add(key);
            Log("criatura morta localmente: " + key);
        }

        void ApplyRemoteKill(string key)
        {
            if (KnownKills.Contains(key)) return;
            KnownKills.Add(key);
            if (Registry.Find(key) == null) { Log("morte remota sem criatura correspondente: " + key); return; }
            if (!TryKill(key)) { PendingKills.Add(key); Log("morte remota pendente (criatura inativa): " + key); }
        }

        public readonly List<string> PendingKills = new List<string>();

        bool TryKill(string key)
        {
            Creature c = Registry.Find(key);
            if (c == null || c.IsDead) return true;
            if (CreatureSync.Inst != null) CreatureSync.Inst.LiberarCriatura(c); // uma copia volta ao normal para morrer
            if (!c.isActiveAndEnabled) return false;
            Patches.ApplyingRemote = true;
            try { c.Death(true, false, false); }
            catch (Exception e) { Log("falha ao aplicar morte remota " + key + ": " + e.Message); }
            finally { Patches.ApplyingRemote = false; }
            if (!c.IsDead) return false;
            RemoteKillsApplied++;
            Log("morte remota aplicada: " + key);
            return true;
        }

        void RetryPendingKills()
        {
            for (int i = PendingKills.Count - 1; i >= 0; i--)
                if (TryKill(PendingKills[i])) PendingKills.RemoveAt(i);
        }

        // Somente para testes automatizados: atravessa o menu inicial e a selecao de personagem.
        void AutoStart()
        {
            if (Role == "client" && !HasSeed) return;
            string scene = SceneManager.GetActiveScene().name;
            if (_autoSteps > 60) return;
            // Telas de carregamento esperam "espaco para continuar".
            foreach (Type t in new[] { typeof(UI.Screens.NewGameLoadingScreen), typeof(UI.Screens.LoadingScreen) })
            {
                // Mesmo criterio do jogo (OnSpace testa gameObject.activeSelf); inclui objetos/componentes desligados.
                foreach (UnityEngine.Object o in Resources.FindObjectsOfTypeAll(t))
                {
                    var mb = (MonoBehaviour)o;
                    if (!mb.gameObject.scene.IsValid() || !mb.gameObject.activeInHierarchy) continue;
                    _autoSteps++; Log("auto: espaco em " + t.Name);
                    AccessTools.Method(t, "OnSpace").Invoke(mb, null);
                    return;
                }
            }
            if (scene == "StartMenu")
            {
                var sm = FindObjectOfType<UI.Screens.StartMenu>();
                if (sm != null && sm.NewGameButton != null && sm.NewGameButton.interactable) { _autoSteps++; Log("auto: Novo jogo"); sm.NewGame(); }
            }
            else if (scene == "CharacterSelection")
            {
                var cs = UI.Screens.CharacterSelection.Instance;
                string classe = Arg("-coop-classe", "");
                if (cs != null && cs.isActiveAndEnabled && classe != "" && !_classeEscolhida && cs.CharClassToggleGroup != null)
                {
                    var disponiveis = new List<string>();
                    foreach (UnityEngine.UI.Toggle t in cs.CharClassToggleGroup.GetComponentsInChildren<UnityEngine.UI.Toggle>(true))
                    {
                        UI.CharClassToggle ct = t.GetComponent<UI.CharClassToggle>();
                        UI.CharacterPreset p = ct != null ? ct.CharacterPreset : null;
                        if (p == null) continue;
                        disponiveis.Add(p.Class.ToString());
                        if (string.Equals(p.Class.ToString(), classe, StringComparison.OrdinalIgnoreCase)) { t.isOn = true; _classeEscolhida = true; }
                    }
                    Log("auto: classe " + classe + (_classeEscolhida ? " escolhida" : " nao encontrada") + " (disponiveis: " + string.Join(", ", disponiveis.ToArray()) + ")");
                    _classeEscolhida = true;
                    return;
                }
                if (cs != null && cs.isActiveAndEnabled) { _autoSteps++; Log("auto: avancar selecao de personagem"); AccessTools.Method(typeof(UI.Screens.CharacterSelection), "OnStartGame").Invoke(cs, null); }
            }
        }

        // Somente para testes automatizados: le comandos de BepInEx\SparkCoop-<role>.cmd
        //   kill-nearest      mata a criatura viva mais proxima do heroi local
        //   move <dx> <dz>    desloca o heroi local (teleporte curto) para testar o fantasma
        void TestCommands()
        {
            string f = Path.Combine(Paths.BepInExRootPath, "SparkCoop-" + Role + ".cmd");
            if (!File.Exists(f) || MainChar.Instance == null) return;
            string[] lines = File.ReadAllLines(f);
            File.Delete(f);
            foreach (string line in lines)
            {
                string[] a = line.Trim().Split(' ');
                if (a[0] == "kill-nearest")
                {
                    Creature best = null; float bd = float.MaxValue;
                    foreach (Creature c in FindObjectsOfType<Creature>())
                    {
                        if (c.IsDead || !c.isActiveAndEnabled || Registry.KeyOf(c) == null) continue;
                        float d = (c.transform.position - MainChar.Instance.transform.position).sqrMagnitude;
                        if (d < bd) { bd = d; best = c; }
                    }
                    if (best != null) { Log("teste: matando " + Registry.KeyOf(best) + " a " + Mathf.Sqrt(bd).ToString("0.0") + " m"); best.Death(false, false, false); Log("teste: IsDead=" + best.IsDead); }
                }
                else if (a[0] == "goto-key" && a.Length == 2)
                {
                    Creature c = Registry.Find(a[1]);
                    if (c != null) { MainChar.Instance.transform.position = c.transform.position + Vector3.right * 2f; Log("teste: heroi levado ate " + a[1]); }
                }
                else if (a[0] == "kill-key" && a.Length == 2)
                {
                    Creature c = Registry.Find(a[1]);
                    if (c != null) { c.Death(false, false, false); Log("teste: kill-key " + a[1] + " ativo=" + c.isActiveAndEnabled + " IsDead=" + c.IsDead); }
                }
                else if (a[0] == "list-near")
                {
                    var l = new List<KeyValuePair<float, string>>();
                    foreach (Creature c in FindObjectsOfType<Creature>(true))
                    {
                        string k = Registry.KeyOf(c); if (k == null) continue;
                        l.Add(new KeyValuePair<float, string>((c.transform.position - MainChar.Instance.transform.position).magnitude, k + " ativo=" + c.isActiveAndEnabled + " morto=" + c.IsDead));
                    }
                    l.Sort((x, y) => x.Key.CompareTo(y.Key));
                    for (int i = 0; i < Math.Min(6, l.Count); i++) Log("teste: " + l[i].Key.ToString("0.0") + " m " + l[i].Value);
                }
                else if (a[0] == "walk" && a.Length == 4)
                {
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    _walk = new Vector2(float.Parse(a[1], ci), float.Parse(a[2], ci));
                    _walkUntil = Time.realtimeSinceStartup + float.Parse(a[3], ci);
                    Log("teste: andando " + _walk + " por " + a[3] + " s");
                }
                else if (a[0] == "die" && a.Length == 2)
                {
                    // die corpo|abismo: mata o heroi local pelo caminho normal do jogo (MainChar.Death).
                    MainChar.Instance.Death(a[1] == "abismo" ? MainChar.DeathType.DeadFromAbyss : MainChar.DeathType.DeadFromCreatures, false);
                    Log("teste: heroi local morreu (" + a[1] + ")");
                }
                else if (a[0] == "cair")
                {
                    // Joga o heroi para baixo do chao: o proprio jogo detecta a queda no abismo (MainChar.Update).
                    MainChar.Instance.transform.position += Vector3.down * 6f;
                    Log("teste: heroi jogado para baixo do chao em " + MainChar.Instance.transform.position);
                }
                else if (a[0] == "goto-ghost" && Ghost != null)
                {
                    MainChar.Instance.transform.position = Ghost.Position + Vector3.right * 1.2f;
                    Log("teste: heroi levado ate o fantasma");
                }
                else if (a[0] == "hold-revive" && a.Length == 2)
                {
                    CoopVida.TesteSegurarAte = Time.realtimeSinceStartup + float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture);
                    Log("teste: segurando E por " + a[1] + " s");
                }
                else if (a[0] == "rest")
                {
                    Actions.SleepingPlace best = null; float bd = float.MaxValue;
                    foreach (var sp in FindObjectsOfType<Actions.SleepingPlace>())
                    {
                        float d = (sp.transform.position - MainChar.Instance.transform.position).sqrMagnitude;
                        if (d < bd) { bd = d; best = sp; }
                    }
                    if (best != null) { UI.UserInterface.Instance.SleepingScreen.Show(best, false); Log("teste: descansando na cama a " + Mathf.Sqrt(bd).ToString("0") + " m"); }
                    else Log("teste: nenhuma cama na cena");
                }
                else if (a[0] == "end-rest")
                {
                    UI.UserInterface.Instance.SleepingScreen.HideScreen();
                    Log("teste: botao de sair da cama");
                }
                else if (a[0] == "status")
                {
                    var v = CoopVida.Inst; var ch = MainChar.Instance.Character; var hp = ch.GetSpec(CharSpecType.Health);
                    Log("status: caido=" + v.LocalCaido + " abismo=" + v.LocalAbismo + " amigoCaido=" + v.AmigoCaido + " pronto=" + v.LocalPronto + "/" + v.AmigoPronto +
                        " morto=" + ch.IsDead + " vida=" + hp.CurrentValue.ToString("0") + "/" + hp.MaxValue.ToString("0") + " envenenado=" + ch.IsPoisoned() +
                        " cama=" + UI.UserInterface.Instance.SleepingScreen.gameObject.activeSelf + " dia=" + Game.GetCurrentDayNumber() + " pos=" + MainChar.Instance.transform.position +
                        " luz=" + (MainChar.Instance.DefaultPointLight != null && MainChar.Instance.DefaultPointLight.enabled) +
                        " estado0=" + MainChar.Instance.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).fullPathHash + " animator=" + MainChar.Instance.GetComponent<Animator>().enabled);
                }
                else if (a[0] == "criatura" && a.Length == 2)
                {
                    Log("teste: " + CreatureSync.Inst.Descrever(a[1]));
                }
                else if (a[0] == "hit-key" && a.Length == 3)
                {
                    // Simula um golpe local: tira vida da criatura deste jogo (se for copia, o golpe vai ao dono).
                    Creature c = Registry.Find(a[1]);
                    // Usa Damaged(float), o mesmo caminho de dano do jogo, para passar pela deteccao de golpes.
                    if (c != null) { c.Damaged(float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture)); Log("teste: golpe local em " + a[1] + ", vida " + c.Character.GetSpec(CharSpecType.Health).CurrentValue.ToString("0.#")); }
                }
                else if (a[0] == "ghost-diag")
                {
                    if (Ghost != null) Ghost.Diag(); else Log("diag: sem fantasma");
                    Ghost.DiagBones(MainChar.Instance.gameObject, "local");
                    Animator la = MainChar.Instance.GetComponent<Animator>();
                    Log("diag local: init=" + la.isInitialized + " playables=" + la.hasBoundPlayables + " speed=" + la.speed + " update=" + la.updateMode +
                        " timeScale=" + Time.timeScale + " pausado=" + Game.IsPaused + " foco=" + Application.isFocused + " t0=" + la.GetCurrentAnimatorStateInfo(0).normalizedTime.ToString("0.00"));
                }
                else if (a[0] == "move" && a.Length == 3)
                {
                    Vector3 d = new Vector3(float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture), 0, float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture));
                    MainChar.Instance.transform.position += d;
                    Log("teste: heroi movido para " + MainChar.Instance.transform.position);
                }
            }
        }

        void OnGUI()
        {
            if (Net == null || !CfgHud.Value) return;
            float age = Time.realtimeSinceStartup - LastRecv;
            string st = Net.HasPeer && age < 3f ? L.T("conectado a ", "connected to ") + PeerName : Net.StatusText;
            string world = LocalWorldHash == "" ? "-" : (RemoteWorldHash == "" ? LocalWorldHash + L.T(" (remoto ?)", " (remote ?)") : (LocalWorldHash == RemoteWorldHash ? L.T("IGUAL ", "SAME ") + LocalWorldHash : L.T("DIFERENTE ", "DIFFERENT ") + LocalWorldHash + " / " + RemoteWorldHash));
            string txt = "SparkCoop " + Version + " [" + Role + "] " + st +
                         L.T("\nsemente: ", "\nseed: ") + (HasSeed ? Seed.ToString() : "?") + L.T("   mundo: ", "   world: ") + world +
                         L.T("\ncena remota: ", "\nremote scene: ") + PeerScene + L.T("   mortes enviadas: ", "   kills sent: ") + KillsToSend.Count + L.T("   recebidas: ", "   received: ") + RemoteKillsApplied +
                         (CreatureSync.Inst != null ? "\n" + CreatureSync.Inst.Resumo() : "") +
                         (Dificuldade.Inst != null && Dificuldade.Inst.BonusAtual > 0 ? L.T("   vida dos monstros: +", "   monster health: +") + Dificuldade.Inst.BonusAtual + "%" : "");
            GUI.Box(new Rect(10, 10, 560, 78), "");
            GUI.Label(new Rect(18, 14, 550, 76), txt);
            if (Ghost != null) Ghost.DrawLabel();
        }
    }

    // ---------------------------------------------------------------- Patches
    public static class Patches
    {
        public static bool ApplyingRemote;

        // Semeia os geradores aleatorios do jogo antes de gerar o nivel, para os dois lados gerarem o mesmo mundo.
        [HarmonyPrefix, HarmonyPatch(typeof(LevelManager), "LevelGeneration")]
        static void SeedBeforeGeneration()
        {
            var p = Plugin.Inst;
            if (p == null || !p.HasSeed) { if (p != null) p.Log("AVISO: nivel gerado sem semente (cliente ainda nao conectado)"); return; }
            int s = p.Seed ^ (Game.GetLevelNumber() * 7919);
            Semear(s);
            p.Log("geracao do nivel " + Game.GetLevelNumber() + " semeada com " + s);
        }

        // Game.Random e "static readonly": o JIT do Mono grava o endereco do objeto no codigo do jogo. Trocar o objeto
        // deixa o codigo apontando para lixo (crash nativo). Por isso o objeto continua o mesmo e so o ESTADO interno
        // e copiado de um System.Random novo com a semente desejada.
        static readonly System.Reflection.FieldInfo[] CamposRandom =
            typeof(System.Random).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        public static void Semear(int s)
        {
            UnityEngine.Random.InitState(s);
            var atual = (System.Random)AccessTools.Field(typeof(Game), "Random").GetValue(null);
            var novo = new System.Random(s);
            foreach (var f in CamposRandom)
            {
                object v = f.GetValue(novo);
                var arr = v as Array;
                var dest = f.GetValue(atual) as Array;
                if (arr != null && dest != null && dest.Length == arr.Length) Array.Copy(arr, dest, arr.Length);
                else f.SetValue(atual, v);
            }
        }

        // Etapas do gerador que definem criaturas e o mapa. Cada uma recebe semente propria antes de rodar, para nao
        // depender de quanto aleatorio as etapas anteriores consumiram (isso muda com a classe e o progresso do heroi).
        public static readonly string[] EtapasDoMundo = {
            "GenerateCreaturesPopulation", "BoostCreaturesConstitutionByTier", "GeneratePowerfulCorpses", "GeneratePowerfulCruels",
            "GeneratePowerfulSpiders", "GeneratePowerfulObservers", "GenerateRats", "GenerateSmallSpiders", "GenerateBats",
            "GenerateScolopendras", "BalanceCreaturesPopulation", "BalanceSpiderInsideItems", "GenerateRandomGrills",
            "GenerateRandomRopes", "GenerateLibraryTower", "GenerateTrapZones", "GenerateTraps", "GenerateLocks",
            "RandomizeOrdinaryDoorsState" };

        public static int PatchEtapasDoMundo(Harmony h)
        {
            var prefixo = new HarmonyMethod(typeof(Patches).GetMethod("SemearEtapa", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
            int n = 0;
            foreach (string nome in EtapasDoMundo)
            {
                var m = AccessTools.Method(typeof(Generator), nome);
                if (m == null) { Plugin.Inst.Log("aviso: etapa do gerador nao encontrada: " + nome); continue; }
                h.Patch(m, prefixo); n++;
            }
            return n;
        }

        static void SemearEtapa(System.Reflection.MethodBase __originalMethod)
        {
            var p = Plugin.Inst;
            if (p == null || !p.HasSeed) return;
            uint hash = 2166136261;
            foreach (char ch in __originalMethod.Name) { hash ^= ch; hash *= 16777619; }
            int s = p.Seed ^ (Game.GetLevelNumber() * 7919) ^ (int)hash;
            Semear(s);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(LevelManager), "LevelGeneration")]
        static void AfterGeneration()
        {
            var p = Plugin.Inst;
            if (p == null || p.Net == null) return;
            p.LocalWorldHash = p.Registry.Build();
            p.KillsToSend.Clear(); p.KnownKills.Clear(); p.PendingKills.Clear(); p.RemoteKillsApplied = 0;
            p.Log("mundo gerado: " + p.Registry.Count + " criaturas, hash " + p.LocalWorldHash);
        }

        // Modo de teste (-coop-nosave 1): nao grava saves nem sincroniza com a Steam Cloud.
        [HarmonyPrefix, HarmonyPatch(typeof(SaveGameSystem), "SaveGame")]
        static bool BlockSave()
        {
            if (Plugin.Inst != null && Plugin.Inst.NoSave) { Plugin.Inst.Log("save bloqueado (modo de teste)"); return false; }
            return true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(SaveGameSystem), "SaveGame")]
        static void AfterSave()
        {
            if (Plugin.Inst != null && !Plugin.Inst.NoSave)
                Plugin.Inst.Log("jogo salvo: " + SaveGameSystem.CurrentSaveFile + " (nivel " + Game.GetLevelNumber() + ")");
        }

        [HarmonyPrefix, HarmonyPatch(typeof(SaveGameSystem), "LoadGame")]
        static void BeforeLoad(bool loadRecent)
        {
            if (Plugin.Inst != null) Plugin.Inst.Log("carregando save: " + SaveGameSystem.CurrentSaveFile + (loadRecent ? " (mais recente)" : ""));
        }

        // -coop-nosave 1 ou -coop-nocloud 1: nada vai para a Steam Cloud nem vem dela (protege os saves reais na nuvem).
        [HarmonyPrefix, HarmonyPatch(typeof(SaveGameSystem), "SyncLocalAndCloudSaveFiles")]
        static bool BlockCloud() { return Plugin.Inst == null || !Plugin.Inst.NoCloud; }

        [HarmonyPrefix, HarmonyPatch(typeof(SaveGameSystem), "EnsureSteamCloudSync")]
        static bool BlockCloudEnsure() { return Plugin.Inst == null || !Plugin.Inst.NoCloud; }

        [HarmonyPrefix, HarmonyPatch(typeof(SaveGameSystem), "TryUploadLocalSaveToCloud")]
        static bool BlockCloudUpload(ref bool __result)
        {
            if (Plugin.Inst == null || !Plugin.Inst.NoCloud) return true;
            __result = false; return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(SaveGameSystem), "TryDeleteCloudSave")]
        static bool BlockCloudDelete(ref bool __result)
        {
            if (Plugin.Inst == null || !Plugin.Inst.NoCloud) return true;
            __result = false; return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Creature), "Death")]
        static void AfterDeath(Creature __instance)
        {
            // Death() retorna cedo (sem matar) quando o Animator nao esta pronto; so conta morte real.
            if (ApplyingRemote || Plugin.Inst == null || Plugin.Inst.Net == null || !__instance.IsDead) return;
            Plugin.Inst.OnLocalCreatureDeath(__instance);
        }
    }

    // ---------------------------------------------------------------- Criaturas
    // Chave estavel por criatura: familia, rank e posicao logo apos a geracao (igual nos dois lados se a semente bater).
    public class CreatureRegistry
    {
        readonly Dictionary<string, Creature> _byKey = new Dictionary<string, Creature>();
        readonly Dictionary<int, string> _byInstance = new Dictionary<int, string>();
        // Indice numerico = posicao na lista ordenada de chaves: igual nos dois jogos quando o hash do mundo bate.
        public readonly List<Creature> PorIndice = new List<Creature>();
        public readonly List<string> Chaves = new List<string>();
        readonly Dictionary<int, int> _indice = new Dictionary<int, int>();
        public int Count { get { return _byKey.Count; } }

        public int IndexOf(Creature c) { int i; return c != null && _indice.TryGetValue(c.GetInstanceID(), out i) ? i : -1; }

        // porId: mundo carregado de save - a chave e o ID de save do jogo (igual nos dois quando o amigo carregou o mundo do
        // host); as posicoes ja mudaram desde a geracao. Sem ID, cai na chave por posicao.
        public string Build(bool porId = false)
        {
            _byKey.Clear(); _byInstance.Clear(); PorIndice.Clear(); Chaves.Clear(); _indice.Clear();
            var list = new List<KeyValuePair<string, Creature>>();
            foreach (Creature c in UnityEngine.Object.FindObjectsOfType<Creature>(true))
            {
                Vector3 v = c.transform.position;
                string k = c.FamilyId + "/" + c.Rank + "@" + Mathf.RoundToInt(v.x * 2) + "," + Mathf.RoundToInt(v.y * 2) + "," + Mathf.RoundToInt(v.z * 2);
                if (porId)
                {
                    SaveableObject so = c.GetComponentInParent<SaveableObject>();
                    string id = so != null ? so.GetUniqueID() : null;
                    if (!string.IsNullOrEmpty(id)) k = c.FamilyId + "/" + c.Rank + "#" + id;
                }
                list.Add(new KeyValuePair<string, Creature>(k, c));
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            var sb = new StringBuilder();
            foreach (var kv in list)
            {
                string k = kv.Key; int n = 1;
                while (_byKey.ContainsKey(k)) k = kv.Key + "#" + (++n);
                _byKey[k] = kv.Value;
                _byInstance[kv.Value.GetInstanceID()] = k;
                _indice[kv.Value.GetInstanceID()] = PorIndice.Count;
                PorIndice.Add(kv.Value); Chaves.Add(k);
                sb.Append(k).Append(';');
            }
            return Fnv(sb.ToString()).ToString("x8");
        }

        public string KeyOf(Creature c)
        {
            string k;
            return _byInstance.TryGetValue(c.GetInstanceID(), out k) ? k : null;
        }

        public Creature Find(string key)
        {
            Creature c;
            return _byKey.TryGetValue(key, out c) && c != null ? c : null;
        }

        static uint Fnv(string s)
        {
            uint h = 2166136261;
            foreach (char ch in s) { h ^= ch; h *= 16777619; }
            return h;
        }
    }

    // ---------------------------------------------------------------- Fantasma (heroi remoto)
    public class Ghost
    {
        GameObject _go;
        Animator _anim;
        string _name;
        Vector3 _targetPos;
        Quaternion _targetRot;
        bool _hasTarget;
        readonly int[] _lastState = new int[8];

        UI.CharacterClass _classe;
        Transform _ossos;
        readonly HeroBodyShape.State _formato = new HeroBodyShape.State();

        public static Ghost Create(GameObject source, string name, Aparencia aparencia)
        {
            // Instanciar sob um pai inativo impede Awake/OnEnable dos scripts do jogo na copia
            // (MainChar.Awake sobrescreveria MainChar.Instance).
            var holder = new GameObject("SparkCoop_GhostHolder");
            holder.SetActive(false);
            GameObject copy = UnityEngine.Object.Instantiate(source, holder.transform);
            copy.name = NomeRaiz;
            var g = new Ghost();
            MainChar cm = copy.GetComponent<MainChar>();
            if (aparencia != null)
            {
                try { aparencia.AplicarNoClone(copy); }
                catch (Exception e) { Plugin.Inst.Log("aparencia: falha ao montar: " + e); }
            }
            g._classe = cm != null ? cm.Class : UI.CharacterClass.None;
            g._ossos = cm != null ? cm.BonesRoot : null;
            Strip(copy);
            HideDuplicateSkeletons(copy);
            copy.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(holder);
            g._go = copy;
            g._name = name;
            g._anim = copy.GetComponent<Animator>();
            if (g._anim != null) { g._anim.applyRootMotion = false; g._anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; }
            // O jogo e muito escuro e as luzes da copia foram removidas: o fantasma leva a propria "lamparina".
            var lampObj = new GameObject("SparkCoop_GhostLamp");
            lampObj.transform.SetParent(copy.transform, false);
            lampObj.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            Light lamp = lampObj.AddComponent<Light>();
            lamp.type = LightType.Point; lamp.range = 6f; lamp.intensity = 1.6f; lamp.color = new Color(1f, 0.78f, 0.5f);
            copy.SetActive(true);
            if (g._anim != null) { g._anim.enabled = true; g._anim.Rebind(); g._anim.Update(0f); }
            Animator[] all = copy.GetComponentsInChildren<Animator>(true);
            Plugin.Inst.Log("fantasma: " + copy.GetComponentsInChildren<Renderer>(true).Length + " renderers, " + all.Length + " animators; raiz=" +
                (g._anim == null ? "sem animator" : (g._anim.runtimeAnimatorController == null ? "sem controller" : g._anim.runtimeAnimatorController.name + " avatar=" + (g._anim.avatar != null) + " camadas=" + g._anim.layerCount)));
            return g;
        }

        static void Strip(GameObject root)
        {
            var tipos = new SortedDictionary<string, int>();
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is Animator || c is Renderer || c is MeshFilter) continue;
                string n = c.GetType().FullName; int k; tipos.TryGetValue(n, out k); tipos[n] = k + 1;
            }
            var sb = new StringBuilder("fantasma: removendo ");
            foreach (var kv in tipos) sb.Append(kv.Key).Append('x').Append(kv.Value).Append(", ");
            Plugin.Inst.Log(sb.ToString());
            // Varias passadas porque alguns componentes dependem de outros (RequireComponent).
            for (int pass = 0; pass < 4; pass++)
            {
                foreach (Component c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform || c is Animator || c is Renderer || c is MeshFilter) continue;
                    try { UnityEngine.Object.DestroyImmediate(c); } catch { }
                }
            }
        }

        public static string Path(Transform t, Transform root)
        {
            string p = t.name;
            while (t.parent != null && t != root) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        // Estrutura de ossos: o que o Animator move x o que os SkinnedMeshRenderers desenham.
        public static void DiagBones(GameObject go, string quem)
        {
            Animator an = go.GetComponent<Animator>();
            var log = Plugin.Inst;
            log.Log("ossos " + quem + ": humano=" + (an.avatar != null && an.avatar.isHuman) + " valido=" + (an.avatar != null && an.avatar.isValid));
            if (an.avatar != null && an.avatar.isHuman)
            {
                Transform hips = an.GetBoneTransform(HumanBodyBones.Hips), sp = an.GetBoneTransform(HumanBodyBones.Spine);
                log.Log("ossos " + quem + ": Hips=" + (hips == null ? "null" : Path(hips, go.transform)) + " Spine=" + (sp == null ? "null" : Path(sp, go.transform) + " rot " + sp.localRotation.eulerAngles));
            }
            int i = 0;
            foreach (SkinnedMeshRenderer r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (i++ >= 8) break;
                Transform rb = r.rootBone;
                int fora = 0;
                foreach (Transform b in r.bones) if (b == null || !b.IsChildOf(go.transform)) fora++;
                log.Log("ossos " + quem + ": SMR " + r.name + " ativo=" + r.gameObject.activeInHierarchy + " root=" + (rb == null ? "null" : Path(rb, go.transform)) +
                        " ossos=" + r.bones.Length + " fora/nulos=" + fora);
            }
        }

        // As armaduras trazem uma copia propria do esqueleto (Base/Hips/Spine1...). No heroi, MainChar religa a malha da
        // armadura ao esqueleto principal e a copia fica sem uso; mas no clone o Animator procura ossos por nome e pode
        // se ligar a copia, animando um esqueleto invisivel (resultado: T-pose). Renomeia as copias que nenhuma malha usa.
        // A copia esta sob um pai inativo; "visivel" = renderer ligado e todos os pais ate a raiz da copia ativos.
        static bool VisibleUnder(Renderer r, Transform root)
        {
            if (!r.enabled) return false;
            for (Transform t = r.transform; t != null; t = t.parent) { if (!t.gameObject.activeSelf) return false; if (t == root) break; }
            return true;
        }

        static void HideDuplicateSkeletons(GameObject root)
        {
            foreach (SkinnedMeshRenderer r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (VisibleUnder(r, root.transform))
                    Plugin.Inst.Log("fantasma: malha visivel " + Path(r.transform, root.transform) + " -> raiz " + (r.rootBone == null ? "null" : Path(r.rootBone, root.transform)));
            var usados = new HashSet<Transform>();
            foreach (SkinnedMeshRenderer r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!VisibleUnder(r, root.transform)) continue;
                if (r.rootBone != null) usados.Add(r.rootBone);
                foreach (Transform b in r.bones) if (b != null) usados.Add(b);
            }
            Transform principal = root.transform.Find("Bones/Base/Hips");
            int renomeados = 0;
            foreach (Transform hips in root.GetComponentsInChildren<Transform>(true))
            {
                if (hips.name != "Hips" || hips == principal) continue;
                Transform esq = hips.parent != null ? hips.parent : hips;
                bool emUso = false;
                foreach (Transform t in esq.GetComponentsInChildren<Transform>(true)) if (usados.Contains(t)) { emUso = true; break; }
                if (emUso) { Plugin.Inst.Log("fantasma: esqueleto em uso mantido: " + Path(esq, root.transform)); continue; }
                foreach (Transform t in esq.GetComponentsInChildren<Transform>(true)) { t.name = t.name + "_sc"; renomeados++; }
                Plugin.Inst.Log("fantasma: esqueleto duplicado desativado: " + Path(esq, root.transform));
            }
            if (principal == null) Plugin.Inst.Log("fantasma: AVISO esqueleto principal Bones/Base/Hips nao encontrado");
            Plugin.Inst.Log("fantasma: " + renomeados + " ossos duplicados renomeados");
        }

        public void Diag()
        {
            DiagBones(_go, "fantasma");
            if (_anim == null) { Plugin.Inst.Log("diag: sem animator"); return; }
            AnimatorStateInfo s = _anim.GetCurrentAnimatorStateInfo(0);
            Transform bone = null;
            foreach (Transform t in _go.GetComponentsInChildren<Transform>(true)) if (t.name.ToLowerInvariant().Contains("spine")) { bone = t; break; }
            Plugin.Inst.Log("diag: enabled=" + _anim.enabled + " ativo=" + _anim.isActiveAndEnabled + " init=" + _anim.isInitialized +
                " playables=" + _anim.hasBoundPlayables + " speed=" + _anim.speed + " update=" + _anim.updateMode + " culling=" + _anim.cullingMode +
                " estado0=" + s.fullPathHash + " t=" + s.normalizedTime.ToString("0.00") + " osso=" + (bone == null ? "?" : bone.name + " " + bone.localRotation.eulerAngles));
        }

        public void SetVisible(bool v) { if (_go != null && _go.activeSelf != v) _go.SetActive(v); }
        public bool Visible { get { return _go != null && _go.activeSelf; } }
        public const string NomeRaiz = "SparkCoop_Ghost";
        public static bool IsGhost(Animator a) { return a != null && a.gameObject.name == NomeRaiz; }
        public Vector3 Position { get { return _go != null ? _go.transform.position : Vector3.zero; } }

        public void Apply(Msg m)
        {
            _targetPos = m.Pos; _targetRot = m.Rot;
            if (!_hasTarget) { _go.transform.position = m.Pos; _go.transform.rotation = m.Rot; _hasTarget = true; }
            if (_anim == null) return;
            // Caído: o Animator do fantasma fica desligado (corpo congelado) até o outro levantar.
            bool remotoMorto = RemoteBool(m, "IsDead");
            RemotoMorto = remotoMorto;
            if (!_anim.enabled)
            {
                if (remotoMorto) return;
                _anim.enabled = true;
                for (int i = 0; i < _lastState.Length; i++) _lastState[i] = 0;
                Plugin.Inst.Log("fantasma: o outro jogador levantou; animacao religada");
            }
            AnimSnapshot.Apply(_anim, m, _lastState);
        }

        public bool RemotoMorto;
        int _idxIsDead = -2;
        bool RemoteBool(Msg m, string nome)
        {
            if (_idxIsDead == -2)
            {
                _idxIsDead = -1;
                AnimatorControllerParameter[] ps = _anim.parameters;
                for (int i = 0; i < ps.Length; i++) if (ps[i].name == nome) { _idxIsDead = i; break; }
            }
            return _idxIsDead >= 0 && _idxIsDead < m.AnimFloats.Count && m.AnimFloats[_idxIsDead] > 0.5f;
        }

        // Formato do corpo por classe (ombros), como MainChar.LateUpdate faz com o heroi.
        public void LateTick()
        {
            if (_go == null || !_go.activeSelf || _ossos == null || _classe == UI.CharacterClass.None) return;
            try { HeroBodyShape.Apply(_ossos, _classe, _formato); } catch { _classe = UI.CharacterClass.None; }
        }

        public void Tick(float dt)
        {
            if (_go == null || !_hasTarget) return;
            float t = Mathf.Clamp01(dt * 12f);
            Vector3 p = _go.transform.position;
            _go.transform.position = (p - _targetPos).sqrMagnitude > 25f ? _targetPos : Vector3.Lerp(p, _targetPos, t);
            _go.transform.rotation = Quaternion.Slerp(_go.transform.rotation, _targetRot, t);
        }

        public void DrawLabel()
        {
            if (_go == null || !_go.activeSelf || Camera.main == null) return;
            Vector3 sp = Camera.main.WorldToScreenPoint(_go.transform.position + Vector3.up * 2.1f);
            if (sp.z <= 0) return;
            GUI.Label(new Rect(sp.x - 60, Screen.height - sp.y - 10, 120, 22), "<b>" + _name + "</b>");
        }

        public void Destroy() { if (_go != null) UnityEngine.Object.Destroy(_go); _go = null; }
    }

    // Captura/aplica parametros e estados do Animator (mesmo controlador nos dois lados).
    public static class AnimSnapshot
    {
        public static void Capture(Animator an, Msg m)
        {
            AnimatorControllerParameter[] ps = an.parameters;
            m.AnimFloats = new List<float>();
            for (int i = 0; i < ps.Length; i++)
            {
                switch (ps[i].type)
                {
                    case AnimatorControllerParameterType.Float: m.AnimFloats.Add(an.GetFloat(ps[i].nameHash)); break;
                    case AnimatorControllerParameterType.Int: m.AnimFloats.Add(an.GetInteger(ps[i].nameHash)); break;
                    case AnimatorControllerParameterType.Bool: m.AnimFloats.Add(an.GetBool(ps[i].nameHash) ? 1f : 0f); break;
                    default: m.AnimFloats.Add(0f); break;
                }
            }
            m.LayerStates = new List<int>();
            m.LayerTimes = new List<float>();
            m.LayerWeights = new List<float>();
            int n = Math.Min(an.layerCount, 8);
            for (int l = 0; l < n; l++)
            {
                AnimatorStateInfo s = an.GetCurrentAnimatorStateInfo(l);
                m.LayerStates.Add(s.fullPathHash);
                m.LayerTimes.Add(s.normalizedTime);
                m.LayerWeights.Add(an.GetLayerWeight(l));
            }
        }

        public static void Apply(Animator an, Msg m, int[] last)
        {
            if (an.runtimeAnimatorController == null) return;
            AnimatorControllerParameter[] ps = an.parameters;
            int n = Math.Min(ps.Length, m.AnimFloats.Count);
            for (int i = 0; i < n; i++)
            {
                float v = m.AnimFloats[i];
                switch (ps[i].type)
                {
                    case AnimatorControllerParameterType.Float: an.SetFloat(ps[i].nameHash, v); break;
                    case AnimatorControllerParameterType.Int: an.SetInteger(ps[i].nameHash, (int)v); break;
                    case AnimatorControllerParameterType.Bool: an.SetBool(ps[i].nameHash, v > 0.5f); break;
                }
            }
            int layers = Math.Min(an.layerCount, m.LayerStates.Count);
            for (int l = 0; l < layers; l++)
            {
                an.SetLayerWeight(l, m.LayerWeights[l]);
                int h = m.LayerStates[l];
                if (h != last[l] && an.HasState(l, h)) { an.CrossFade(h, 0.1f, l, m.LayerTimes[l] % 1f); last[l] = h; }
            }
        }
    }

    // ---------------------------------------------------------------- Mensagens
    public class Msg
    {
        // EVENT = evento de jogo com nome e texto livre, entregue com confirmacao (ver Plugin.SendEvent).
        // CRIATURAS = dono e estado das criaturas sincronizadas (sem garantia de entrega, como STATE).
        public const byte HELLO = 1, WELCOME = 2, STATE = 3, EVENT = 4, CRIATURAS = 5;
        public static bool SemGarantia(byte tipo) { return tipo == STATE || tipo == CRIATURAS; }
        public List<CriaturaEstado> Criaturas = new List<CriaturaEstado>();
        const uint Magic = 0x50534B43; // "CKSP"

        public byte Type;
        public int Protocol = Plugin.Protocol;
        public string Name = "";
        public int Seed;
        public uint Seq;
        public string Scene = "", WorldHash = "";
        public bool HasHero;
        public Vector3 Pos;
        public Quaternion Rot = Quaternion.identity;
        public List<float> AnimFloats = new List<float>();
        public List<int> LayerStates = new List<int>();
        public List<float> LayerTimes = new List<float>();
        public List<float> LayerWeights = new List<float>();
        public List<string> Kills = new List<string>();
        public string Text = "";

        public static Msg Event(uint id, string name, string text) { var m = new Msg(); m.Type = EVENT; m.Seq = id; m.Name = name; m.Text = text ?? ""; return m; }

        public static Msg Hello(string name) { var m = new Msg(); m.Type = HELLO; m.Name = name; return m; }
        public static Msg Welcome(string name, int seed) { var m = new Msg(); m.Type = WELCOME; m.Name = name; m.Seed = seed; return m; }

        public byte[] Write()
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(Magic); w.Write(Type); w.Write(Protocol);
            if (Type == HELLO) w.Write(Name);
            else if (Type == WELCOME) { w.Write(Name); w.Write(Seed); }
            else if (Type == EVENT) { w.Write(Seq); w.Write(Name); w.Write(Text); }
            else if (Type == CRIATURAS) { w.Write(Seq); w.Write((ushort)Criaturas.Count); foreach (CriaturaEstado c in Criaturas) c.Write(w); }
            else
            {
                w.Write(Seq); w.Write(Scene); w.Write(WorldHash); w.Write(HasHero);
                w.Write(Pos.x); w.Write(Pos.y); w.Write(Pos.z);
                w.Write(Rot.x); w.Write(Rot.y); w.Write(Rot.z); w.Write(Rot.w);
                w.Write((byte)AnimFloats.Count); foreach (float f in AnimFloats) w.Write(f);
                w.Write((byte)LayerStates.Count);
                for (int i = 0; i < LayerStates.Count; i++) { w.Write(LayerStates[i]); w.Write(LayerTimes[i]); w.Write(LayerWeights[i]); }
                w.Write((byte)Kills.Count); foreach (string k in Kills) w.Write(k);
            }
            w.Flush();
            return ms.ToArray();
        }

        public static Msg Read(byte[] data)
        {
            var r = new BinaryReader(new MemoryStream(data));
            if (r.ReadUInt32() != Magic) return null;
            var m = new Msg();
            m.Type = r.ReadByte(); m.Protocol = r.ReadInt32();
            if (m.Type == HELLO) m.Name = r.ReadString();
            else if (m.Type == WELCOME) { m.Name = r.ReadString(); m.Seed = r.ReadInt32(); }
            else if (m.Type == EVENT) { m.Seq = r.ReadUInt32(); m.Name = r.ReadString(); m.Text = r.ReadString(); }
            else if (m.Type == CRIATURAS) { m.Seq = r.ReadUInt32(); int n = r.ReadUInt16(); for (int i = 0; i < n; i++) m.Criaturas.Add(CriaturaEstado.Read(r)); }
            else if (m.Type == STATE)
            {
                m.Seq = r.ReadUInt32(); m.Scene = r.ReadString(); m.WorldHash = r.ReadString(); m.HasHero = r.ReadBoolean();
                m.Pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                m.Rot = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                int nf = r.ReadByte(); for (int i = 0; i < nf; i++) m.AnimFloats.Add(r.ReadSingle());
                int nl = r.ReadByte();
                for (int i = 0; i < nl; i++) { m.LayerStates.Add(r.ReadInt32()); m.LayerTimes.Add(r.ReadSingle()); m.LayerWeights.Add(r.ReadSingle()); }
                int nk = r.ReadByte(); for (int i = 0; i < nk; i++) m.Kills.Add(r.ReadString());
            }
            else return null;
            return m;
        }
    }

    // ---------------------------------------------------------------- Transportes
    // Contrato de transporte: 1 host + 1 cliente por enquanto. Implementacoes: Net (UDP direto) e SteamTransport.
    public interface ITransport
    {
        bool HasPeer { get; }
        string PeerEndPoint { get; }   // texto para logs
        string StatusText { get; }     // texto curto para o HUD quando ainda nao conectado
        bool TryReceive(out Msg m);    // chamado no thread principal
        void Send(Msg m);
        void Close();
    }

    public class Net : ITransport
    {
        public string StatusText { get { return _isHost ? L.T("aguardando cliente na porta ", "waiting for client on port ") + Port : L.T("procurando host ", "looking for host ") + HostText; } }
        readonly UdpClient _udp;
        readonly bool _isHost;
        readonly Queue<Msg> _inbox = new Queue<Msg>();
        readonly Thread _thread;
        volatile bool _running = true;
        IPEndPoint _peer;
        public readonly int Port;
        public readonly string HostText;

        public bool HasPeer { get { return _peer != null; } }
        public string PeerEndPoint { get { return _peer == null ? "?" : _peer.ToString(); } }

        public Net(bool isHost, string host, int port)
        {
            _isHost = isHost; Port = port; HostText = host + ":" + port;
            if (isHost) _udp = new UdpClient(port);
            else
            {
                _udp = new UdpClient(0);
                IPAddress ip;
                if (!IPAddress.TryParse(host, out ip)) ip = Dns.GetHostAddresses(host)[0];
                _peer = null;
                _hostEp = new IPEndPoint(ip, port);
            }
            // Evita que um ICMP "porta inalcancavel" derrube o socket no Windows.
            try { _udp.Client.IOControl(-1744830452, new byte[] { 0 }, null); } catch { }
            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Start();
        }

        readonly IPEndPoint _hostEp;

        void Loop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _udp.Receive(ref from);
                    Msg m = Msg.Read(data);
                    if (m == null) continue;
                    if (_isHost) { if (m.Type == Msg.HELLO || (_peer != null && from.Equals(_peer))) _peer = from; else continue; }
                    else { if (!from.Equals(_hostEp)) continue; _peer = from; }
                    lock (_inbox) { if (_inbox.Count < 256) _inbox.Enqueue(m); }
                }
                catch (SocketException) { }
                catch (ObjectDisposedException) { return; }
                catch (Exception) { }
            }
        }

        public bool TryReceive(out Msg m)
        {
            lock (_inbox)
            {
                if (_inbox.Count == 0) { m = null; return false; }
                m = _inbox.Dequeue(); return true;
            }
        }

        public void Send(Msg m)
        {
            IPEndPoint to = _isHost ? _peer : _hostEp;
            if (to == null) return;
            byte[] b = m.Write();
            try { _udp.Send(b, b.Length, to); } catch (Exception) { }
        }

        public void Close() { _running = false; try { _udp.Close(); } catch { } }
    }
}
