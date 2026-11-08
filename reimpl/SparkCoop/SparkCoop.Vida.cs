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
// SparkCoop - herói caído, reviver e dormir juntos (regras combinadas com o usuário em 31/10/2026):
//  * Se o outro jogador está de pé, morrer deixa o herói CAÍDO (sem tela de morte). O amigo segura E por 5 s perto
//    dele e ele levanta com 15% da vida, sem debuffs temporários (veneno, sangramento...).
//  * Queda no abismo: não dá para reviver no local; o herói volta quando o amigo terminar um descanso numa cama.
//    Qualquer herói caído também volta assim.
//  * Se os dois caírem, acontece a morte normal do jogo para ambos.
//  * Descanso: o botão de sair da cama vira "pronto para dormir"; o dia só passa (e o jogo só salva) quando os dois
//    confirmarem. Se o amigo está caído ou no abismo, ninguém espera por ele.
using System;
using System.Collections.Generic;
using System.Globalization;
using HandItems;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using AnimatorStates.MainCharStates;
using Management;
using UI;
using UI.Screens;

namespace SparkCoop
{
    public class CoopVida : MonoBehaviour
    {
        public static CoopVida Inst;
        public const float SegundosParaReviver = 5f;
        public const float VidaAoReviver = 0.15f;
        public const float DistanciaReviver = 2.2f;

        public bool LocalCaido, LocalAbismo, AmigoCaido, AmigoAbismo;
        public bool LocalPronto, AmigoPronto;
        bool _fimDeJogo;
        float _progressoReviver;
        int _estadoIdle;
        string _aviso = ""; float _avisoAte;

        Plugin P { get { return Plugin.Inst; } }
        bool EmSessao { get { return P != null && P.Net != null && P.Net.HasPeer && Time.realtimeSinceStartup - P.LastRecv < 5f; } }

        void Awake()
        {
            Inst = this;
            Plugin.Inst.GameEvent += OnEvento;
        }

        // ---------------------------------------------------------------- caído
        // Chamado no lugar do fim de jogo quando a animação de morte começa. true = o mod assumiu (herói fica caído).
        public bool TentarFicarCaido(Animator animator)
        {
            if (!EmSessao || _fimDeJogo || LocalCaido) return false;
            bool abismo = Necropolis.Instance != null && Necropolis.Instance.CurrentHero != null &&
                          Necropolis.Instance.CurrentHero.DeathType == MainChar.DeathType.DeadFromAbyss;
            LocalCaido = true; LocalAbismo = abismo;
            animator.enabled = false; // o jogo faz isso para congelar o corpo no chão
            MainChar.Instance.IgnoredForEnemy = true;
            // Death() desliga o colisor; sem isto o corpo atravessa o chão.
            var rb = MainChar.Instance.GetComponent<Rigidbody>();
            if (rb != null && !abismo) { rb.velocity = Vector3.zero; rb.isKinematic = true; }
            P.SendEvent("caido", abismo ? "abismo" : "corpo");
            P.Log("heroi local caido" + (abismo ? " (abismo)" : ""));
            if (AmigoCaido) { FimDeJogo(); return false; }
            return true;
        }

        void FimDeJogo()
        {
            if (_fimDeJogo) return;
            _fimDeJogo = true;
            P.Log("os dois cairam: fim de jogo normal");
            P.SendEvent("fim-de-jogo", "");
            if (LocalCaido) AccessTools.Method(typeof(UserInterface), "ShowAfterDeathEvent").Invoke(UserInterface.Instance, null);
        }

