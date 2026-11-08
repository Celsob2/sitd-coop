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
// SparkCoop - aparência real do outro jogador no fantasma.
// Cada jogo descreve o visual do próprio herói (classe, cabelo/barba/bigode/cor, armadura e cada item visível com o
// ponto do corpo onde está pendurado) e envia ao outro como evento "aparencia". O outro monta o fantasma com essa
// receita usando as mesmas peças e funções do jogo: prefabs por nome (SaveGameManager.TryResolveDynamicPrefab),
// armadura presa ao esqueleto pelos nomes dos ossos (como MainChar.SetArmorToSkeleton), cabeça e cabelo por
// PlayerHead.SelectHead e formato do corpo por HeroBodyShape.Apply.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using UnityEngine;
using HandItems;
using Management;
using UI;

namespace SparkCoop
{
    public class Aparencia
    {
        // Inativos: partes do item desligadas no herói de verdade (o jogo liga/desliga peças ao vestir/segurar, ex.: a versão
        // "caída no chão" de cada item e as peças que somem em cadáveres). O fantasma espelha esse estado.
        public class Item { public string Prefab, Caminho, Inativos = ""; public Vector3 Pos, Rot, Escala; }

        public int Classe, Cabelo, Barba, Bigode, CorCabelo;
        public string Armadura = "", ArmaduraInativos = "";
        public readonly List<Item> Itens = new List<Item>();

        static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        // ---------------------------------------------------------------- descrever o herói local
        public static Aparencia DoHeroiLocal()
        {
            MainChar mc = MainChar.Instance;
            if (mc == null) return null;
            var a = new Aparencia();
            a.Classe = (int)mc.Class;
            CharacterAppearance ap = Game.Appearance;
            if (ap == null || ap.Class != mc.Class)
            {
                CharacterPreset preset = SaveGameManager.Instance != null ? SaveGameManager.Instance.GetCharacterPreset(mc.Class) : null;
                ap = preset != null ? CharacterAppearance.FromPreset(preset) : null;
            }
            if (ap != null) { a.Cabelo = (int)ap.HairStyle; a.Barba = (int)ap.BeardStyle; a.Bigode = (int)ap.MustacheStyle; a.CorCabelo = (int)ap.HairColor; }
            Armor armor = mc.GetArmor();
            if (armor != null) { a.Armadura = NomeDoPrefab(armor); a.ArmaduraInativos = PartesInativas(armor.transform); }
            Transform inventario = mc.Inventory != null ? mc.Inventory.transform : null;
            foreach (HandItem h in mc.GetComponentsInChildren<HandItem>(false))
            {
                if (h is Armor || h.transform.parent == null || !EhItemDeVerdade(h)) continue;
                if (inventario != null && h.transform.IsChildOf(inventario)) continue;
                var it = new Item();
                it.Prefab = NomeDoPrefab(h);
                it.Caminho = Caminho(h.transform.parent, mc.transform);
                it.Pos = h.transform.localPosition; it.Rot = h.transform.localEulerAngles; it.Escala = h.transform.localScale;
                it.Inativos = PartesInativas(h.transform);
                a.Itens.Add(it);
            }
            return a;
        }

        const char SepPartes = '^';

        static string PartesInativas(Transform raiz)
        {
            var l = new List<string>();
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                if (t != raiz && !t.gameObject.activeSelf) l.Add(Caminho(t, raiz));
            return string.Join(SepPartes.ToString(), l.ToArray());
        }

        // Liga todas as partes do item e desliga as que estavam desligadas no herói de verdade.
        static void EspelharPartes(Transform raiz, string inativos)
        {
            var off = new HashSet<string>(inativos.Split(new[] { SepPartes }, StringSplitOptions.RemoveEmptyEntries));
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                if (t != raiz) t.gameObject.SetActive(!off.Contains(Caminho(t, raiz)));
        }

        // Os "punhos" (Left_Weapon/Right_Weapon) são HandItems que fazem parte do corpo do herói e seguram as armas;
        // itens de verdade (armas, escudo, lamparina...) são objetos salváveis criados a partir de um prefab.
        static bool EhItemDeVerdade(HandItem h) { return h.GetComponent<SaveableObject>() != null; }

