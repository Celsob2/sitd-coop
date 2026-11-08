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
// SparkCoop - criaturas sincronizadas com "dono por proximidade".
//  * O host decide o dono de cada criatura viva a até RaioSync de algum herói: o jogador mais próximo (com folga para
//    não trocar a todo instante). Longe dos dois, cada jogo segue com a própria criatura (como antes).
//  * No jogo do dono a criatura é normal: a IA e o combate do jogo agem contra o herói daquele jogo.
//  * No outro jogo ela vira CÓPIA: IA, detector e NavMesh desligados, ataques bloqueados, e posição/rotação/vida/
//    animação vindas do dono ~10 vezes por segundo.
//  * Golpe na cópia: a vida perdida é enviada ao dono ("golpe"), que tira essa vida da criatura real.
//  * Mortes continuam pelo mecanismo já existente (chave da criatura + Creature.Death).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;
using Creatures;
using HandItems;

namespace SparkCoop
{
    public class CriaturaEstado
    {
        public const byte SemDono = 255;
        public ushort Idx;
        public byte Dono = SemDono;
        public bool TemEstado;
        public Vector3 Pos;
        public float RotY, Vida;
        public int Estado0;
        public float Tempo0;
        public List<float> Params = new List<float>();
        // Camadas 1..n do Animator (ataques como a mordida da aranha rodam fora da camada 0): estado, tempo e peso.
        public List<int> EstadosN = new List<int>();
        public List<float> TemposN = new List<float>(), PesosN = new List<float>();

        public void Write(BinaryWriter w)
        {
            w.Write(Idx); w.Write(Dono); w.Write(TemEstado);
            if (!TemEstado) return;
            w.Write(Pos.x); w.Write(Pos.y); w.Write(Pos.z); w.Write(RotY); w.Write(Vida); w.Write(Estado0); w.Write(Tempo0);
            w.Write((byte)Params.Count); foreach (float f in Params) w.Write(f);
            w.Write((byte)EstadosN.Count);
            for (int i = 0; i < EstadosN.Count; i++) { w.Write(EstadosN[i]); w.Write(TemposN[i]); w.Write(PesosN[i]); }
        }