        public void Reviver(Vector3? posicao)
        {
            MainChar mc = MainChar.Instance;
            if (!LocalCaido || mc == null) return;
            CharacterBase ch = mc.Character;
            if (posicao.HasValue)
            {
                mc.transform.position = posicao.Value;
                var rb0 = mc.GetComponent<Rigidbody>(); if (rb0 != null) rb0.velocity = Vector3.zero;
            }
            ch.Alive();
            // MainChar.Death cancela todas as rotinas repetidas do heroi (CancelInvoke) e Alive() so religa as do
            // CharacterBase. Sem religar estas (mesmos tempos de MainChar.Start), a luz de visao em volta do heroi
            // ("UpdateLight") nunca mais acende depois do descanso e a tela fica preta sem lamparina.
            foreach (var r in new[] { new { N = "ScanRoomSize", T = 0f, R = 0.2f }, new { N = "UpdateAnimatorSpeed", T = 0f, R = 0.1f },
                                      new { N = "CheckPreDeathState", T = 0.05f, R = 0.1f }, new { N = "UpdateLight", T = 0.05f, R = 0.2f } })
            {
                mc.CancelInvoke(r.N);
                mc.InvokeRepeating(r.N, r.T, r.R);
            }
            CharSpec hp = ch.GetSpec(CharSpecType.Health);
            hp.CurrentValue = Mathf.Max(1f, hp.MaxValue * VidaAoReviver);
            ch.RemoveAllDebuffs(true);
            var rb = mc.GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = false;
            var col = mc.GetComponent<CapsuleCollider>(); if (col != null) col.enabled = true;
            if (mc.ActionZone != null) mc.ActionZone.enabled = true;
            mc.IgnoredForEnemy = false;
            Animator an = mc.GetComponent<Animator>();
            an.enabled = true;
            an.SetBool("IsDead", false);
            if (LocalAbismo) DesfazerQuedaNoAbismo(mc, an);
            if (_estadoIdle != 0) an.CrossFade(_estadoIdle, 0.25f, 0, 0f);
            an.SetTrigger("Getup");
            LocalCaido = false; LocalAbismo = false; _fimDeJogo = false;
            P.SendEvent("revivido", "");
            Aviso(L.T("Você levantou com ", "You got up with ") + Mathf.RoundToInt(VidaAoReviver * 100) + L.T("% da vida.", "% health."));
            P.Log("heroi local revivido; vida " + hp.CurrentValue.ToString("0") + "/" + hp.MaxValue.ToString("0") + (posicao.HasValue ? " em " + posicao.Value : ""));
        }

        // Na queda no abismo o jogo liga as animacoes "caindo" (FallingFrontDeath/FallingBackDeath) e zera as camadas de
        // animacao das armas (SetAnimationLayer(Base)); sem desfazer isso o heroi volta preso na pose de queda, sem andar.
        void DesfazerQuedaNoAbismo(MainChar mc, Animator an)
        {
            foreach (string campo in new[] { "FallingFrontDeath", "FallingBackDeath" })
            {
                var f = AccessTools.Field(AccessTools.TypeByName("MainCharAnimationParams"), campo);   // classe interna do jogo
                string nome = f != null ? f.GetValue(null) as string : null;
                if (!string.IsNullOrEmpty(nome)) an.SetBool(nome, false);
            }
            try { AccessTools.Method(typeof(MainChar), "UpdateAnimationLayer").Invoke(mc, null); }
            catch (Exception e) { P.Log("abismo: falha ao restaurar a camada de animacao: " + e.Message); }
            Game.IsPlayerControlLocked = false;
            if (Dificuldade.Inst != null && Dificuldade.Inst.AbismoPerdeItens) PerderItens(mc);
        }

        // Opcao "perde tudo": o heroi volta do abismo sem nenhum item (como a morte no abismo do jogo, mas vivo).
        void PerderItens(MainChar mc)
        {
            Inventory inv = mc.Inventory;
            if (inv == null || inv.InventoryList == null) return;
            int n = 0;
            Itens.Aplicando = true;   // nao avisar o outro jogo (itens compartilhados continuam com ele)
            try
            {
                foreach (HandItem it in new List<HandItem>(inv.InventoryList))
                {
                    if (it == null) continue;
                    try { inv.DestroyItem(it); n++; } catch (Exception e) { P.Log("abismo: falha ao remover " + it.name + ": " + e.Message); }
                }
            }
            finally { Itens.Aplicando = false; }
            P.Log("abismo: heroi voltou sem itens (" + n + " removidos)");
            Aviso(L.T("Você voltou do abismo sem seus itens.", "You came back from the abyss without your items."));
        }