        static string NomeDoPrefab(Component c)
        {
            SaveableObject so = c.GetComponent<SaveableObject>();
            if (so != null && !string.IsNullOrEmpty(so.PrefabName)) return so.PrefabName;
            return c.gameObject.name.Replace("(Clone)", "").Trim();
        }

        static string Caminho(Transform t, Transform raiz)
        {
            if (t == raiz) return "";
            string p = t.name;
            for (t = t.parent; t != null && t != raiz; t = t.parent) p = t.name + "/" + p;
            return p;
        }

        // ---------------------------------------------------------------- texto (evento de rede)
        public string Serializar()
        {
            var sb = new StringBuilder();
            sb.Append(Classe).Append('|').Append(Cabelo).Append('|').Append(Barba).Append('|').Append(Bigode).Append('|').Append(CorCabelo).Append('|').Append(Armadura).Append('|').Append(ArmaduraInativos);
            foreach (Item it in Itens)
                sb.Append('\n').Append(it.Prefab).Append('|').Append(it.Caminho).Append('|').Append(V(it.Pos)).Append('|').Append(V(it.Rot)).Append('|').Append(V(it.Escala)).Append('|').Append(it.Inativos);
            return sb.ToString();
        }

        public static Aparencia Ler(string s)
        {
            string[] linhas = s.Split('\n');
            string[] c = linhas[0].Split('|');
            var a = new Aparencia();
            a.Classe = int.Parse(c[0]); a.Cabelo = int.Parse(c[1]); a.Barba = int.Parse(c[2]); a.Bigode = int.Parse(c[3]); a.CorCabelo = int.Parse(c[4]);
            a.Armadura = c[5];
            if (c.Length > 6) a.ArmaduraInativos = c[6];
            for (int i = 1; i < linhas.Length; i++)
            {
                string[] f = linhas[i].Split('|');
                if (f.Length < 5) continue;
                var it = new Item(); it.Prefab = f[0]; it.Caminho = f[1]; it.Pos = LerV(f[2]); it.Rot = LerV(f[3]); it.Escala = LerV(f[4]);
                if (f.Length > 5) it.Inativos = f[5];
                a.Itens.Add(it);
            }
            return a;
        }

        // Arredondado: pequenas variações quadro a quadro não podem mudar a receita (senão o fantasma é remontado à toa).
        static string V(Vector3 v) { return v.x.ToString("0.00", CI) + "," + v.y.ToString("0.00", CI) + "," + v.z.ToString("0.00", CI); }
        static Vector3 LerV(string s) { string[] p = s.Split(','); return new Vector3(float.Parse(p[0], CI), float.Parse(p[1], CI), float.Parse(p[2], CI)); }

        // O que muda o visual de verdade (sem posições): usada para decidir se precisa reenviar/remontar.
        public string Assinatura()
        {
            var sb = new StringBuilder();
            sb.Append(Classe).Append('|').Append(Cabelo).Append('|').Append(Barba).Append('|').Append(Bigode).Append('|').Append(CorCabelo).Append('|').Append(Armadura);
            foreach (Item it in Itens) sb.Append(';').Append(it.Prefab).Append('@').Append(it.Caminho);
            return sb.ToString();
        }

        public string Resumo()
        {
            return "classe " + (CharacterClass)Classe + ", armadura " + (Armadura == "" ? "-" : Armadura) + ", " + Itens.Count + " itens visiveis";
        }

