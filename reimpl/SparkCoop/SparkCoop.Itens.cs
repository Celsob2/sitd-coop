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
// SparkCoop - achados compartilhados.
//  - Tabuletas (Actions.TextPlateAction): quando um le, o outro ganha o poema no diario e a tabuleta fica lida.
//    Tabuletas sao objetos fixos do mapa, com o mesmo ID de save nos dois jogos.
//  - Itens unicos (artefato, anel, amuleto, chave, livro, diario, livro de conhecimento, caveira, nota de missao):
//    quando um pega, o mesmo item some do mapa do outro e aparece no inventario dele (mesmo ID); quando um usa/consome,
//    some do inventario do outro. Armas, armaduras e consumiveis continuam separados para cada jogador.
using System;
using System.Collections.Generic;
using HandItems;
using HarmonyLib;
using Management;
using UnityEngine;

namespace SparkCoop
{
    public class Itens : MonoBehaviour
    {
        public static Itens Inst;
        public static bool Aplicando;   // aplicando algo vindo do outro jogo (nao reenviar)

        static Plugin P { get { return Plugin.Inst; } }

        void Awake()
        {
            Inst = this;
            new Harmony(Plugin.Guid + ".itens").PatchAll(typeof(ItensPatches));
            P.GameEvent += OnEvento;
        }

        public static bool PodeEnviar
        {
            get
            {
                return !Aplicando && P != null && P.Net != null && P.Net.HasPeer && P.LocalWorldHash != "" &&
                       P.LocalWorldHash == P.RemoteWorldHash && Time.timeSinceLevelLoad > 5f;
            }
        }

        public static bool Compartilhado(HandItem item)
        {
            if (item == null) return false;
            if (item is Artifact || item is Ring || item is Talisman || item is Key || item is Book || item is Diary ||
                item is KnowledgeBook || item is YargeeSkull) return true;
            Note n = item as Note;
            return n != null && n.IsQuest();
        }

        public static string IdDe(Component c) { SaveableObject so = c != null ? c.GetComponent<SaveableObject>() : null; return so != null ? so.GetUniqueID() : ""; }
        static string PrefabDe(Component c) { SaveableObject so = c != null ? c.GetComponent<SaveableObject>() : null; return so != null ? so.PrefabName : ""; }
        static string Num(float f) { return f.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture); }
        static float Ler(string s) { return float.Parse(s, System.Globalization.CultureInfo.InvariantCulture); }

        static Inventory Inventario { get { return MainChar.Instance != null ? MainChar.Instance.Inventory : null; } }

        // ---------------------------------------------------------------- envio
        public void TabuletaLida(Actions.TextPlateAction t)
        {
            Vector3 v = t.transform.position;
            P.SendEvent("tabuleta", IdDe(t) + ";" + t.PoemId + ";" + Num(v.x) + ";" + Num(v.y) + ";" + Num(v.z));
        }

        public void ItemPego(HandItem item, Vector3 ondeEstava)
        {
            string nome = item.name.Replace("(Clone)", "").Trim();
            P.SendEvent("item-pego", IdDe(item) + ";" + PrefabDe(item) + ";" + item.GetType().Name + ";" + Num(ondeEstava.x) + ";" + Num(ondeEstava.y) + ";" +
                                     Num(ondeEstava.z) + ";" + nome.Replace(";", ","));
        }

        public void ItemUsado(HandItem item) { P.SendEvent("item-usado", IdDe(item) + ";" + item.GetType().Name); }

        // ---------------------------------------------------------------- recebimento
        void OnEvento(string nome, string texto)
        {
            try
            {
                if (nome == "tabuleta") AplicarTabuleta(texto.Split(';'));
                else if (nome == "item-pego") AplicarItemPego(texto.Split(new[] { ';' }, 7));
                else if (nome == "item-usado") AplicarItemUsado(texto.Split(';'));
            }
            catch (Exception e) { P.Log("achados: falha ao aplicar " + nome + ": " + e.Message); }
        }

        void AplicarTabuleta(string[] f)
        {
            if (f.Length != 5) return;
            Vector3 pos = new Vector3(Ler(f[2]), Ler(f[3]), Ler(f[4]));
            Actions.TextPlateAction alvo = null;
            foreach (Actions.TextPlateAction t in FindObjectsOfType<Actions.TextPlateAction>(true))
                if ((f[0] != "" && IdDe(t) == f[0]) || (t.transform.position - pos).sqrMagnitude < 0.25f) { alvo = t; break; }
            int poema = int.Parse(f[1]);
            bool novo = false;
            Inventory inv = Inventario;
            if (inv != null && UI.LocalizationManager.Instance != null)
            {
                UI.LocalizedPoem lp = UI.LocalizationManager.Instance.GetPoem(poema, Game.GetLevelNumber());
                if (lp != null) novo = inv.AddPoem(lp);
            }
            if (alvo != null)
            {
                var oi = (ObjectInteraction)AccessTools.Field(typeof(Actions.TextPlateAction), "_objectInteraction").GetValue(alvo);
                if (oi != null) oi.IsKnownObject = true;
                try { AccessTools.Method(typeof(Actions.TextPlateAction), "ClearDust").Invoke(alvo, null); } catch (Exception) { }
            }
            P.Log("achados: tabuleta lida pelo outro (" + (alvo != null ? alvo.name : "nao achada") + ", poema " + poema + (novo ? ", novo no diario" : ", ja conhecido") + ")");
            if (novo && CoopVida.Inst != null) CoopVida.Inst.Aviso(P.PeerName + L.T(" leu uma tabuleta: poema adicionado ao seu diário.", " read a tablet: poem added to your journal."));
        }