        // ---------------------------------------------------------------- dormir juntos
        // Chamado quando o jogador aperta o botão de sair da cama. true = pode seguir agora.
        public bool PodeTerminarDescanso()
        {
            if (!EmSessao || AmigoCaido || _liberandoDescanso) return true;
            if (!LocalPronto)
            {
                LocalPronto = true;
                P.SendEvent("pronto-dormir", "");
                if (!AmigoPronto) Aviso(L.T("Pronto para dormir. Aguardando ", "Ready to sleep. Waiting for ") + P.PeerName + "...");
                return AmigoPronto;
            }
            if (AmigoPronto) return true;
            // Apertou de novo enquanto esperava: sai da cama sozinho (o amigo pode nem conseguir deitar agora).
            LocalPronto = false;
            P.SendEvent("desistiu-dormir", "");
            P.Log("descanso: saiu da cama sem esperar o amigo");
            return true;
        }

        bool _liberandoDescanso;
        void LiberarDescanso()
        {
            LocalPronto = AmigoPronto = false;
            if (UserInterface.Instance == null || !UserInterface.Instance.SleepingScreen.gameObject.activeSelf) return;
            P.Log("os dois prontos: terminando o descanso");
            _liberandoDescanso = true;
            try { UserInterface.Instance.SleepingScreen.HideScreen(); }
            finally { _liberandoDescanso = false; }
        }

        // Depois que o descanso terminou (dia passou e o jogo salvou): quem está caído volta perto desta cama.
        public void DescansoTerminado()
        {
            LocalPronto = AmigoPronto = false;
            if (!EmSessao || !AmigoCaido) return;
            Vector3 p = MainChar.Instance.transform.position + MainChar.Instance.transform.right * 1.2f;
            P.SendEvent("reviver-na-cama", p.x.ToString(CultureInfo.InvariantCulture) + ";" + p.y.ToString(CultureInfo.InvariantCulture) + ";" + p.z.ToString(CultureInfo.InvariantCulture));
        }

        // ---------------------------------------------------------------- eventos
        void OnEvento(string nome, string texto)
        {
            switch (nome)
            {
                case "caido":
                    AmigoCaido = true; AmigoAbismo = texto == "abismo";
                    AmigoPronto = false;
                    Aviso(AmigoAbismo ? P.PeerName + L.T(" caiu no abismo! Ele volta quando você descansar numa cama.", " fell into the abyss! They come back when you rest in a bed.")
                                      : P.PeerName + L.T(" caiu! Segure E perto dele por 5 s para reviver.", " is down! Hold E near them for 5 s to revive."));
                    if (LocalCaido) FimDeJogo();
                    else if (LocalPronto) LiberarDescanso(); // não espera mais quem caiu
                    break;
                case "revivido":
                    AmigoCaido = AmigoAbismo = false;
                    Aviso(P.PeerName + L.T(" está de pé!", " is back up!"));
                    break;
                case "reviver":
                    if (LocalCaido && !LocalAbismo) Reviver(null);
                    break;
                case "reviver-na-cama":
                    if (LocalCaido)
                    {
                        string[] v = texto.Split(';');
                        var ci = CultureInfo.InvariantCulture;
                        Reviver(new Vector3(float.Parse(v[0], ci), float.Parse(v[1], ci), float.Parse(v[2], ci)));
                    }
                    break;
                case "pronto-dormir":
                    AmigoPronto = true;
                    if (LocalPronto) LiberarDescanso();
                    else Aviso(P.PeerName + L.T(" está pronto para dormir. Deite numa cama para descansar junto.", " is ready to sleep. Lie down in a bed to rest together."));
                    break;
                case "desistiu-dormir":
                    AmigoPronto = false;
                    break;
                case "fim-de-jogo":
                    if (!_fimDeJogo) { _fimDeJogo = true; if (LocalCaido) AccessTools.Method(typeof(UserInterface), "ShowAfterDeathEvent").Invoke(UserInterface.Instance, null); }
                    break;
            }
        }

        public void NovoNivel()
        {
            LocalCaido = LocalAbismo = AmigoCaido = AmigoAbismo = LocalPronto = AmigoPronto = _fimDeJogo = false;
            _progressoReviver = 0; _estadoIdle = 0;
        }

