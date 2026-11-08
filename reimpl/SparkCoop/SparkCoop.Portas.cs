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
// SparkCoop - portas sincronizadas: abrir, fechar ou destrancar uma porta num jogo faz o mesmo no jogo do outro.
// Cada porta (Actions.DoorAction) e identificada pela posicao arredondada, igual nos dois mundos. Evento confiavel "porta".
// Tambem registra no log os golpes de arma em criaturas (vida antes/depois, se era copia) para investigar dessincronia.
using System;
using System.Collections.Generic;
using Actions;
using Creatures;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SparkCoop
{
    public class Portas : MonoBehaviour
    {
        public static Portas Inst;
        public static bool Aplicando;   // true enquanto aplica uma porta vinda do outro jogo (nao reenviar)
        readonly Dictionary<string, DoorAction> _porChave = new Dictionary<string, DoorAction>();
        string _cena = "";

        static Plugin P { get { return Plugin.Inst; } }

        void Awake()
        {
            Inst = this;
            var h = new Harmony(Plugin.Guid + ".portas");
            h.PatchAll(typeof(PortasPatches));
            PortasPatches.PatchGolpes(h);
            P.GameEvent += OnEvento;
        }

        public static string Chave(DoorAction d)
        {
            Vector3 v = d.transform.position;
            return "porta@" + Mathf.RoundToInt(v.x * 2) + "," + Mathf.RoundToInt(v.y * 2) + "," + Mathf.RoundToInt(v.z * 2);
        }

        DoorAction Achar(string chave)
        {
            string cena = SceneManager.GetActiveScene().name;
            DoorAction d;
            if (cena == _cena && _porChave.TryGetValue(chave, out d) && d != null) return d;
            _porChave.Clear(); _cena = cena;
            foreach (DoorAction x in FindObjectsOfType<DoorAction>(true)) _porChave[Chave(x)] = x;
            return _porChave.TryGetValue(chave, out d) ? d : null;
        }

        // So envia com sessao ativa, mundos iguais e depois do carregamento da fase (o jogo mexe nas portas ao gerar/carregar).
        public static bool PodeEnviar
        {
            get
            {
                return !Aplicando && P != null && P.Net != null && P.Net.HasPeer && P.LocalWorldHash != "" &&
                       P.LocalWorldHash == P.RemoteWorldHash && Time.timeSinceLevelLoad > 3f;
            }
        }

        public void Enviar(DoorAction d, string op)
        {
            P.SendEvent("porta", Chave(d) + ";" + op);
        }

        // Trancas (Lock) de portas e baus: destrancar num jogo destranca no outro (chave, gazua ou acao).
        public static string ChaveTranca(Lock l)
        {
            Vector3 v = l.transform.position;
            return "tranca@" + Mathf.RoundToInt(v.x * 2) + "," + Mathf.RoundToInt(v.y * 2) + "," + Mathf.RoundToInt(v.z * 2);
        }

        public void EnviarTranca(Lock l) { P.SendEvent("tranca", ChaveTranca(l)); }

        void AplicarTranca(string chave)
        {
            Lock alvo = null;
            foreach (Lock l in FindObjectsOfType<Lock>(true)) if (ChaveTranca(l) == chave) { alvo = l; break; }
            if (alvo == null) { P.Log("tranca remota sem correspondente: " + chave); return; }
            if (!alvo.IsLocked) { P.Log("tranca remota ja estava aberta: " + chave); return; }
            Aplicando = true;
            try { alvo.Unlock(false); }
            catch (Exception e) { P.Log("falha ao destrancar " + chave + ": " + e.Message); }
            finally { Aplicando = false; }
            P.Log("tranca remota aplicada: " + chave + " (" + alvo.name + ", trancada=" + alvo.IsLocked + ")");
        }

        // Estado inicial das trancas: o gerador nem sempre tranca as mesmas coisas nos dois mundos (ex.: um bau trancado
        // no jogo do guerreiro e aberto no do ladino). O host envia a lista do que esta trancado no mundo dele, uma vez
        // por fase, e o amigo destranca/tranca para ficar igual.
        string _trancasEnviadas = "";

        void Update()
        {
            if (P == null || P.Net == null || P.Role != "host" || !P.Net.HasPeer) { if (P != null && (P.Net == null || !P.Net.HasPeer)) _trancasEnviadas = ""; return; }
            if (P.LocalWorldHash == "" || P.LocalWorldHash != P.RemoteWorldHash || Time.timeSinceLevelLoad < 6f || _trancasEnviadas == P.LocalWorldHash) return;
            _trancasEnviadas = P.LocalWorldHash;
            var trancadas = new List<string>();
            int total = 0;
            foreach (Lock l in FindObjectsOfType<Lock>(true)) { total++; if (l.IsLocked) trancadas.Add(ChaveTranca(l)); }
            P.SendEvent("trancas", string.Join("|", trancadas.ToArray()));
            P.Log("trancas: enviadas " + trancadas.Count + " trancadas de " + total);
        }

        void AplicarTrancas(string texto)
        {
            var doHost = new HashSet<string>(texto.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            int abertas = 0, fechadas = 0, total = 0;
            Aplicando = true;
            try
            {
                foreach (Lock l in FindObjectsOfType<Lock>(true))
                {
                    total++;
                    bool trancadaNoHost = doHost.Contains(ChaveTranca(l));
                    if (l.IsLocked && !trancadaNoHost) { try { l.Unlock(false); abertas++; } catch (Exception e) { P.Log("trancas: falha ao destrancar " + l.name + ": " + e.Message); } }
                    else if (!l.IsLocked && trancadaNoHost) { l.IsLocked = true; fechadas++; }
                }
            }
            finally { Aplicando = false; }
            P.Log("trancas: igualadas ao host (" + total + " trancas; " + abertas + " destrancadas, " + fechadas + " trancadas)");
        }

        void OnEvento(string nome, string texto)
        {
            if (nome == "tranca") { AplicarTranca(texto); return; }
            if (nome == "trancas") { if (P.Role == "client") AplicarTrancas(texto); return; }
            if (nome != "porta") return;
            string[] f = texto.Split(';');
            if (f.Length != 2) return;
            DoorAction d = Achar(f[0]);
            if (d == null) { P.Log("porta remota sem correspondente: " + f[0]); return; }
            Aplicando = true;
            try
            {
                // OpenDoor/CloseDoor sao privados no jogo.
                if (f[1] == "abrir" && !d.IsOpened) AccessTools.Method(typeof(DoorAction), "OpenDoor").Invoke(d, new object[] { true });
                else if (f[1] == "fechar" && d.IsOpened) AccessTools.Method(typeof(DoorAction), "CloseDoor").Invoke(d, new object[] { true });
                else if (f[1] == "destrancar" && d.IsLocked()) d.UnlockDoor(true, false, false);
                P.Log("porta remota aplicada: " + f[0] + " " + f[1] + " (aberta=" + d.IsOpened + ")");
            }
            catch (Exception e) { P.Log("falha ao aplicar porta " + f[0] + ": " + e.Message); }
            finally { Aplicando = false; }
        }
    }

    // Diagnostico: criaturas golpeadas nos ultimos 10 s; loga quando a vida sobe de repente (e o que o mod fez naquele quadro).
    public class Diag : MonoBehaviour
    {
        static Diag _inst;
        class Alvo { public Creature C; public float Vida, Ate; }
        static readonly Dictionary<int, Alvo> _alvos = new Dictionary<int, Alvo>();
        static readonly Dictionary<int, string> _marcas = new Dictionary<int, string>();

        public static void Acompanhar(Creature c)
        {
            if (_inst == null) _inst = new GameObject("SparkCoop_Diag").AddComponent<Diag>();
            UnityEngine.Object.DontDestroyOnLoad(_inst.gameObject);
            Alvo a;
            if (!_alvos.TryGetValue(c.GetInstanceID(), out a)) { a = new Alvo(); a.C = c; _alvos[c.GetInstanceID()] = a; }
            a.Vida = c.Character.GetSpec(CharSpecType.Health).CurrentValue; a.Ate = Time.realtimeSinceStartup + 10f;
        }

        public static void Marcar(CharacterBase ch, string o) { _marcas[ch.GetInstanceID()] = Time.frameCount + ": " + o; }

        void Update() { Checar("Update"); }
        void LateUpdate() { Checar("LateUpdate"); }

        static void Checar(string onde)
        {
            if (_alvos.Count == 0) return;
            var fim = new List<int>();
            foreach (var kv in _alvos)
            {
                Alvo a = kv.Value;
                if (a.C == null || a.C.Character == null || Time.realtimeSinceStartup > a.Ate) { fim.Add(kv.Key); continue; }
                float v = a.C.Character.GetSpec(CharSpecType.Health).CurrentValue;
                if (v > a.Vida + 5f)
                {
                    string m; _marcas.TryGetValue(a.C.Character.GetInstanceID(), out m);
                    Plugin.Inst.Log("DIAG vida subiu " + a.Vida.ToString("0.#") + " -> " + v.ToString("0.#") + " em " + (Plugin.Inst.Registry.KeyOf(a.C) ?? a.C.name) +
                                    " (quadro " + Time.frameCount + ", visto no " + onde + "; ativa=" + a.C.enabled + " morta=" + a.C.IsDead +
                                    " copia=" + CreatureSync.EhCopiaStatic(a.C) + "; ultima acao do mod: " + (m ?? "nenhuma") + ")");
                }
                a.Vida = v;
            }
            foreach (int k in fim) _alvos.Remove(k);
        }
    }

    public static class PortasPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(DoorAction), "OpenDoor")]
        static void Abriu(DoorAction __instance) { if (Portas.PodeEnviar) Portas.Inst.Enviar(__instance, "abrir"); }

        [HarmonyPostfix, HarmonyPatch(typeof(DoorAction), "CloseDoor")]
        static void Fechou(DoorAction __instance) { if (Portas.PodeEnviar) Portas.Inst.Enviar(__instance, "fechar"); }

        [HarmonyPostfix, HarmonyPatch(typeof(Lock), "Unlock")]
        static void DestrancouTranca(Lock __instance) { if (Portas.PodeEnviar) Portas.Inst.EnviarTranca(__instance); }

        [HarmonyPostfix, HarmonyPatch(typeof(DoorAction), "UnlockDoor")]
        static void Destrancou(DoorAction __instance) { if (Portas.PodeEnviar) Portas.Inst.Enviar(__instance, "destrancar"); }

        // ---- diagnostico: golpes de arma em criaturas
        public static void PatchGolpes(Harmony h)
        {
            foreach (var m in typeof(Creature).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
            {
                var ps = m.GetParameters();
                if (m.Name != "Damaged" || ps.Length != 3 || ps[1].ParameterType != typeof(HandItems.Weapon)) continue;
                h.Patch(m, new HarmonyMethod(typeof(PortasPatches).GetMethod("AntesGolpe", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)),
                           new HarmonyMethod(typeof(PortasPatches).GetMethod("DepoisGolpe", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)));
            }
        }

        static void AntesGolpe(Creature __instance, out float __state)
        {
            __state = __instance.Character != null ? __instance.Character.GetSpec(CharSpecType.Health).CurrentValue : -1f;
        }

        static void DepoisGolpe(Creature __instance, float __state, DamageType __result)
        {
            if (Plugin.Inst == null || __instance.Character == null) return;
            CharSpec hp = __instance.Character.GetSpec(CharSpecType.Health);
            Diag.Acompanhar(__instance);
            string k = Plugin.Inst.Registry.KeyOf(__instance) ?? __instance.name;
            if (CreatureSync.EhCopiaStatic(__instance) || __result.ToString() == "None") Plugin.Inst.Log("golpe de arma em " + k + ": resultado " + __result + ", vida " + __state.ToString("0.#") + " -> " + hp.CurrentValue.ToString("0.#") +
                            "/" + hp.MaxValue.ToString("0") + (CreatureSync.EhCopiaStatic(__instance) ? " (copia: dono e o outro jogo)" : " (local)") +
                            " ativa=" + __instance.enabled + " morta=" + __instance.IsDead);
        }
    }
}
