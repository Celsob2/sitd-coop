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
// SparkCoop - observacao (so log): o que acontece quando o jogador pega/usa/larga itens, le tabuletas e usa artefatos.
// Serve para desenhar a sincronizacao de itens unicos; nao muda nada no jogo.
using System;
using HarmonyLib;
using HandItems;
using Management;
using UnityEngine;

namespace SparkCoop
{
    public static class Observar
    {
        public static void Ligar()
        {
            var h = new Harmony(Plugin.Guid + ".observar");
            Tentar(h, typeof(Inventory), "AddItem", "PegouItem");
            Tentar(h, typeof(Inventory), "DestroyItem", "DestruiuItem");
            Tentar(h, typeof(Inventory), "DropItem", "LargouItem");
            Tentar(h, typeof(Actions.TextPlateAction), "ShowContent", "LeuTabuleta");
            Tentar(h, typeof(Actions.DoorArtifactAction), "Activate", "UsouArtefato");
            Tentar(h, typeof(Actions.LockArtifactAction), "Activate", "UsouArtefato");
            Tentar(h, typeof(Actions.PedestalAction), "Activate", "UsouArtefato");
        }

        static void Tentar(Harmony h, Type t, string metodo, string pos)
        {
            try
            {
                var m = AccessTools.Method(t, metodo);
                if (m == null) { Plugin.Inst.Log("observar: " + t.Name + "." + metodo + " nao encontrado"); return; }
                h.Patch(m, null, new HarmonyMethod(typeof(Observar).GetMethod(pos, System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)));
            }
            catch (Exception e) { Plugin.Inst.Log("observar: falha em " + t.Name + "." + metodo + ": " + e.Message); }
        }

        public static string Descrever(Component item)
        {
            if (item == null) return "null";
            SaveableObject so = item.GetComponent<SaveableObject>();
            string extra = "";
            var nota = item as Note; if (nota != null) extra = " nota id=" + nota.Id + " grupo=" + nota.GroupId + " missao=" + nota.IsQuest();
            var livro = item as Book; if (livro != null) extra = " livro id=" + livro.Id + " serie=" + livro.Serie;
            var diario = item as Diary; if (diario != null) extra = " diario grupo=" + diario.NotesGroupId + " missao=" + diario.IsQuest();
            var kb = item as KnowledgeBook; if (kb != null) extra = " livro de conhecimento " + kb.SkillType;
            return item.GetType().Name + " '" + item.name + "'" + extra + " id=" + (so != null ? so.GetUniqueID() : "-") +
                   " prefab=" + (so != null ? so.PrefabName : "-") + " pos=" + item.transform.position;
        }

        static void PegouItem(HandItem item, bool isSilent, bool loadedFromSave)
        {
            if (loadedFromSave || Plugin.Inst == null) return;
            Plugin.Inst.Log("OBS pegou: " + Descrever(item) + (isSilent ? " (silencioso)" : ""));
        }

        static void DestruiuItem(HandItem item) { if (Plugin.Inst != null) Plugin.Inst.Log("OBS destruiu/usou: " + Descrever(item)); }
        static void LargouItem(HandItem item) { if (Plugin.Inst != null) Plugin.Inst.Log("OBS largou: " + Descrever(item)); }

        static void LeuTabuleta(Actions.TextPlateAction __instance)
        {
            if (Plugin.Inst == null) return;
            SaveableObject so = __instance.GetComponent<SaveableObject>();
            Plugin.Inst.Log("OBS leu tabuleta '" + __instance.name + "' poema=" + __instance.PoemId + " texto=" + (__instance.CustomText ?? "") +
                            " id=" + (so != null ? so.GetUniqueID() : "-") + " pos=" + __instance.transform.position +
                            " poemas no inventario=" + PoemasNoInventario());
        }

        static string PoemasNoInventario()
        {
            Inventory inv = MainChar.Instance != null ? MainChar.Instance.GetComponent<Inventory>() : null;
            return inv != null && inv.Poems != null ? inv.Poems.Count.ToString() : "?";
        }

        static void UsouArtefato(object __instance)
        {
            var c = __instance as Component;
            if (Plugin.Inst != null && c != null) Plugin.Inst.Log("OBS artefato/acao " + __instance.GetType().Name + " '" + c.name + "' pos=" + c.transform.position);
        }
    }
}