        // ---------------------------------------------------------------- quadro a quadro
        void Update()
        {
            MainChar mc = MainChar.Instance;
            if (mc == null || P == null) return;
            if (_estadoIdle == 0 && !LocalCaido && mc.Character != null && !mc.Character.IsDead)
            {
                Animator an = mc.GetComponent<Animator>();
                if (an != null && an.isActiveAndEnabled) _estadoIdle = an.GetCurrentAnimatorStateInfo(0).fullPathHash;
            }

            // Reviver o amigo: segurar E (teclado) ou o botão de ação do controle perto do corpo dele.
            bool podeReviver = EmSessao && AmigoCaido && !AmigoAbismo && !LocalCaido && P.Ghost != null && P.Ghost.Visible &&
                               Vector3.Distance(P.Ghost.Position, mc.transform.position) <= DistanciaReviver;
            bool segurando = podeReviver && ((Keyboard.current != null && Keyboard.current.eKey.isPressed) ||
                                             (Gamepad.current != null && Gamepad.current.buttonWest.isPressed) ||
                                             Time.realtimeSinceStartup < TesteSegurarAte);
            if (segurando)
            {
                _progressoReviver += Time.unscaledDeltaTime;
                if (_progressoReviver >= SegundosParaReviver)
                {
                    _progressoReviver = 0;
                    AmigoCaido = false;
                    P.SendEvent("reviver", "");
                }
            }
            else _progressoReviver = 0;
            _podeReviver = podeReviver;
        }

        bool _podeReviver;
        public static float TesteSegurarAte; // só para testes automatizados (simula segurar E)

        public void Aviso(string s) { _aviso = s; _avisoAte = Time.realtimeSinceStartup + 6f; P.Log("aviso: " + s); }

