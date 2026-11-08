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
// SparkCoop - idioma dos textos que o jogador ve (painel, avisos, botoes): portugues do Brasil ou ingles.
// Os logs continuam em portugues. Config [Coop] Language = auto | pt | en; "auto" segue o idioma escolhido no jogo.
using System;
using BepInEx.Configuration;
using UnityEngine;

namespace SparkCoop
{
    public static class L
    {
        static ConfigEntry<string> _cfg;
        static string _ultimo = "";

        static ConfigEntry<string> Cfg
        {
            get
            {
                if (_cfg == null && Plugin.Inst != null)
                    _cfg = Plugin.Inst.Config.Bind("Coop", "Language", "auto",
                        "Idioma dos textos do mod / mod text language: auto (segue o jogo / follows the game) | pt | en");
                return _cfg;
            }
        }

        // true = portugues; false = ingles (qualquer idioma do jogo que nao seja portugues).
        public static bool Pt
        {
            get
            {
                string v = Cfg == null ? "auto" : (Cfg.Value ?? "").Trim().ToLowerInvariant();
                bool pt;
                if (v == "pt" || v == "pt-br" || v == "ptbr") pt = true;
                else if (v == "en") pt = false;
                else pt = IdiomaDoJogo() == SystemLanguage.Portuguese;
                string agora = pt ? "pt" : "en";
                if (agora != _ultimo) { _ultimo = agora; if (Plugin.Inst != null) Plugin.Inst.Log("idioma do mod: " + agora + " (config " + v + ")"); }
                return pt;
            }
        }

        static SystemLanguage IdiomaDoJogo()
        {
            try
            {
                if (UI.LocalizationManager.Instance != null) return UI.LocalizationManager.Instance.CurrentLanguage;
            }
            catch (Exception) { }
            return Application.systemLanguage;
        }

        public static string T(string pt, string en) { return Pt ? pt : en; }

        // Botao do painel: troca para o outro idioma e grava na config.
        public static void Alternar()
        {
            if (Cfg != null) Cfg.Value = Pt ? "en" : "pt";
        }

        public static string Sigla { get { return Pt ? "PT-BR" : "EN"; } }
    }
}