        void AplicarItemPego(string[] f)
        {
            if (f.Length != 7) return;
            string id = f[0], prefab = f[1], tipo = f[2], nomeItem = f[6];
            Vector3 pos = new Vector3(Ler(f[3]), Ler(f[4]), Ler(f[5]));
            Inventory inv = Inventario;
            if (inv == null) return;
            HandItem item = Achar(id, prefab, tipo, pos);
            string como;
            if (item != null) como = "o mesmo item do mapa";
            else
            {
                if (string.IsNullOrEmpty(prefab) || SaveGameManager.Instance == null) { P.Log("achados: item " + nomeItem + " sem modelo para criar"); return; }
                GameObject go = SaveGameManager.Instance.CreateSaveableObject(prefab, MainChar.Instance.transform.position, null);
                item = go != null ? go.GetComponent<HandItem>() : null;
                if (item == null) { P.Log("achados: nao consegui criar " + prefab); return; }
                como = "copia criada (nao achei no mapa)";
            }
            TrocarId(item, id);
            Aplicando = true;
            try { inv.AddItem(item, true, false, false); }
            finally { Aplicando = false; }
            P.Log("achados: " + nomeItem + " (" + tipo + ") pego pelo outro -> no meu inventario (" + como + ")");
            if (CoopVida.Inst != null) CoopVida.Inst.Aviso(P.PeerName + L.T(" pegou ", " picked up ") + nomeItem + L.T(": está no seu inventário também.", ": it is in your inventory too."));
        }

        void AplicarItemUsado(string[] f)
        {
            if (f.Length < 1 || f[0] == "") return;
            Inventory inv = Inventario;
            if (inv == null || inv.InventoryList == null) return;
            foreach (HandItem it in new List<HandItem>(inv.InventoryList))
            {
                if (it == null || IdDe(it) != f[0]) continue;
                Aplicando = true;
                try { inv.DestroyItem(it); }
                finally { Aplicando = false; }
                P.Log("achados: " + it.name + " usado pelo outro -> removido do meu inventario");
                return;
            }
            P.Log("achados: item usado pelo outro nao esta no meu inventario (" + f[0] + ")");
        }

        // Mesmo ID (mundo carregado do host) ou mesmo modelo/tipo perto de onde o outro pegou (fora do inventario).
        HandItem Achar(string id, string prefab, string tipo, Vector3 pos)
        {
            if (id != "" && SaveGameManager.Instance != null)
            {
                SaveableObject so = SaveGameManager.Instance.GetSaveableObject(id);
                HandItem h = so != null ? so.GetComponent<HandItem>() : null;
                if (h != null && !NoInventario(h)) return h;
            }
            HandItem melhor = null; float md = 2.5f * 2.5f;
            foreach (HandItem h in FindObjectsOfType<HandItem>(true))
            {
                if (h.GetType().Name != tipo || PrefabDe(h) != prefab || NoInventario(h)) continue;
                float d = (h.transform.position - pos).sqrMagnitude;
                if (d < md) { md = d; melhor = h; }
            }
            return melhor;
        }

        static bool NoInventario(HandItem h)
        {
            Inventory inv = Inventario;
            return inv != null && inv.InventoryList != null && inv.InventoryList.Contains(h);
        }

        static void TrocarId(HandItem item, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            SaveableObject so = item.GetComponent<SaveableObject>();
            if (so == null || so.GetUniqueID() == id || SaveGameManager.Instance == null) return;
            SaveGameManager.Instance.UnregisterSaveableObject(so);
            so.SetUniqueID(id);
            SaveGameManager.Instance.RegisterSaveableObject(so);
        }
    }

    public static class ItensPatches
    {
        [HarmonyPostfix, HarmonyPatch(typeof(Actions.TextPlateAction), "ShowContent")]
        static void LeuTabuleta(Actions.TextPlateAction __instance) { if (Itens.PodeEnviar) Itens.Inst.TabuletaLida(__instance); }

        [HarmonyPrefix, HarmonyPatch(typeof(Inventory), "AddItem")]
        static void AntesDePegar(HandItem item, out Vector3 __state) { __state = item != null ? item.transform.position : Vector3.zero; }

        [HarmonyPostfix, HarmonyPatch(typeof(Inventory), "AddItem")]
        static void DepoisDePegar(HandItem item, bool loadedFromSave, Vector3 __state)
        {
            if (loadedFromSave || !Itens.PodeEnviar || !Itens.Compartilhado(item)) return;
            Itens.Inst.ItemPego(item, __state);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Inventory), "DestroyItem")]
        static void AntesDeDestruir(HandItem item) { if (Itens.PodeEnviar && Itens.Compartilhado(item)) Itens.Inst.ItemUsado(item); }
    }
}
