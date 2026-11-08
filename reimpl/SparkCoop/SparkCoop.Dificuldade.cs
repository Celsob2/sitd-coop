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
// SparkCoop - vida extra dos monstros em co-op (+0%, +50%, +75% ou +100%), escolhida pelo host e enviada ao amigo.
// O jogo recalcula a vida maxima em CharacterBase.CalculateSpecs (base + Constituicao x 12); um postfix multiplica o
// resultado so para criaturas. A vida atual acompanha a troca de fator, entao mudar no meio da partida tambem vale.
// So vale com uma sessao co-op ativa; sozinho o jogo fica como sempre foi.
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Creatures;
using HarmonyLib;
using UnityEngine;

namespace SparkCoop
{
    public class Dificuldade : MonoBehaviour
    {
        public static Dificuldade Inst;
        public static readonly int[] Opcoes = { 0, 50, 75, 100 };

        public ConfigEntry<int> CfgVidaMonstros;
        public int BonusDoHost;               // cliente: valor recebido do host
        // Queda no abismo: o heroi volta sem nenhum item (true) ou com o que tinha (false). Vale a escolha do host.
        public ConfigEntry<bool> CfgAbismoPerdeItens;
        public bool AbismoDoHost;
        bool _hadPeer;
        int _enviado = -1;
        int _enviadoAbismo = -1;

        public bool AbismoPerdeItens { get { return P != null && P.Role == "client" ? AbismoDoHost : CfgAbismoPerdeItens.Value; } }

        public void EscolherAbismo(bool perde)
        {
            CfgAbismoPerdeItens.Value = perde;
            P.Log("queda no abismo: " + (perde ? "perde os itens" : "mantem os itens"));
        }
        float _proximoLog;
        public int Ajustadas { get { int n = 0; foreach (float f in _fatorAplicado.Values) if (f > 1.001f) n++; return n; } }

        static Plugin P { get { return Plugin.Inst; } }

        static string Arg(string name)
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        void Awake()
        {
            Inst = this;
            CfgVidaMonstros = P.Config.Bind("Coop", "MonsterHealthBonus", 0,
                "Vida extra dos monstros em co-op, em %: 0 | 50 | 75 | 100. Vale o valor do host. / Extra monster health in co-op (host decides).");
            int v;
            if (int.TryParse(Arg("-coop-vida-monstros") ?? "", out v)) CfgVidaMonstros.Value = Normalizar(v);
            CfgAbismoPerdeItens = P.Config.Bind("Coop", "AbyssLoseItems", false,
                "Quem cai no abismo volta sem nenhum item (true) ou com os itens que tinha (false). Vale o valor do host. / Abyss fall: lose all items (host decides).");
            new Harmony(Plugin.Guid + ".dificuldade").PatchAll(typeof(DificuldadePatches));
            P.GameEvent += OnEvento;
            P.Log("vida dos monstros (config do host): +" + CfgVidaMonstros.Value + "%");
        }

        public static int Normalizar(int v)
        {
            int melhor = 0;
            foreach (int o in Opcoes) if (Math.Abs(o - v) < Math.Abs(melhor - v)) melhor = o;
            return melhor;
        }

        // Bonus em vigor agora (0 sem sessao co-op).
        public int BonusAtual
        {
            get
            {
                if (P == null || P.Net == null) return 0;
                return P.Role == "host" ? Normalizar(CfgVidaMonstros.Value) : BonusDoHost;
            }
        }

        public float Fator { get { return 1f + BonusAtual / 100f; } }

        // Host: botao do painel.
        public void Escolher(int bonus)
        {
            CfgVidaMonstros.Value = Normalizar(bonus);
            P.Log("vida dos monstros escolhida: +" + CfgVidaMonstros.Value + "%");
            AplicarAgora();
        }

        string _hashResumido = "";
        float _resumoEm;

