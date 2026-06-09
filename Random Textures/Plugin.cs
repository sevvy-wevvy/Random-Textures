using BepInEx;
using SeveralBees;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Utilla;
using Utilla.Attributes;

namespace RandomTextures
{
    [BepInPlugin("com.sev.gorillatag.randomtextures", "Random Textures", "1.0.0")]
    [BepInDependency("org.legoandmars.gorillatag.utilla", "1.5.0")]
    [BepInDependency("com.Sev.gorillatag.SeveralBees", "1.0.0")]
    [ModdedGamemode]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instanse;
        public string SbTokn = "";

        public float swapIntervl = 5f;

        private List<Texture2D> loadedtexs = new List<Texture2D>();
        private string texFoldr;
        private float timerr = 0f;
        private Shader urpLitShader;

        private bool inModdedRoom = false;
        private bool originlsSaved = false;
        private bool autoSwapOn = false;
        private Coroutine applyCoroutne = null;

        private struct OriginalMatState
        {
            public Renderer renderer;
            public Shader shader;
            public Texture mainTex;
        }

        private List<OriginalMatState> savedStates = new List<OriginalMatState>();

        private void Awake()
        {
            Instanse = this;
            texFoldr = Path.Combine(Paths.BepInExRootPath, "Random Texture Textures");

            if (!Directory.Exists(texFoldr))
                Directory.CreateDirectory(texFoldr);

            SeveralBees.Plugin.Instance.Startup.Add(SbStartup);
            Logger.LogInfo("[Random Textures] Loaded!");

            urpLitShader = Shader.Find("Universal Render Pipeline/Lit");

            StartCoroutine(DownloadDefaultTex());
        }

        public void SbStartup()
        {
            SbTokn = Api.Instance.GenerateToken("Random Textures", true, "Main");

            Api.Instance.SetButtonInfo(SbTokn, new List<ModButtonInfo>
            {
                new ModButtonInfo { buttonText = "Auto Swap", isTogglable = true, enabled = false, enableMethod = () => EnableAutoSwap(), disableMethod = () => DisableAutoSwap(), toolTip = "Re-randomizes every texture in the scene every few seconds." },
                new ModButtonInfo { buttonText = "Randomize", isTogglable = false, method = () => PickRandoTex(), toolTip = "Gives every object a different random texture right now." },
                new ModButtonInfo { buttonText = "Reload Texs", isTogglable = false, method = () => ReloadTexsFromFolder(), toolTip = "Reloads textures from your Random Texture Textures folder." },
                new ModButtonInfo { buttonText = "Undo", isTogglable = false, method = () => UndoTextures(), toolTip = "Restores everything back to how it looked before." },
            });

            ReloadTexsFromFolder();
        }

        [ModdedGamemodeJoin]
        private void OnJoinModded(string gamemode)
        {
            inModdedRoom = true;
        }

        [ModdedGamemodeLeave]
        private void OnLeaveModded(string gamemode)
        {
            inModdedRoom = false;
            UndoTextures();
        }

        private void Update()
        {
            if (!autoSwapOn) return;

            timerr += Time.deltaTime;
            if (timerr >= swapIntervl)
            {
                timerr = 0f;
                PickRandoTex();
            }
        }

        private void EnableAutoSwap()
        {
            autoSwapOn = true;
            timerr = 0f;
        }

        private void DisableAutoSwap()
        {
            autoSwapOn = false;
        }

        public void PickRandoTex()
        {
            if (!inModdedRoom && Photon.Pun.PhotonNetwork.InRoom)
            {
                Logger.LogWarning("[Random Textures] Not in a modded room!");
                return;
            }

            if (loadedtexs.Count == 0)
            {
                Logger.LogWarning("[Random Textures] No textures loaded!");
                return;
            }

            if (applyCoroutne != null)
                StopCoroutine(applyCoroutne);

            applyCoroutne = StartCoroutine(ApplyTexSpread());
        }

        private bool IsTmp(Renderer r)
        {
            string sname = r.material.shader.name;
            if (sname.Contains("TextMeshPro") || sname.Contains("TMP")) return true;
            if (r.GetComponent<TMPro.TMP_Text>() != null) return true;
            if (r.GetComponent<TMPro.TextMeshPro>() != null) return true;
            if (r.GetComponent<TMPro.TextMeshProUGUI>() != null) return true;
            return false;
        }

        private IEnumerator ApplyTexSpread()
        {
            Renderer[] all = Resources.FindObjectsOfTypeAll<Renderer>();

            if (!originlsSaved)
            {
                savedStates.Clear();
                foreach (var r in all)
                {
                    if (r == null || r.material == null) continue;
                    if (IsTmp(r)) continue;
                    savedStates.Add(new OriginalMatState
                    {
                        renderer = r,
                        shader = r.material.shader,
                        mainTex = r.material.mainTexture
                    });
                }
                originlsSaved = true;
            }

            int batchSze = 50;
            int cnt = 0;

            foreach (var r in all)
            {
                if (r == null || r.material == null) continue;
                if (IsTmp(r)) continue;

                if (urpLitShader != null)
                    r.material.shader = urpLitShader;
                r.material.mainTexture = loadedtexs[Random.Range(0, loadedtexs.Count)];

                cnt++;
                if (cnt >= batchSze)
                {
                    cnt = 0;
                    yield return null;
                }
            }

            applyCoroutne = null;
        }

        public void UndoTextures()
        {
            if (!originlsSaved) return;

            if (applyCoroutne != null)
            {
                StopCoroutine(applyCoroutne);
                applyCoroutne = null;
            }

            foreach (var s in savedStates)
            {
                if (s.renderer == null || s.renderer.material == null) continue;
                s.renderer.material.shader = s.shader;
                s.renderer.material.mainTexture = s.mainTex;
            }

            savedStates.Clear();
            originlsSaved = false;
            autoSwapOn = false;
        }

        public void ReloadTexsFromFolder()
        {
            loadedtexs.Clear();

            string[] files = Directory.GetFiles(texFoldr, "*.png");
            foreach (var f in files)
            {
                byte[] data = File.ReadAllBytes(f);
                Texture2D t = new Texture2D(2, 2);
                if (t.LoadImage(data))
                    loadedtexs.Add(t);
            }

            Logger.LogInfo($"[Random Textures] Loaded {loadedtexs.Count} texture(s) from folder.");
        }

        private IEnumerator DownloadDefaultTex()
        {
            if (Directory.GetFiles(texFoldr, "*.png").Length > 0) yield break;

            string savePth = Path.Combine(texFoldr, "DefaultImage.png");

            using (UnityWebRequest req = UnityWebRequestTexture.GetTexture("https://raw.githubusercontent.com/sevvy-wevvy/Random-Textures/refs/heads/main/DefaultImage.png"))
            {
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    Texture2D dlTex = DownloadHandlerTexture.GetContent(req);
                    File.WriteAllBytes(savePth, dlTex.EncodeToPNG());
                    loadedtexs.Add(dlTex);
                    Logger.LogInfo("[Random Textures] Downloaded DefaultImage.png");
                }
                else
                {
                    Logger.LogWarning("[Random Textures] Couldn't grab the default image, check your connection maybe?");
                }
            }
        }
    }
}