        void OnGUI()
        {
            if (P == null || P.Net == null || _fimDeJogo) return;
            var estilo = new GUIStyle(GUI.skin.label);
            estilo.alignment = TextAnchor.MiddleCenter; estilo.fontSize = 22; estilo.richText = true;
            string centro = null;
            if (LocalCaido)
                centro = LocalAbismo ? L.T("Você caiu no abismo.\nVocê volta quando ", "You fell into the abyss.\nYou come back when ") + P.PeerName + L.T(" descansar numa cama.", " rests in a bed.")
                                     : L.T("Você está caído.\n", "You are down.\n") + P.PeerName + L.T(" pode te reviver (segurar E por 5 s).", " can revive you (hold E for 5 s).");
            else if (_podeReviver)
                centro = _progressoReviver > 0 ? L.T("Revivendo ", "Reviving ") + P.PeerName + "... " + Mathf.CeilToInt(SegundosParaReviver - _progressoReviver) + " s"
                                               : L.T("Segure [E] para reviver ", "Hold [E] to revive ") + P.PeerName;
            else if (LocalPronto && !AmigoPronto)
                centro = L.T("Pronto para dormir. Aguardando ", "Ready to sleep. Waiting for ") + P.PeerName + "..." +
                         L.T("\n(aperte \"Terminar de descansar\" de novo para descansar sozinho)", "\n(press \"End rest\" again to rest alone)");
            else if (Time.realtimeSinceStartup < _avisoAte)
                centro = _aviso;
            if (centro == null) return;
            var r = new Rect(0, Screen.height * 0.62f, Screen.width, 90);
            var sombra = new GUIStyle(estilo); sombra.normal.textColor = Color.black;
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), centro, sombra);
            GUI.Label(r, centro, estilo);
            if (_podeReviver && _progressoReviver > 0)
            {
                float w = 300f, x = (Screen.width - w) / 2f, y = r.y + 80;
                GUI.Box(new Rect(x, y, w, 14), "");
                GUI.Box(new Rect(x, y, w * Mathf.Clamp01(_progressoReviver / SegundosParaReviver), 14), "");
            }
        }
    }

    public static class VidaPatches
    {
        // Com o amigo no abismo, a cama nao recusa por "descansei ha pouco": o descanso e o que traz o amigo de volta.
        [HarmonyPostfix, HarmonyPatch(typeof(MainChar), "HasRestCooldown")]
        static void SemEsperaParaDescansar(ref bool __result)
        {
            // Tambem quando o amigo ja esta na cama esperando: senao ele fica preso la ("descansei ha pouco" aqui).
            if (__result && CoopVida.Inst != null && ((CoopVida.Inst.AmigoCaido && CoopVida.Inst.AmigoAbismo) || CoopVida.Inst.AmigoPronto)) __result = false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(DeathAnimatorState), "OnStateEnter", new[] { typeof(Animator), typeof(AnimatorStateInfo), typeof(int) })]
        static bool AntesDaMorte(Animator animator, int layerIndex)
        {
            // O fantasma "morrendo" não é o herói local. Mas o fantasma precisa congelar no chão como o jogo faz com o
            // herói (desligando o Animator); sem isso ele cai e levanta sem parar. Ghost.Apply religa quando o outro levanta.
            if (Ghost.IsGhost(animator))
            {
                Ghost g = Plugin.Inst != null ? Plugin.Inst.Ghost : null;
                if (g != null && g.RemotoMorto) animator.enabled = false;
                return false;
            }
            if (CoopVida.Inst == null || animator.GetLayerWeight(layerIndex) <= 0f) return true;
            return !CoopVida.Inst.TentarFicarCaido(animator);
        }

        // O fantasma usa o controlador de animação do herói, que tem scripts de estado (StateMachineBehaviour) que
        // agem sobre MainChar.Instance (tela preta, colisor, armas...). Nenhum deles pode rodar no fantasma.
        public static int PatchStateBehaviours(Harmony h)
        {
            var prefixo = new HarmonyMethod(typeof(VidaPatches).GetMethod("PularNoFantasma", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
            int n = 0;
            foreach (Type t in typeof(MainChar).Assembly.GetTypes())
            {
                if (!typeof(StateMachineBehaviour).IsAssignableFrom(t) || t == typeof(DeathAnimatorState)) continue;
                foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    if (!m.Name.StartsWith("OnState")) continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 0 || ps[0].ParameterType != typeof(Animator)) continue;
                    try { h.Patch(m, prefixo); n++; } catch (Exception e) { Plugin.Inst.Log("aviso: nao foi possivel proteger " + t.Name + "." + m.Name + ": " + e.Message); }
                }
            }
            return n;
        }

        static bool PularNoFantasma(object[] __args)
        {
            return !(__args.Length > 0 && Ghost.IsGhost(__args[0] as Animator));
        }

        // Death() desliga o colisor do herói; em co-op o corpo precisa ficar no chão para o amigo alcançar.
        [HarmonyPostfix, HarmonyPatch(typeof(MainChar), "Death")]
        static void DepoisDeMorrer(MainChar __instance, MainChar.DeathType deathType)
        {
            if (CoopVida.Inst == null || Plugin.Inst == null || Plugin.Inst.Net == null || deathType == MainChar.DeathType.DeadFromAbyss) return;
            var rb = __instance.GetComponent<Rigidbody>();
            if (rb != null) { rb.velocity = Vector3.zero; rb.isKinematic = true; }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(SleepingScreen), "HideScreen")]
        static bool AntesDeSairDaCama()
        {
            return CoopVida.Inst == null || CoopVida.Inst.PodeTerminarDescanso();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(SleepingScreen), "EndRest")]
        static void DepoisDoDescanso()
        {
            if (CoopVida.Inst != null) CoopVida.Inst.DescansoTerminado();
        }

        // O descanso tambem termina por outros caminhos (pesadelo, sonho normal/bom, descanso sem tela). Todos salvam o
        // jogo e precisam trazer de volta o amigo que caiu no abismo.
        [HarmonyPostfix, HarmonyPatch(typeof(DreamScreen), "EndNightmares")]
        static void DepoisDoPesadelo() { if (CoopVida.Inst != null) CoopVida.Inst.DescansoTerminado(); }

        [HarmonyPostfix, HarmonyPatch(typeof(DreamScreen), "EndNormal")]
        static void DepoisDoSonho() { if (CoopVida.Inst != null) CoopVida.Inst.DescansoTerminado(); }

        [HarmonyPostfix, HarmonyPatch(typeof(DreamScreen), "EndPositive")]
        static void DepoisDoSonhoBom() { if (CoopVida.Inst != null) CoopVida.Inst.DescansoTerminado(); }

        [HarmonyPostfix, HarmonyPatch(typeof(Actions.SleepingPlace), "UsualRest")]
        static void DepoisDoDescansoComum() { if (CoopVida.Inst != null) CoopVida.Inst.DescansoTerminado(); }
    }
}