        void Update()
        {
            // Resumo no log alguns segundos depois de cada nivel gerado: quantas criaturas receberam a vida extra.
            if (P != null && P.LocalWorldHash != "" && P.LocalWorldHash != _hashResumido)
            {
                if (_resumoEm == 0) _resumoEm = Time.realtimeSinceStartup + 8f;
                else if (Time.realtimeSinceStartup > _resumoEm) { _hashResumido = P.LocalWorldHash; _resumoEm = 0; Resumo(); }
            }
            if (P == null || P.Net == null) { _hadPeer = false; _enviado = -1; _enviadoAbismo = -1; return; }
            bool peer = P.Net.HasPeer;
            if (P.Role == "host" && peer && (!_hadPeer || _enviado != BonusAtual))
            {
                _enviado = BonusAtual;
                P.SendEvent("vida-monstros", _enviado.ToString());
            }
            int abismo = CfgAbismoPerdeItens.Value ? 1 : 0;
            if (P.Role == "host" && peer && (!_hadPeer || _enviadoAbismo != abismo))
            {
                _enviadoAbismo = abismo;
                P.SendEvent("abismo-itens", abismo.ToString());
            }
            _hadPeer = peer;
        }

        void OnEvento(string nome, string texto)
        {
            if (nome == "abismo-itens" && P.Role == "client")
            {
                AbismoDoHost = texto == "1";
                P.Log("queda no abismo definida pelo host: " + (AbismoDoHost ? "perde os itens" : "mantem os itens"));
                return;
            }
            if (nome != "vida-monstros" || P.Role != "client") return;
            int v; if (!int.TryParse(texto, out v)) return;
            if (Normalizar(v) == BonusDoHost) return;
            BonusDoHost = Normalizar(v);
            P.Log("vida dos monstros definida pelo host: +" + BonusDoHost + "%");
            AplicarAgora();
        }

        // Recalcula todas as criaturas ja existentes (troca de fator no meio da partida).
        public void AplicarAgora()
        {
            var calc = AccessTools.Method(typeof(CharacterBase), "CalculateSpecs");
            foreach (Creature c in FindObjectsOfType<Creature>(true))
                if (c != null && c.Character != null && !c.IsEnvironment && c.Character.IsInitialized)
                    try { calc.Invoke(c.Character, null); } catch (Exception) { }
        }

        // ---- chamado pelo postfix de CalculateSpecs
        readonly Dictionary<int, float> _fatorAplicado = new Dictionary<int, float>();
        readonly Dictionary<int, bool> _ehCriatura = new Dictionary<int, bool>();

        // vidaAntes = vida atual antes do CalculateSpecs original. O jogo corta a vida atual ao maximo SEM bonus durante o
        // recalculo; aqui ela volta ao que era (limitada ao maximo com bonus).
        public void Ajustar(CharacterBase ch, bool estavaInicializado, float vidaOriginal)
        {
            int id = ch.GetInstanceID();
            bool cri;
            if (!_ehCriatura.TryGetValue(id, out cri))
            {
                Creature c = ch.GetComponent<Creature>();
                cri = c != null && !c.IsEnvironment;
                _ehCriatura[id] = cri;
            }
            if (!cri) return;
            CharSpec hp = ch.GetSpec(CharSpecType.Health);
            if (hp == null) return;
            float vidaAntes = hp.CurrentValue;
            if (estavaInicializado && vidaOriginal > hp.CurrentValue + 0.01f) hp.CurrentValue = vidaOriginal;   // desfaz o corte do jogo
            float f = Fator, antes;
            bool primeiraVez = !_fatorAplicado.TryGetValue(id, out antes);
            if (primeiraVez) antes = 1f;
            float maxOriginal = hp.MaxValue;
            hp.MaxValue *= f;   // CalculateSpecs acabou de recalcular a base; o multiplicador vale sempre sobre ela
            // A vida atual so e mexida na primeira vez que a criatura passa aqui ou quando o fator muda. (Nao depender de
            // IsInitialized: confiar nele devolvia a vida cheia a cada recalculo e a criatura nunca morria.)
            if (primeiraVez || Math.Abs(antes - f) > 0.001f)
            {
                bool cheia = hp.CurrentValue >= maxOriginal * antes - 0.5f || !estavaInicializado;
                hp.CurrentValue = cheia ? hp.MaxValue : Mathf.Min(hp.MaxValue, hp.CurrentValue * f / antes);
            }
            else hp.CurrentValue = Mathf.Min(hp.MaxValue, hp.CurrentValue);
            if (Math.Abs(hp.CurrentValue - vidaAntes) > 0.5f)
                Diag.Marcar(ch, "vida extra (" + (primeiraVez ? "primeira vez" : Math.Abs(antes - f) > 0.001f ? "fator mudou" : "desfez corte do jogo") + ") " +
                                vidaAntes.ToString("0.#") + " -> " + hp.CurrentValue.ToString("0.#") + " (antes do recalculo " + vidaOriginal.ToString("0.#") + ")");
            if (primeiraVez || Math.Abs(antes - f) > 0.001f)
            {
                _fatorAplicado[id] = f;
                if (f > 1.001f && Time.realtimeSinceStartup > _proximoLog)
                {
                    _proximoLog = Time.realtimeSinceStartup + 3f;
                    P.Log("vida dos monstros +" + BonusAtual + "%: " + ch.name + " " + maxOriginal.ToString("0") + " -> " + hp.MaxValue.ToString("0") +
                          " (vida atual " + hp.CurrentValue.ToString("0") + "; ajustadas ate agora " + Ajustadas + ")");
                }
            }
        }