        public static CriaturaEstado Read(BinaryReader r)
        {
            var c = new CriaturaEstado();
            c.Idx = r.ReadUInt16(); c.Dono = r.ReadByte(); c.TemEstado = r.ReadBoolean();
            if (!c.TemEstado) return c;
            c.Pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); c.RotY = r.ReadSingle(); c.Vida = r.ReadSingle();
            c.Estado0 = r.ReadInt32(); c.Tempo0 = r.ReadSingle();
            int n = r.ReadByte(); for (int i = 0; i < n; i++) c.Params.Add(r.ReadSingle());
            int k = r.ReadByte();
            for (int i = 0; i < k; i++) { c.EstadosN.Add(r.ReadInt32()); c.TemposN.Add(r.ReadSingle()); c.PesosN.Add(r.ReadSingle()); }
            return c;
        }
    }

    public class CreatureSync : MonoBehaviour
    {
        public static CreatureSync Inst;
        public const float RaioSync = 40f, Folga = 3f, Intervalo = 0.1f;

        class Copia
        {
            public Creature C;
            public readonly List<Behaviour> Desligados = new List<Behaviour>();
            public NavMeshAgent Agente; public bool AgenteEstavaLigado;
            public bool RootMotion;
            public Vector3 Pos; public float RotY; public bool TemAlvo;
            public float VidaAplicada = -1f;
            public readonly int[] UltEstado = new int[6];      // ultimo estado aplicado por camada do Animator
            public readonly float[] UltTempo = new float[6];
        }

        readonly Dictionary<int, byte> _dono = new Dictionary<int, byte>();    // índice -> dono atual
        readonly Dictionary<int, Copia> _copias = new Dictionary<int, Copia>(); // índice -> cópia (criatura de outro dono)
        float _proximo; uint _seq;
        float _ultimaListaHost;
        public int Enviadas, Recebidas;

        Plugin P { get { return Plugin.Inst; } }
        bool SouHost { get { return P.Role == "host"; } }
        byte Eu { get { return SouHost ? (byte)0 : (byte)1; } }
        bool Ativo { get { return P != null && P.Net != null && P.Net.HasPeer && P.Registry.Count > 0 && P.LocalWorldHash != "" && P.LocalWorldHash == P.RemoteWorldHash; } }

        void Awake() { Inst = this; P.GameEvent += OnEvento; }

        public bool EhCopia(Creature c) { int i = P.Registry.IndexOf(c); return i >= 0 && _copias.ContainsKey(i); }
        public static bool EhCopiaStatic(Creature c) { return Inst != null && c != null && Inst.EhCopia(c); }

        public void NovoNivel() { foreach (int i in new List<int>(_copias.Keys)) Liberar(i); _dono.Clear(); }

        public byte DonoDe(int i) { byte d; return _dono.TryGetValue(i, out d) ? d : CriaturaEstado.SemDono; }

        // ---------------------------------------------------------------- quadro a quadro
        void Update()
        {
            if (P == null) return;
            if (!Ativo) { if (_copias.Count > 0) NovoNivel(); return; }
            float now = Time.realtimeSinceStartup;
            if (!SouHost && _copias.Count > 0 && now - _ultimaListaHost > 1.5f) { P.Log("criaturas: host sem enviar ha 1,5 s; liberando copias"); NovoNivel(); }

            // Cópias: suaviza o movimento e detecta golpes (vida caiu localmente).
            foreach (var kv in _copias)
            {
                Copia cp = kv.Value;
                if (cp.C == null || cp.C.IsDead) continue;
                if (cp.TemAlvo)
                {
                    Transform t = cp.C.transform;
                    float k = Mathf.Clamp01(Time.deltaTime * 10f);
                    t.position = (t.position - cp.Pos).sqrMagnitude > 16f ? cp.Pos : Vector3.Lerp(t.position, cp.Pos, k);
                    t.rotation = Quaternion.Slerp(t.rotation, Quaternion.Euler(0f, cp.RotY, 0f), k);
                }
            }

            if (now < _proximo) return;
            _proximo = now + Intervalo;
            if (SouHost) AtribuirDonos();
            EnviarEstados();
        }

        // Campos protegidos da criatura.
        static readonly System.Reflection.FieldInfo CampoAnimator = AccessTools.Field(typeof(Creature), "Animator");
        static readonly System.Reflection.FieldInfo CampoAgente = AccessTools.Field(typeof(Creature), "NavAgent");
        static Animator Anim(Creature c) { return c != null ? (Animator)CampoAnimator.GetValue(c) : null; }

        static CharSpec Vida(Creature c) { return c.Character != null ? c.Character.GetSpec(CharSpecType.Health) : null; }

        // ---------------------------------------------------------------- host: quem é dono de cada criatura
        void AtribuirDonos()
        {
            MainChar mc = MainChar.Instance;
            if (mc == null) return;
            Vector3 h = mc.transform.position;
            bool temCliente = P.Ghost != null && P.Ghost.Visible;
            Vector3 cl = temCliente ? P.Ghost.Position : Vector3.zero;
            var lista = P.Registry.PorIndice;
            for (int i = 0; i < lista.Count; i++)
            {
                Creature c = lista[i];
                byte atual = DonoDe(i);
                if (c == null || c.IsDead) { if (atual != CriaturaEstado.SemDono) Definir(i, CriaturaEstado.SemDono); continue; }
                Vector3 p = c.transform.position;
                float dh = (p - h).magnitude, dc = temCliente ? (p - cl).magnitude : float.MaxValue;
                // Herói caído não é dono de nada: as criaturas ignoram herói caído e ficariam paradas no jogo do outro.
                if (CoopVida.Inst != null && CoopVida.Inst.LocalCaido) dh = float.MaxValue;
                if (CoopVida.Inst != null && CoopVida.Inst.AmigoCaido) dc = float.MaxValue;
                byte novo;
                if (Mathf.Min(dh, dc) > RaioSync) novo = CriaturaEstado.SemDono;
                else if (atual == 0 && dc + Folga >= dh) novo = 0;
                else if (atual == 1 && dh + Folga >= dc) novo = 1;
                else novo = dh <= dc ? (byte)0 : (byte)1;
                if (novo != atual) Definir(i, novo);
            }
        }

        void Definir(int i, byte dono)
        {
            byte antes = DonoDe(i);
            if (dono == CriaturaEstado.SemDono) _dono.Remove(i); else _dono[i] = dono;
            if (dono == Eu || dono == CriaturaEstado.SemDono) Liberar(i);
            else Prender(i);
            if (antes != dono) P.Log("criatura " + P.Registry.Chaves[i] + ": dono " + Nome(antes) + " -> " + Nome(dono));
        }

        static string Nome(byte d) { return d == 0 ? "host" : d == 1 ? "cliente" : "ninguem"; }

        // ---------------------------------------------------------------- rede
        void EnviarEstados()
        {
            var m = new Msg(); m.Type = Msg.CRIATURAS; m.Seq = ++_seq;
            var lista = P.Registry.PorIndice;
            foreach (var kv in _dono)
            {
                int i = kv.Key; byte dono = kv.Value;
                // Host manda a lista completa de donos (+ estado das suas); cliente manda só o estado das suas.
                if (!SouHost && dono != Eu) continue;
                var e = new CriaturaEstado(); e.Idx = (ushort)i; e.Dono = dono;
                Creature c = lista[i];
                if (dono == Eu && c != null && !c.IsDead) Capturar(c, e);
                m.Criaturas.Add(e);
                if (m.Criaturas.Count >= 60) break;
            }
            if (m.Criaturas.Count == 0 && !SouHost) return;
            P.Net.Send(m);
            Enviadas++;
        }

        static void Capturar(Creature c, CriaturaEstado e)
        {
            e.TemEstado = true;
            e.Pos = c.transform.position; e.RotY = c.transform.eulerAngles.y;
            CharSpec hp = Vida(c); e.Vida = hp != null ? hp.CurrentValue : 0f;
            Animator an = Anim(c);
            if (an != null && an.isActiveAndEnabled && an.runtimeAnimatorController != null)
            {
                AnimatorStateInfo s = an.GetCurrentAnimatorStateInfo(0);
                e.Estado0 = s.fullPathHash; e.Tempo0 = s.normalizedTime;
                foreach (AnimatorControllerParameter p in an.parameters)
                {
                    switch (p.type)
                    {
                        case AnimatorControllerParameterType.Float: e.Params.Add(an.GetFloat(p.nameHash)); break;
                        case AnimatorControllerParameterType.Int: e.Params.Add(an.GetInteger(p.nameHash)); break;
                        case AnimatorControllerParameterType.Bool: e.Params.Add(an.GetBool(p.nameHash) ? 1f : 0f); break;
                        default: e.Params.Add(0f); break;
                    }
                    if (e.Params.Count >= 250) break;
                }
                for (int l = 1; l < Math.Min(an.layerCount, 6); l++)
                {
                    AnimatorStateInfo sl = an.GetCurrentAnimatorStateInfo(l);
                    e.EstadosN.Add(sl.fullPathHash); e.TemposN.Add(sl.normalizedTime); e.PesosN.Add(an.GetLayerWeight(l));
                }
            }
        }

        public void Receber(Msg m)
        {
            Recebidas++;
            var lista = P.Registry.PorIndice;
            if (!SouHost)
            {
                // A mensagem do host é a lista completa de donos: o que não veio volta a ser "de ninguém".
                _ultimaListaHost = Time.realtimeSinceStartup;
                var vistos = new HashSet<int>();
                foreach (CriaturaEstado e in m.Criaturas) { if (e.Idx < lista.Count) { vistos.Add(e.Idx); if (DonoDe(e.Idx) != e.Dono) Definir(e.Idx, e.Dono); } }
                foreach (int i in new List<int>(_dono.Keys)) if (!vistos.Contains(i)) Definir(i, CriaturaEstado.SemDono);
            }
            foreach (CriaturaEstado e in m.Criaturas)
            {
                if (!e.TemEstado || e.Idx >= lista.Count) continue;
                Copia cp;
                if (_copias.TryGetValue(e.Idx, out cp)) Aplicar(cp, e);
            }
        }

        static void Aplicar(Copia cp, CriaturaEstado e)
        {
            if (cp.C == null || cp.C.IsDead) return;
            cp.Pos = e.Pos; cp.RotY = e.RotY; cp.TemAlvo = true;
            CharSpec hp = Vida(cp.C);
            if (hp != null) { hp.CurrentValue = e.Vida; cp.VidaAplicada = e.Vida; }
            Animator an = Anim(cp.C);
            // Criatura parada/dormindo no jogo de quem nao e dono pode estar com o Animator desligado; a copia precisa dele
            // ligado para repetir a animacao do dono (antes ficava congelada).
            if (an != null && !an.enabled && cp.C.gameObject.activeInHierarchy) an.enabled = true;
            if (an == null || !an.isActiveAndEnabled || an.runtimeAnimatorController == null) return;
            AnimatorControllerParameter[] ps = an.parameters;
            int n = Math.Min(ps.Length, e.Params.Count);
            for (int i = 0; i < n; i++)
            {
                switch (ps[i].type)
                {
                    case AnimatorControllerParameterType.Float: an.SetFloat(ps[i].nameHash, e.Params[i]); break;
                    case AnimatorControllerParameterType.Int: an.SetInteger(ps[i].nameHash, (int)e.Params[i]); break;
                    case AnimatorControllerParameterType.Bool: an.SetBool(ps[i].nameHash, e.Params[i] > 0.5f); break;
                }
            }
            Camada(an, cp, 0, e.Estado0, e.Tempo0);
            for (int l = 0; l < e.EstadosN.Count && l + 1 < an.layerCount && l + 1 < cp.UltEstado.Length; l++)
            {
                an.SetLayerWeight(l + 1, e.PesosN[l]);
                Camada(an, cp, l + 1, e.EstadosN[l], e.TemposN[l]);
            }
        }

        // Troca de estado, ou o mesmo estado recomecado (o dono repetiu o ataque: o tempo normalizado voltou para perto de 0).
        static void Camada(Animator an, Copia cp, int l, int estado, float tempo)
        {
            if (estado == 0) return;
            bool recomecou = estado == cp.UltEstado[l] && tempo + 0.25f < cp.UltTempo[l];
            if ((estado != cp.UltEstado[l] || recomecou) && an.HasState(l, estado))
                an.CrossFade(estado, recomecou ? 0.05f : 0.15f, l, tempo % 1f);
            cp.UltEstado[l] = estado; cp.UltTempo[l] = tempo;
        }

        // ---------------------------------------------------------------- virar cópia / voltar ao normal
        void Prender(int i)
        {
            if (_copias.ContainsKey(i)) return;
            Creature c = P.Registry.PorIndice[i];
            if (c == null || c.IsDead) return;
            var cp = new Copia(); cp.C = c;
            foreach (MonoBehaviour b in c.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (b == null || !b.enabled) continue;
                if (b is Creature || b is DetectionZone) { b.enabled = false; cp.Desligados.Add(b); }
            }
            cp.Agente = (NavMeshAgent)CampoAgente.GetValue(c);
            if (cp.Agente != null) { cp.AgenteEstavaLigado = cp.Agente.enabled; cp.Agente.enabled = false; }
            if (Anim(c) != null) { cp.RootMotion = Anim(c).applyRootMotion; Anim(c).applyRootMotion = false; }
            CharSpec hp = Vida(c); cp.VidaAplicada = hp != null ? hp.CurrentValue : -1f;
            cp.Pos = c.transform.position; cp.RotY = c.transform.eulerAngles.y;
            _copias[i] = cp;
        }

        public void Liberar(int i)
        {
            Copia cp;
            if (!_copias.TryGetValue(i, out cp)) return;
            _copias.Remove(i);
            if (cp.C == null) return;
            foreach (Behaviour b in cp.Desligados) if (b != null) b.enabled = true;
            if (Anim(cp.C) != null) Anim(cp.C).applyRootMotion = cp.RootMotion;
            if (cp.Agente != null && cp.AgenteEstavaLigado)
            {
                cp.Agente.enabled = true;
                if (cp.Agente.isOnNavMesh) cp.Agente.Warp(cp.C.transform.position);
            }
        }

        public void LiberarCriatura(Creature c) { int i = P.Registry.IndexOf(c); if (i >= 0) Liberar(i); }

        // Chamado quando um golpe do herói local (arma, flecha, arma arremessada, armadilha) termina numa cópia.
        public void GolpeNaCopia(Creature c, float perda)
        {
            int i = P.Registry.IndexOf(c);
            if (i < 0 || perda <= 0.001f) return;
            P.SendEvent("golpe", i + ";" + perda.ToString("0.###", CultureInfo.InvariantCulture));
        }

        // ---------------------------------------------------------------- golpes encaminhados ao dono
        void OnEvento(string nome, string texto)
        {
            if (nome != "golpe") return;
            string[] f = texto.Split(';');
            int i; float perda;
            if (f.Length != 2 || !int.TryParse(f[0], out i) || !float.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out perda)) return;
            if (i < 0 || i >= P.Registry.PorIndice.Count || DonoDe(i) != Eu) return;
            Creature c = P.Registry.PorIndice[i];
            if (c == null || c.IsDead) return;
            CharSpec hp = Vida(c);
            if (hp == null) return;
            hp.CurrentValue -= perda;
            P.Log("golpe do outro jogador em " + P.Registry.Chaves[i] + ": -" + perda.ToString("0.#") + " (vida " + hp.CurrentValue.ToString("0.#") + ")");
            if (hp.CurrentValue <= 0f) c.Death(false, false, false);
        }

        // ---------------------------------------------------------------- diagnóstico (comando de teste "criatura <chave>")
        public string Descrever(string chave)
        {
            int i = P.Registry.Chaves.IndexOf(chave);
            if (i < 0) return "criatura " + chave + " nao registrada";
            Creature c = P.Registry.PorIndice[i];
            CharSpec hp = c != null ? Vida(c) : null;
            return "criatura " + chave + " dono=" + Nome(DonoDe(i)) + " copia=" + _copias.ContainsKey(i) + " pos=" + (c != null ? c.transform.position.ToString() : "?") +
                   " vida=" + (hp != null ? hp.CurrentValue.ToString("0.#") : "?") + " morta=" + (c != null && c.IsDead) + " ia=" + (c != null && c.enabled) +
                   " estado0=" + (c != null && Anim(c) != null && Anim(c).isActiveAndEnabled ? Anim(c).GetCurrentAnimatorStateInfo(0).fullPathHash : 0);
        }

        public string Resumo()
        {
            int meus = 0, deles = 0;
            foreach (byte d in _dono.Values) { if (d == Eu) meus++; else deles++; }
            return L.T("criaturas: minhas ", "creatures: mine ") + meus + L.T(", do outro ", ", other player's ") + deles;
        }
    }

    public static class CriaturaPatches
    {
        // Cópias não atacam: os eventos de animação de ataque (AttackRightWeapon, AttackHeadWeapon...) ligam as armas da
        // criatura e o contato fere o herói. No jogo do não-dono, isso não pode acontecer.
        public static int PatchAtaques(Harmony h)
        {
            var prefixo = new HarmonyMethod(typeof(CriaturaPatches).GetMethod("PularSeCopia", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
            int n = 0;
            foreach (Type t in typeof(Creature).Assembly.GetTypes())
            {
                if (!typeof(Creature).IsAssignableFrom(t)) continue;
                foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    if (!m.Name.StartsWith("Attack") || m.IsAbstract || m.ReturnType != typeof(void)) continue;
                    try { h.Patch(m, prefixo); n++; } catch (Exception e) { Plugin.Inst.Log("aviso: ataque nao protegido " + t.Name + "." + m.Name + ": " + e.Message); }
                }
            }
            return n;
        }

        static bool PularSeCopia(Creature __instance) { return !CreatureSync.EhCopiaStatic(__instance); }

        // Golpes do herói local nas cópias: mede a vida perdida só na chamada mais externa de Damaged*/DamagedThrowingWeapon
        // (elas chamam umas às outras). Efeitos contínuos (sangramento, veneno) não passam por aqui e não contam.
        public static int PatchDanos(Harmony h)
        {
            var pre = new HarmonyMethod(typeof(CriaturaPatches).GetMethod("AntesDoDano", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
            var pos = new HarmonyMethod(typeof(CriaturaPatches).GetMethod("DepoisDoDano", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
            int n = 0;
            foreach (var m in typeof(Creature).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
            {
                if (m.Name != "Damaged" && m.Name != "DamagedThrowingWeapon") continue;
                var ps = m.GetParameters();
                if (ps.Length == 1 && ps[0].ParameterType != typeof(float)) continue; // Damaged(DamageType) só anima
                h.Patch(m, pre, pos); n++;
            }
            return n;
        }

        static int _profundidade;

        static void AntesDoDano(Creature __instance, object[] __args, out float __state)
        {
            _profundidade++;
            __state = -1f;
            if (_profundidade != 1 || !CreatureSync.EhCopiaStatic(__instance) || __instance.Character == null) return;
            // Armadilhas do cenário existem nos dois mundos e o dono já aplica esse dano; só a armadilha montada pelo
            // herói deste jogo conta como golpe dele.
            Trap trap = __args.Length > 0 ? __args[0] as Trap : null;
            if (trap != null && !trap.IsOwnTrap) return;
            __state = __instance.Character.GetSpec(CharSpecType.Health).CurrentValue;
        }

        static void DepoisDoDano(Creature __instance, float __state, System.Reflection.MethodBase __originalMethod)
        {
            _profundidade--;
            if (__state < 0f || __instance.Character == null || CreatureSync.Inst == null) return;
            float perda = __state - __instance.Character.GetSpec(CharSpecType.Health).CurrentValue;
            if (perda > 0.001f) Plugin.Inst.Log("golpe local numa copia via " + __originalMethod.Name + "(" + __originalMethod.GetParameters().Length + "): -" + perda.ToString("0.#"));
            CreatureSync.Inst.GolpeNaCopia(__instance, perda);
        }

        // Rede de segurança: dano vindo de uma cópia nunca fere o herói local.
        [HarmonyPrefix, HarmonyPatch(typeof(MainChar), "Damaged", new[] { typeof(CharacterBase), typeof(Weapon) })]
        static bool DanoDeCopia(CharacterBase enemyChar, ref DamageType __result)
        {
            if (enemyChar == null) return true;
            Creature c = enemyChar.GetComponentInParent<Creature>();
            if (!CreatureSync.EhCopiaStatic(c)) return true;
            __result = default(DamageType);
            return false;
        }
    }
}