        // ---------------------------------------------------------------- montar no clone (ainda inativo, antes de remover os scripts)
        public void AplicarNoClone(GameObject clone)
        {
            MainChar cm = clone.GetComponent<MainChar>();
            if (cm == null) { Plugin.Inst.Log("aparencia: clone sem MainChar"); return; }
            var cls = (CharacterClass)Classe;
            cm.Class = cls;

            // 1. Tira todos os itens do herói local (armadura, armas, lamparina, inventário).
            var locais = new List<GameObject>();
            foreach (HandItem h in clone.GetComponentsInChildren<HandItem>(true)) if (EhItemDeVerdade(h)) locais.Add(h.gameObject);
            foreach (GameObject g in locais) if (g != null) UnityEngine.Object.DestroyImmediate(g);

            // 2. Armadura do outro jogador, presa ao esqueleto do clone pelo nome dos ossos.
            var ossos = new Dictionary<string, Transform>();
            if (cm.BonesRoot != null)
                foreach (Transform t in cm.BonesRoot.GetComponentsInChildren<Transform>(true)) if (!ossos.ContainsKey(t.name)) ossos[t.name] = t;
            Armor armor = null;
            GameObject prefab;
            if (Armadura != "" && Resolver(Armadura, out prefab) && cm.ArmorPoint != null)
            {
                GameObject inst = UnityEngine.Object.Instantiate(prefab, cm.ArmorPoint);
                inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity;
                int semOsso = 0;
                foreach (SkinnedMeshRenderer r in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Transform[] b = r.bones;
                    for (int i = 0; i < b.Length; i++)
                    {
                        Transform novo;
                        if (b[i] != null && ossos.TryGetValue(b[i].name, out novo)) b[i] = novo; else semOsso++;
                    }
                    r.bones = b;
                    Transform rb;
                    if (r.rootBone != null && ossos.TryGetValue(r.rootBone.name, out rb)) r.rootBone = rb;
                }
                EspelharPartes(inst.transform, ArmaduraInativos);
                inst.SetActive(true);
                armor = inst.GetComponent<Armor>();
                if (armor != null && cm.Hands != null) foreach (GameObject h in cm.Hands) if (h != null) h.SetActive(!armor.DisableHeroHands);
                Plugin.Inst.Log("aparencia: armadura " + Armadura + (semOsso > 0 ? " (" + semOsso + " ossos sem par)" : ""));
            }
            else if (Armadura != "") Plugin.Inst.Log("aparencia: armadura nao encontrada: " + Armadura);
            // Corpo "sem armadura": o jogo esconde MainChar.Body ao vestir armadura e mostra ao tirar (EnableBody). O clone
            // vem do heroi local; sem isto, um outro jogador sem armadura aparecia so com cabeca e mochila.
            if (cm.Body != null) cm.Body.SetActive(armor == null);
            if (armor == null && cm.Hands != null) foreach (GameObject h in cm.Hands) if (h != null) h.SetActive(true);

            // 3. Cabeça, cabelo, barba, bigode e cor (função do próprio jogo).
            try
            {
                CharacterPreset preset = SaveGameManager.Instance.GetCharacterPreset(cls);
                var ap = new CharacterAppearance();
                ap.Class = cls; ap.HairStyle = (PlayerHairStyle)Cabelo; ap.BeardStyle = (PlayerBeardStyle)Barba;
                ap.MustacheStyle = (PlayerMustacheStyle)Bigode; ap.HairColor = (CharacterHairColor)CorCabelo;
                PlayerHead.SelectHead(cm.Heads, cls, cm.HeadAppearanceRoot, armor, preset, ap);
            }
            catch (Exception e) { Plugin.Inst.Log("aparencia: falha na cabeca: " + e.Message); }

            // 4. Itens visíveis (armas, escudo, lamparina, aljava...) nos mesmos pontos do corpo.
            foreach (Item it in Itens)
            {
                Transform pai = it.Caminho == "" ? clone.transform : clone.transform.Find(it.Caminho);
                if (pai == null || !Resolver(it.Prefab, out prefab)) { Plugin.Inst.Log("aparencia: item ignorado " + it.Prefab + " em " + it.Caminho); continue; }
                GameObject g = UnityEngine.Object.Instantiate(prefab, pai);
                g.transform.localPosition = it.Pos; g.transform.localEulerAngles = it.Rot; g.transform.localScale = it.Escala;
                EspelharPartes(g.transform, it.Inativos);
                g.SetActive(true);
            }
        }

        static bool Resolver(string nome, out GameObject prefab)
        {
            prefab = null;
            if (SaveGameManager.Instance == null) return false;
            object[] args = { nome, null };
            bool ok = (bool)AccessTools.Method(typeof(SaveGameManager), "TryResolveDynamicPrefab").Invoke(SaveGameManager.Instance, args);
            prefab = args[1] as GameObject;
            return ok && prefab != null;
        }
    }
}
