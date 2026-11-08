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
// SparkCoop - regras da partida definidas pelo host: dificuldade (Game.Mode), caminho (Game.Path: Sobrevivente/Explorador)
// e evento de calendario (Halloween). Elas mudam a geracao do mapa, entao precisam ser iguais nos dois jogos.
// O host envia o que escolheu (evento "regras"); no amigo, os seletores da tela de personagem ficam travados com o aviso
// "definido pelo host", os valores do host sao aplicados antes de gerar a fase, e "comecar" espera o host entrar na fase.
using System;
using HarmonyLib;
using Management;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SparkCoop
{
    public class Regras : MonoBehaviour
    {
        public static Regras Inst;
        public bool TemDoHost;
        public GameMode ModoHost; public GamePath CaminhoHost; public CalendarEvent EventoHost;
        string _enviado = "";
        bool _hadPeer;
        float _avisoAte;

        static Plugin P { get { return Plugin.Inst; } }
        public bool SouCliente { get { return P != null && P.Net != null && P.Role == "client"; } }

        void Awake()
        {
            Inst = this;
            new Harmony(Plugin.Guid + ".regras").PatchAll(typeof(RegrasPatches));
            P.GameEvent += OnEvento;
        }

        static string Atuais() { return (int)Game.Mode + ";" + (int)Game.Path + ";" + (int)Game.CalendarEvent; }

        void Update()
        {
            if (P == null || P.Net == null) { _hadPeer = false; return; }
            bool peer = P.Net.HasPeer;
            if (P.Role == "host" && peer)
            {
                string a = Atuais();
                if (!_hadPeer || a != _enviado) { _enviado = a; P.SendEvent("regras", a); }
            }
            _hadPeer = peer;
            if (SouCliente && SceneManager.GetActiveScene().name == "CharacterSelection") TravarSeletores();
        }

        void OnEvento(string nome, string texto)
        {
            if (nome != "regras" || P.Role != "client") return;
            string[] f = texto.Split(';');
            int m, c, e;
            if (f.Length != 3 || !int.TryParse(f[0], out m) || !int.TryParse(f[1], out c) || !int.TryParse(f[2], out e)) return;
            ModoHost = (GameMode)m; CaminhoHost = (GamePath)c; EventoHost = (CalendarEvent)e; TemDoHost = true;
            P.Log("regras do host: dificuldade " + ModoHost + ", caminho " + CaminhoHost + ", evento " + EventoHost);
        }

        // Cliente: aplica as regras do host nas variaveis do jogo.
        public void Aplicar(string onde)
        {
            if (!SouCliente || !TemDoHost) return;
            if (Game.Mode != ModoHost || Game.Path != CaminhoHost || Game.CalendarEvent != EventoHost)
                P.Log("regras do host aplicadas (" + onde + "): " + Game.Mode + "/" + Game.Path + "/" + Game.CalendarEvent + " -> " + ModoHost + "/" + CaminhoHost + "/" + EventoHost);
            Game.Mode = ModoHost; Game.Path = CaminhoHost; Game.CalendarEvent = EventoHost;
        }

        void TravarSeletores()
        {
            var cs = UI.Screens.CharacterSelection.Instance;
            if (cs == null) return;
            foreach (UnityEngine.UI.ToggleGroup g in new[] { cs.GameModeToggleGroup, cs.GamePathToggleGroup })
                if (g != null) foreach (UnityEngine.UI.Toggle t in g.GetComponentsInChildren<UnityEngine.UI.Toggle>(true)) t.interactable = false;
            if (cs.HelloweenModeToggle != null) cs.HelloweenModeToggle.interactable = false;
            if (cs.SurvivorModeToggle != null) cs.SurvivorModeToggle.interactable = false;
        }

        // Cliente: so comeca depois que o host ja entrou na fase (o mundo do host define a semente e as regras).
        public bool PodeComecar()
        {
            if (!SouCliente) return true;
            bool hostNaFase = P.Net.HasPeer && P.PeerScene != null && P.PeerScene.StartsWith("Level");
            if (TemDoHost && hostNaFase) { Aplicar("comecar"); return true; }
            _avisoAte = Time.realtimeSinceStartup + 4f;
            P.Log("comecar bloqueado: aguardando o host " + (TemDoHost ? "entrar na fase" : "enviar as regras"));
            return false;
        }

        void OnGUI()
        {
            if (!SouCliente || SceneManager.GetActiveScene().name != "CharacterSelection") return;
            string txt = TemDoHost
                ? L.T("Dificuldade definida pelo host: ", "Difficulty set by the host: ") + Nome(ModoHost) + " · " + Nome(CaminhoHost) +
                  (EventoHost != CalendarEvent.None ? " · " + EventoHost : "")
                : L.T("Aguardando o host definir a dificuldade...", "Waiting for the host to set the difficulty...");
            if (Time.realtimeSinceStartup < _avisoAte)
                txt += "\n" + L.T("Aguardando o host começar a partida...", "Waiting for the host to start the game...");
            var estilo = new GUIStyle(GUI.skin.box); estilo.fontSize = 18; estilo.alignment = TextAnchor.MiddleCenter; estilo.richText = true;
            GUI.Box(new Rect(Screen.width / 2f - 330, 20, 660, Time.realtimeSinceStartup < _avisoAte ? 64 : 36), txt, estilo);
        }

        static string Nome(GameMode m)
        {
            switch (m)
            {
                case GameMode.Story: return L.T("História", "Story");
                case GameMode.Normal: return L.T("Normal", "Normal");
                case GameMode.Nightmare: return L.T("Pesadelo", "Nightmare");
            }
            return m.ToString();
        }

        static string Nome(GamePath c) { return c == GamePath.Survivor ? L.T("Sobrevivente", "Survivor") : L.T("Explorador", "Explorer"); }
    }

    public static class RegrasPatches
    {
        // "Comecar" na tela de personagem do amigo espera o host.
        [HarmonyPrefix, HarmonyPatch(typeof(UI.Screens.CharacterSelection), "OnStartGame")]
        static bool AntesDeComecar() { return Regras.Inst == null || Regras.Inst.PodeComecar(); }

        [HarmonyPrefix, HarmonyPatch(typeof(UI.Screens.CharacterSelection), "ShowLoadingScreen")]
        static void AntesDoCarregamento() { if (Regras.Inst != null) Regras.Inst.Aplicar("tela de carregamento"); }

        // Rede de seguranca: a geracao da fase sempre usa as regras do host.
        [HarmonyPrefix, HarmonyPatch(typeof(LevelManager), "LevelGeneration")]
        static void AntesDeGerar() { if (Regras.Inst != null) Regras.Inst.Aplicar("geracao"); }
    }
}