        void Resumo()
        {
            int total = 0;
            var exemplos = new Dictionary<string, string>();
            var semBonus = new Dictionary<string, int>();
            foreach (Creature c in FindObjectsOfType<Creature>(true))
            {
                if (c == null || c.Character == null || c.IsEnvironment) continue;
                total++;
                CharSpec hp = c.Character.GetSpec(CharSpecType.Health);
                if (hp != null && !exemplos.ContainsKey(c.FamilyId)) exemplos[c.FamilyId] = c.FamilyId + " " + hp.CurrentValue.ToString("0") + "/" + hp.MaxValue.ToString("0");
                float fa;
                if (!_fatorAplicado.TryGetValue(c.Character.GetInstanceID(), out fa) || fa < 1.001f)
                {
                    // Ainda sem bonus: normalmente criaturas desativadas (o jogo so recalcula quando ativam).
                    string k = c.FamilyId + (c.isActiveAndEnabled ? "" : " (inativa)") + (c.Character.IsInitialized ? "" : " (nao iniciada)");
                    int n; semBonus.TryGetValue(k, out n); semBonus[k] = n + 1;
                }
            }
            P.Log("vida dos monstros: bonus +" + BonusAtual + "%, " + Ajustadas + " de " + total + " criaturas com vida extra; exemplos: " +
                  string.Join(", ", new List<string>(exemplos.Values).ToArray()));
            if (BonusAtual > 0 && semBonus.Count > 0)
            {
                var l = new List<string>(); foreach (var kv in semBonus) l.Add(kv.Key + " x" + kv.Value);
                P.Log("vida dos monstros: ainda sem bonus: " + string.Join(", ", l.ToArray()));
            }
        }

        public void NovoNivel() { _fatorAplicado.Clear(); _ehCriatura.Clear(); }

        public string Rotulo(int bonus) { return bonus == 0 ? L.T("normal", "normal") : "+" + bonus + "%"; }
    }

    public static class DificuldadePatches
    {
        public class Estado { public bool Pausado, Inicializado; public float Vida; }

        // Pausado: o original sai sem recalcular; nao mexer.
        [HarmonyPrefix, HarmonyPatch(typeof(CharacterBase), "CalculateSpecs")]
        static void Antes(CharacterBase __instance, out Estado __state)
        {
            __state = new Estado();
            __state.Pausado = Game.IsPaused; __state.Inicializado = __instance.IsInitialized;
            CharSpec hp = __instance.GetSpec(CharSpecType.Health);
            __state.Vida = hp != null ? hp.CurrentValue : 0f;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CharacterBase), "CalculateSpecs")]
        static void Depois(CharacterBase __instance, Estado __state)
        {
            if (Dificuldade.Inst == null || __state == null || __state.Pausado) return;
            try { Dificuldade.Inst.Ajustar(__instance, __state.Inicializado, __state.Vida); }
            catch (Exception e) { if (Plugin.Inst != null) Plugin.Inst.Log("vida dos monstros: erro " + e.Message); }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Management.LevelManager), "LevelGeneration")]
        static void NovoNivel() { if (Dificuldade.Inst != null) Dificuldade.Inst.NovoNivel(); }
    }
}
