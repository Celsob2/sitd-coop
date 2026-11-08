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
// SparkCoop - HUD extra: ping (ida e volta ate o outro jogador, em ms) e seta na borda da tela apontando o amigo quando ele
// esta fora da tela, com nome e distancia. O ping usa eventos "ping"/"pong" a cada segundo.
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SparkCoop
{
    public class Hud : MonoBehaviour
    {
        public static Hud Inst;
        public float PingMs = -1f;
        float _proxPing;
        Texture2D _seta;

        static Plugin P { get { return Plugin.Inst; } }

        // Somente testes: -coop-janela 1920x1080 forca a janela nesse tamanho (o jogo aplica a resolucao das proprias
        // configuracoes ao abrir e ignora -screen-width/-screen-height). Assim duas janelas cabem lado a lado.
        int _janelaL, _janelaA;
        float _proxJanela;

        void Awake()
        {
            Inst = this;
            P.GameEvent += OnEvento;
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == "-coop-janela")
                {
                    string[] wh = a[i + 1].ToLowerInvariant().Split('x');
                    if (wh.Length == 2) { int.TryParse(wh[0], out _janelaL); int.TryParse(wh[1], out _janelaA); }
                }
        }

        void ForcarJanela()
        {
            if (_janelaL <= 0 || _janelaA <= 0 || Time.realtimeSinceStartup < _proxJanela) return;
            _proxJanela = Time.realtimeSinceStartup + 2f;
            if (Screen.width == _janelaL && Screen.height == _janelaA && Screen.fullScreenMode == FullScreenMode.Windowed) return;
            Screen.SetResolution(_janelaL, _janelaA, FullScreenMode.Windowed);
            P.Log("janela forcada para " + _janelaL + "x" + _janelaA + " (estava " + Screen.width + "x" + Screen.height + ")");
        }

        void Update()
        {
            ForcarJanela();
            if (P == null || P.Net == null || !P.Net.HasPeer) { PingMs = -1f; return; }
            if (Time.realtimeSinceStartup < _proxPing) return;
            _proxPing = Time.realtimeSinceStartup + 1f;
            P.SendEvent("ping", Time.realtimeSinceStartup.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }

        void OnEvento(string nome, string texto)
        {
            if (nome == "ping") P.SendEvent("pong", texto);
            else if (nome == "pong")
            {
                float t;
                if (!float.TryParse(texto, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out t)) return;
                float ms = (Time.realtimeSinceStartup - t) * 1000f;
                PingMs = PingMs < 0 ? ms : Mathf.Lerp(PingMs, ms, 0.3f);   // suavizado
            }
        }

        void OnGUI()
        {
            if (P == null || P.Net == null || !P.CfgHud.Value) return;
            // Ping logo abaixo do painel de status do canto superior esquerdo.
            string ping = PingMs < 0 ? L.T("ping: --", "ping: --") : L.T("ping: ", "ping: ") + Mathf.RoundToInt(PingMs) + " ms";
            Color cor = PingMs < 0 ? Color.gray : PingMs < 80 ? new Color(0.5f, 1f, 0.5f) : PingMs < 160 ? new Color(1f, 0.85f, 0.3f) : new Color(1f, 0.45f, 0.4f);
            Color antes = GUI.color; GUI.color = cor;
            GUI.Label(new Rect(18, 90, 300, 22), "<b>" + ping + "</b>");
            GUI.color = antes;
            SetaDoAmigo();
        }

        void SetaDoAmigo()
        {
            Ghost g = P.Ghost;
            Camera cam = Camera.main;
            if (g == null || !g.Visible || cam == null || MainChar.Instance == null) return;
            Vector3 alvo = g.Position + Vector3.up * 1.5f;
            Vector3 sp = cam.WorldToScreenPoint(alvo);
            bool atras = sp.z < 0;
            float margem = 40f;
            bool naTela = !atras && sp.x > margem && sp.x < Screen.width - margem && sp.y > margem && sp.y < Screen.height - margem;
            if (naTela) return;   // o rotulo com o nome sobre o fantasma ja aparece
            // Direcao a partir do centro da tela (atras da camera: inverte).
            Vector2 c = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 d = new Vector2(sp.x, sp.y) - c;
            if (atras) d = -d;
            if (d.sqrMagnitude < 1f) d = Vector2.down;
            d.Normalize();
            float kx = (Screen.width / 2f - margem) / Mathf.Max(0.001f, Mathf.Abs(d.x));
            float ky = (Screen.height / 2f - margem) / Mathf.Max(0.001f, Mathf.Abs(d.y));
            Vector2 borda = c + d * Mathf.Min(kx, ky);
            // Coordenadas de GUI tem o Y invertido.
            Vector2 pos = new Vector2(borda.x, Screen.height - borda.y);
            float ang = Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg;
            if (_seta == null) _seta = CriarSeta();
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(ang, pos);
            Color antes = GUI.color; GUI.color = new Color(1f, 0.8f, 0.35f, 0.95f);
            GUI.DrawTexture(new Rect(pos.x - 16, pos.y - 16, 32, 32), _seta);
            GUI.color = antes;
            GUI.matrix = m;
            float dist = Vector3.Distance(MainChar.Instance.transform.position, g.Position);
            var estilo = new GUIStyle(GUI.skin.label); estilo.alignment = TextAnchor.MiddleCenter; estilo.richText = true;
            Vector2 txt = pos + new Vector2(-d.x, d.y) * 34f;   // rotulo um pouco para dentro da tela
            // No abismo o fantasma continua caindo sem parar; nao mostra metros absurdos.
            string onde = CoopVida.Inst != null && CoopVida.Inst.AmigoAbismo ? L.T("no abismo", "in the abyss")
                        : dist > 300f ? "+300 m" : Mathf.RoundToInt(dist) + " m";
            GUI.Label(new Rect(txt.x - 90, txt.y - 11, 180, 22), "<b>" + P.PeerName + "</b> " + onde, estilo);
        }

        // Triangulo branco apontando para a direita (a rotacao da GUI gira para a direcao certa).
        static Texture2D CriarSeta()
        {
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.ARGB32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = x / (float)(n - 1), fy = Mathf.Abs(y / (float)(n - 1) - 0.5f) * 2f;
                    bool dentro = fx > 0.15f && fy < (1f - fx) * 1.1f;
                    t.SetPixel(x, y, dentro ? Color.white : new Color(1, 1, 1, 0));
                }
            t.Apply();
            return t;
        }
    }
}
