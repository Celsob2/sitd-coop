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
// SparkCoop - "continuar" em co-op: o host envia o mundo dele e o amigo entra nele com o proprio heroi.
//
// O save do jogo e um JSON (Management.GameData) com LevelData, GeneralData, PlayerData (o heroi), StaticObjectStates e
// DynamicObjects. O host, quando carrega ou salva o jogo, guarda o caminho do save; com o amigo conectado, envia esse save
// (decifrado, comprimido, em partes). O amigo escolhe um heroi dele no painel do menu: o mod junta o mundo do host com o
// PlayerData do heroi do amigo (mais os objetos do inventario dele), grava "coop_<save do amigo>.save" e carrega pelo
// proprio jogo. Os saves seguintes do amigo vao para esse arquivo; o save solo dele fica intacto.
// Depois de carregar, criaturas sao identificadas pelo ID de save do jogo (igual nos dois mundos), nao pela posicao.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using HarmonyLib;
using Management;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SparkCoop
{
    public class Mundo : MonoBehaviour
    {
        public static Mundo Inst;
        const int TamanhoParte = 6000, Janela = 4;

        static Plugin P { get { return Plugin.Inst; } }

        // ---- host
        public string ArquivoDoHost;          // save do mundo do host (o ultimo carregado ou salvo)
        int _envioId, _envioAck = -1, _envioProx;
        List<string> _partes;
        bool _hadPeer;
        string _enviadoArquivo; DateTime _enviadoData;

        // ---- amigo
        public string JsonDoHost;             // mundo recebido (JSON do GameData)
        public string ResumoDoHost = "";
        int _recId = -1, _recTotal; string[] _recPartes; int _recCount; string _recResumo = "";

        // Carregamento a partir do menu ja comecou: um segundo clique carregaria a fase duas vezes e travava o jogo.
        public static bool Carregando;

        void Awake()
        {
            Inst = this;
            new Harmony(Plugin.Guid + ".mundo").PatchAll(typeof(MundoPatches));
            P.GameEvent += OnEvento;
            SceneManager.sceneLoaded += (cena, modo) => { if (cena.name == "StartMenu") Carregando = false; };
        }

        // ---------------------------------------------------------------- host: envio
        float _proxPedido;

        void Update()
        {
            if (P == null || P.Net == null) { _hadPeer = false; return; }
            bool peer = P.Net.HasPeer;
            if (P.Role == "client" && peer && JsonDoHost == null && _recPartes == null && Time.realtimeSinceStartup > _proxPedido)
            {
                _proxPedido = Time.realtimeSinceStartup + 10f;
                P.SendEvent("mundo-pedir", "");
            }
            if (P.Role == "host" && peer && ArquivoDoHost != null && File.Exists(ArquivoDoHost))
            {
                DateTime data = File.GetLastWriteTimeUtc(ArquivoDoHost);
                if (!_hadPeer || _enviadoArquivo != ArquivoDoHost || data != _enviadoData) IniciarEnvio(data);
            }
            _hadPeer = peer;
            if (_partes != null)
                while (_envioProx < _partes.Count && _envioProx <= _envioAck + Janela)
                {
                    P.SendEvent("mundo-parte", _envioId + ";" + _envioProx + ";" + _partes[_envioProx]);
                    _envioProx++;
                }
        }

        void IniciarEnvio(DateTime data)
        {
            _enviadoArquivo = ArquivoDoHost; _enviadoData = data;
            string json = LerJson(ArquivoDoHost);
            if (json == null) { P.Log("mundo: nao consegui ler o save do host " + ArquivoDoHost); return; }
            string b64 = Convert.ToBase64String(Comprimir(json));
            _partes = new List<string>();
            for (int i = 0; i < b64.Length; i += TamanhoParte) _partes.Add(b64.Substring(i, Math.Min(TamanhoParte, b64.Length - i)));
            _envioId++; _envioAck = -1; _envioProx = 0;
            GameData gd = JsonUtility.FromJson<GameData>(json);
            string resumo = (gd != null && gd.LevelData != null ? gd.LevelData.LevelName : "?") + ", " + (gd != null && gd.PlayerData != null ? gd.PlayerData.Name : "?");
            P.SendEvent("mundo-ini", _envioId + ";" + _partes.Count + ";" + resumo.Replace(";", ","));
            P.Log("mundo: enviando o save do host (" + Path.GetFileName(ArquivoDoHost) + ", " + (json.Length / 1024) + " KB de JSON, " +
                  (b64.Length / 1024) + " KB comprimido, " + _partes.Count + " partes; " + resumo + ")");
        }

        // ---------------------------------------------------------------- eventos
        void OnEvento(string nome, string texto)
        {
            string[] f;
            switch (nome)
            {
                case "mundo-pedir":
                    if (P.Role == "host" && ArquivoDoHost != null && File.Exists(ArquivoDoHost) && _partes == null) IniciarEnvio(File.GetLastWriteTimeUtc(ArquivoDoHost));
                    break;
                case "mundo-ack":
                    f = texto.Split(';'); int id, i;
                    if (f.Length == 2 && int.TryParse(f[0], out id) && int.TryParse(f[1], out i) && id == _envioId && i > _envioAck)
                    {
                        _envioAck = i;
                        if (_partes != null && _envioAck == _partes.Count - 1) { P.Log("mundo: o amigo recebeu o mundo inteiro"); _partes = null; }
                    }
                    break;
                case "mundo-ini":
                    f = texto.Split(new[] { ';' }, 3);
                    if (f.Length < 2 || P.Role != "client") return;
                    _recId = int.Parse(f[0]); _recTotal = int.Parse(f[1]); _recPartes = new string[_recTotal]; _recCount = 0;
                    _recResumo = f.Length > 2 ? f[2] : "";
                    P.Log("mundo: recebendo o mundo do host (" + _recTotal + " partes; " + _recResumo + ")");
                    break;
                case "mundo-parte":
                    f = texto.Split(new[] { ';' }, 3);
                    int rid, ri;
                    if (f.Length != 3 || !int.TryParse(f[0], out rid) || !int.TryParse(f[1], out ri) || rid != _recId || ri >= _recTotal) return;
                    if (_recPartes[ri] == null) { _recPartes[ri] = f[2]; _recCount++; }
                    // confirma a maior parte contigua recebida (controle de fluxo do host)
                    int contig = -1; while (contig + 1 < _recTotal && _recPartes[contig + 1] != null) contig++;
                    P.SendEvent("mundo-ack", _recId + ";" + contig);
                    if (_recCount == _recTotal) Montar();
                    break;
            }
        }

        void Montar()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (string s in _recPartes) sb.Append(s);
                JsonDoHost = Descomprimir(Convert.FromBase64String(sb.ToString()));
                ResumoDoHost = _recResumo;
                P.Log("mundo: mundo do host recebido (" + (JsonDoHost.Length / 1024) + " KB; " + ResumoDoHost + ")");
            }
            catch (Exception e) { P.Log("mundo: falha ao montar o mundo recebido: " + e.Message); }
            _recPartes = null;
        }

        // ---------------------------------------------------------------- amigo: juntar e carregar
        public List<string> SavesDoAmigo()
        {
            var l = new List<string>();
            try
            {
                foreach (string s in SaveGameSystem.GetSaveFileList())
                    if (!Path.GetFileName(s).StartsWith("coop_") && s != ArquivoDoHost) l.Add(s);
            }
            catch (Exception e) { P.Log("mundo: falha ao listar saves: " + e.Message); }
            return l;
        }

        public string Rotulo(string save)
        {
            try
            {
                var info = SaveGameSystem.GetShortSaveFileInfo(save);
                if (info != null) return info.HeroName + " (" + info.CharacterClass + ", " + info.LevelName + ")";
            }
            catch (Exception) { }
            return Path.GetFileNameWithoutExtension(save);
        }

        // Grava coop_<save>.save com o mundo do host + heroi do save escolhido. Devolve o caminho (ou null).
        public string CriarSaveCoop(string saveDoAmigo)
        {
            string destino = Path.Combine(Path.GetDirectoryName(saveDoAmigo), "coop_" + Path.GetFileName(saveDoAmigo));
            string json = Juntar(JsonDoHost, LerJson(saveDoAmigo));
            File.WriteAllText(destino, json);   // o jogo aceita save em JSON puro (e cifra de novo no proximo save)
            P.Log("mundo: save co-op gravado " + destino + " (mundo do host + heroi de " + Path.GetFileName(saveDoAmigo) + ")");
            return destino;
        }

        // Amigo com o mundo do host recebido: qualquer heroi escolhido (no painel ou no "Continuar" do jogo) entra no mundo do host.
        public bool DeveJuntar(string arquivo)
        {
            return P != null && P.Role == "client" && P.Net != null && JsonDoHost != null && arquivo != null &&
                   !Path.GetFileName(arquivo).StartsWith("coop_");
        }

        public void EntrarComHeroi(string saveDoAmigo)
        {
            if (Carregando) return;
            try
            {
                string destino = CriarSaveCoop(saveDoAmigo);
                var info = SaveGameSystem.GetShortSaveFileInfo(destino);
                var menu = FindObjectOfType<UI.Screens.StartMenu>();
                if (info == null || menu == null || menu.LoadingScreen == null) { P.Log("mundo: nao achei o menu para carregar"); return; }
                AccessTools.Method(typeof(UI.Screens.LoadingScreenStartMenu), "Show").Invoke(menu.LoadingScreen, new object[] { info.LevelName, info });
            }
            catch (Exception e) { P.Log("mundo: falha ao entrar no mundo do host: " + e); }
        }

        // Mundo do host + heroi (PlayerData) do amigo + objetos que o heroi do amigo carrega.
        public static string Juntar(string jsonHost, string jsonAmigo)
        {
            GameData host = JsonUtility.FromJson<GameData>(jsonHost);
            GameData amigo = JsonUtility.FromJson<GameData>(jsonAmigo);
            if (host == null || amigo == null || amigo.PlayerData == null) throw new Exception("save invalido");
            Vector3 perto = host.PlayerData != null ? host.PlayerData.PlayerPosition + new Vector3(1.2f, 0f, 0f) : amigo.PlayerData.PlayerPosition;

            // IDs de objetos do amigo citados no heroi dele (inventario, equipamento); depois, o que esta dentro deles.
            var estados = new Dictionary<string, string>();
            if (amigo.StaticObjectStates != null && amigo.StaticObjectStates.Keys != null)
                for (int i = 0; i < amigo.StaticObjectStates.Keys.Count && i < amigo.StaticObjectStates.Values.Count; i++)
                    estados[amigo.StaticObjectStates.Keys[i]] = amigo.StaticObjectStates.Values[i];
            var dinamicos = new Dictionary<string, DynamicObjectData>();
            if (amigo.DynamicObjects != null) foreach (DynamicObjectData d in amigo.DynamicObjects) if (d != null && d.ObjectID != null) dinamicos[d.ObjectID] = d;

            var levar = new HashSet<string>();
            var texto = new StringBuilder(JsonUtility.ToJson(amigo.PlayerData));
            bool mudou = true;
            while (mudou)
            {
                mudou = false;
                string t = texto.ToString();
                foreach (string id in new List<string>(estados.Keys)) if (!levar.Contains(id) && t.Contains(id)) { levar.Add(id); texto.Append(estados[id]); mudou = true; }
                foreach (string id in new List<string>(dinamicos.Keys)) if (!levar.Contains(id) && t.Contains(id)) { levar.Add(id); texto.Append(dinamicos[id].SaveData); mudou = true; }
            }

            if (host.StaticObjectStates == null) host.StaticObjectStates = new SerializableDictionary();
            if (host.DynamicObjects == null) host.DynamicObjects = new List<DynamicObjectData>();
            int nEst = 0, nDin = 0;
            foreach (string id in levar)
            {
                string est;
                if (estados.TryGetValue(id, out est))
                {
                    int k = host.StaticObjectStates.Keys.IndexOf(id);
                    if (k >= 0) host.StaticObjectStates.Values[k] = est; else { host.StaticObjectStates.Keys.Add(id); host.StaticObjectStates.Values.Add(est); }
                    nEst++;
                }
                DynamicObjectData din;
                if (dinamicos.TryGetValue(id, out din))
                {
                    host.DynamicObjects.RemoveAll(x => x != null && x.ObjectID == id);
                    host.DynamicObjects.Add(din);
                    nDin++;
                }
            }
            host.PlayerData = amigo.PlayerData;
            host.PlayerData.PlayerPosition = perto;
            host.PlayerData.LastRestPlayerPosition = perto;
            Plugin.Inst.Log("mundo: juntando - heroi " + amigo.PlayerData.Name + ", " + nEst + " objetos fixos e " + nDin + " dinamicos levados do save do amigo");
            return JsonUtility.ToJson(host);
        }

        // ---------------------------------------------------------------- utilitarios
        public static string LerJson(string caminho)
        {
            var args = new object[] { caminho, null };
            bool ok = (bool)AccessTools.Method(typeof(SaveGameSystem), "TryReadSaveJson").Invoke(null, args);
            return ok ? (string)args[1] : null;
        }

        static byte[] Comprimir(string s)
        {
            var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionMode.Compress)) { byte[] b = Encoding.UTF8.GetBytes(s); gz.Write(b, 0, b.Length); }
            return ms.ToArray();
        }

        static string Descomprimir(byte[] dados)
        {
            using (var gz = new GZipStream(new MemoryStream(dados), CompressionMode.Decompress))
            using (var r = new StreamReader(gz, Encoding.UTF8)) return r.ReadToEnd();
        }
    }

    public static class MundoPatches
    {
        // "Continuar jogo" do proprio jogo no amigo: troca o save escolhido pelo save co-op (mundo do host + esse heroi).
        [HarmonyPrefix, HarmonyPatch(typeof(UI.Screens.LoadingScreenStartMenu), "Show")]
        static bool AntesDeCarregarDoMenu(ref string sceneName, ref UI.ShortSaveFileInfo saveFileInfo)
        {
            if (Mundo.Carregando) { if (Plugin.Inst != null) Plugin.Inst.Log("mundo: clique repetido ignorado (o carregamento ja comecou)"); return false; }
            Mundo.Carregando = true;
            if (Mundo.Inst == null || saveFileInfo == null || !Mundo.Inst.DeveJuntar(saveFileInfo.FilePath)) return true;
            try
            {
                string destino = Mundo.Inst.CriarSaveCoop(saveFileInfo.FilePath);
                UI.ShortSaveFileInfo novo = SaveGameSystem.GetShortSaveFileInfo(destino);
                if (novo == null) return true;
                saveFileInfo = novo; sceneName = novo.LevelName;
                Plugin.Inst.Log("mundo: 'Continuar' redirecionado para o mundo do host (" + System.IO.Path.GetFileName(destino) + ", fase " + sceneName + ")");
            }
            catch (Exception e) { Plugin.Inst.Log("mundo: falha ao redirecionar o 'Continuar': " + e.Message); }
            return true;
        }

        // Depois de carregar um save: o host guarda o caminho (para enviar) e os dois identificam as criaturas pelo ID de save.
        [HarmonyPostfix, HarmonyPatch(typeof(SaveGameSystem), "LoadGame")]
        static void DepoisDeCarregar()
        {
            var p = Plugin.Inst;
            if (p == null || Mundo.Inst == null) return;
            if (p.Role == "host" || p.Net == null) Mundo.Inst.ArquivoDoHost = SaveGameSystem.CurrentSaveFile;
            if (p.Net == null) return;
            p.LocalWorldHash = p.Registry.Build(true);
            p.KillsToSend.Clear(); p.KnownKills.Clear(); p.PendingKills.Clear(); p.RemoteKillsApplied = 0;
            p.Log("mundo carregado do save: " + p.Registry.Count + " criaturas (por ID de save), hash " + p.LocalWorldHash);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(SaveGameSystem), "SaveGame")]
        static void DepoisDeSalvar()
        {
            var p = Plugin.Inst;
            if (p != null && Mundo.Inst != null && !p.NoSave && p.Role == "host") Mundo.Inst.ArquivoDoHost = SaveGameSystem.CurrentSaveFile;
        }
    }
}
