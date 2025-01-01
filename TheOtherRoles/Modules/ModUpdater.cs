using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using AmongUs.Data;
using Assets.InnerNet;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils;
using Mono.Cecil;
using Newtonsoft.Json.Linq;
using TMPro;
using Twitch;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Action = System.Action;
using IntPtr = System.IntPtr;
using Version = SemanticVersioning.Version;

namespace TheOtherRoles.Modules
{
    public class ModUpdateBehaviour : MonoBehaviour
    {
        public static readonly bool CheckForSubmergedUpdates = true;
        public static bool showPopUp = true;
        public static bool updateInProgress = false;

        public static AudioClip selectSfx = null;

        public static ModUpdateBehaviour Instance { get; private set; }
        public ModUpdateBehaviour(IntPtr ptr) : base(ptr) { }
        public class UpdateData
        {
            public string Tag;
            public JObject Request;
            public string Version { get { return ver != null ? "v" + ver : Tag; } }
            public string Content { get { return DataManager.Settings.Language.CurrentLanguage == SupportedLangs.Japanese ? ContentJp : ContentDefault; } }

            string ContentDefault;
            string ContentJp;
            Version ver;
            public string TimeString;

            public UpdateData(JObject data)
            {
                var t = data["tag_name"]?.ToString();
                int p = t.IndexOf('v');
                Tag = t.Substring(p != -1 ? p + 1 : 0);
                if (!SemanticVersioning.Version.TryParse(Tag, out ver))
                    ver = null;
                Request = data;

                ContentDefault = data["body"]?.ToString();
                var content = ContentDefault;
                int jpTagStartIndex = content.IndexOf("### JP");
                int jpTagEndIndex = jpTagStartIndex != -1 ? jpTagStartIndex + "### JP".Length + 2 : -1;
                int tagStartIndex = content.IndexOf("### EN");
                int tagEndIndex = tagStartIndex != -1 ? tagStartIndex + "### EN".Length + 2 : -1;
                TimeString = DateTime.FromBinary(((Il2CppSystem.DateTime)data["published_at"]).ToBinaryRaw()).ToString();

                if (jpTagStartIndex == -1 && tagStartIndex == -1)
                {
                    ContentDefault = ContentJp = content;
                }
                else if (jpTagStartIndex != -1 && tagStartIndex == -1)
                {
                    ContentDefault = ContentJp = content.Substring(tagEndIndex);
                }
                else if (jpTagStartIndex == -1 && tagStartIndex != -1)
                {
                    ContentDefault = ContentJp = content.Substring(jpTagEndIndex);
                }
                else
                {
                    if (jpTagStartIndex < tagEndIndex)
                    {
                        ContentJp = content.Substring(jpTagEndIndex, tagStartIndex - jpTagEndIndex);
                        ContentDefault = content.Substring(tagEndIndex);
                    }
                    else
                    {
                        ContentDefault = content.Substring(tagEndIndex, jpTagStartIndex - tagEndIndex);
                        ContentJp = content.Substring(jpTagEndIndex);
                    }
                }
            }

            public bool IsNewer(Version version)
            {
                if (ver == null) return false;
                return ver > version;
            }
        }

        public UpdateData TORUpdate;
        public UpdateData SubmergedUpdate;

        [HideFromIl2Cpp]
        public UpdateData RequiredUpdateData => TORUpdate ?? SubmergedUpdate;

        public void Awake()
        {
            if (Instance) Destroy(this);
            Instance = this;

            SceneManager.add_sceneLoaded((System.Action<Scene, LoadSceneMode>)(OnSceneLoaded));
            this.StartCoroutine(CoCheckUpdates());

            foreach (var file in Directory.GetFiles(Paths.PluginPath, "*.old"))
            {
                File.Delete(file);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (updateInProgress || scene.name != "MainMenu") return;
            if (selectSfx == null)
                selectSfx = AccountManager.Instance.accountTab.resendEmailButton.GetComponent<PassiveButton>().ClickSound;

            if (RequiredUpdateData is null)
            {
                showPopUp = false;
                return;
            }

            var template = GameObject.Find("ExitGameButton");
            if (!template) return;

            var button = Instantiate(template, null);
            var buttonTransform = button.transform;
            //buttonTransform.localPosition = new Vector3(-2f, -2f);
            button.GetComponent<AspectPosition>().anchorPoint = new Vector2(0.458f, 0.124f);

            PassiveButton passiveButton = button.GetComponent<PassiveButton>();
            passiveButton.OnClick = new Button.ButtonClickedEvent();
            passiveButton.OnClick.AddListener((Action)(() =>
            {
                this.StartCoroutine(CoUpdate());
                button.SetActive(false);
            }));

            var text = button.transform.GetComponentInChildren<TMPro.TMP_Text>();
            string t = ModTranslation.GetString("Mod-Updater", 1);
            if (TORUpdate is null && SubmergedUpdate is not null) t = SubmergedCompatibility.Loaded ? ModTranslation.GetString("Mod-Updater", 2) : ModTranslation.GetString("Mod-Updater", 3);

            StartCoroutine(Effects.Lerp(0.1f, (System.Action<float>)(p => text.SetText(t))));

            passiveButton.OnMouseOut.AddListener((Action)(() => text.color = Color.red));
            passiveButton.OnMouseOver.AddListener((Action)(() => text.color = Color.white));

            var isSubmerged = TORUpdate == null;
            var mgr = FindObjectOfType<MainMenuManager>(true);

            if (!isSubmerged)
            {
                try
                {
                    string updateVersion = TORUpdate.Content[^5..];
                    if (Version.Parse(TheOtherRolesPlugin.VersionString).BaseVersion() < Version.Parse(updateVersion).BaseVersion())
                    {
                        passiveButton.OnClick.RemoveAllListeners();
                        passiveButton.OnClick = new Button.ButtonClickedEvent();
                        passiveButton.OnClick.AddListener((Action)(() =>
                        {
                            mgr.StartCoroutine(CoShowAnnouncement(ModTranslation.GetString("Mod-Updater", 4)));
                        }));
                    }
                }
                catch
                {
                    TheOtherRolesPlugin.Logger.LogError("parsing version for auto updater failed :(");
                }

            }

            if (isSubmerged && !SubmergedCompatibility.Loaded) showPopUp = false;
            if (showPopUp)
            {
                var data = isSubmerged ? SubmergedUpdate : TORUpdate;

                var announcement = string.Format(ModTranslation.GetString("Mod-Updater", 5), isSubmerged ? ModTranslation.GetString("Opt-General", 68) : "THE OTHER ROLES MR", data.Version, data.Content);
                mgr.StartCoroutine(CoShowAnnouncement(announcement));
                mgr.StartCoroutine(CoShowAnnouncement(announcement, shortTitle: isSubmerged ? "Submerged Update" : "TOR Update", date: isSubmerged ? SubmergedUpdate.TimeString : TORUpdate.TimeString));
            }
            showPopUp = false;
        }

        [HideFromIl2Cpp]
        public IEnumerator CoUpdate()
        {
            updateInProgress = true;
            var isSubmerged = TORUpdate is null;
            var updateName = (isSubmerged ? ModTranslation.GetString("Opt-General", 68) : "The Other Roles MR");

            var popup = Instantiate(TwitchManager.Instance.TwitchPopup);
            popup.TextAreaTMP.fontSize *= 0.7f;
            popup.TextAreaTMP.enableAutoSizing = false;

            popup.Show();

            var button = popup.transform.GetChild(2).gameObject;
            button.SetActive(false);
            popup.TextAreaTMP.text = string.Format(ModTranslation.GetString("Mod-Updater", 6), updateName);

            var download = Task.Run(DownloadUpdate);
            while (!download.IsCompleted) yield return null;

            button.SetActive(true);
            popup.TextAreaTMP.text = download.Result ? string.Format(ModTranslation.GetString("Mod-Updater", 7), updateName) : ModTranslation.GetString("Mod-Updater", 8);
        }

        private static int announcementNumber = 501;
        [HideFromIl2Cpp]
        public IEnumerator CoShowAnnouncement(string announcement, bool show = true, string shortTitle = "TOR Update", string title = "", string date = "")
        {
            var mgr = FindObjectOfType<MainMenuManager>(true);
            var popUpTemplate = UnityEngine.Object.FindObjectOfType<AnnouncementPopUp>(true);
            if (popUpTemplate == null)
            {
                TheOtherRolesPlugin.Logger.LogError("couldnt show credits, popUp is null");
                yield return null;
            }
            var popUp = UnityEngine.Object.Instantiate(popUpTemplate);

            popUp.gameObject.SetActive(true);
            Assets.InnerNet.Announcement creditsAnnouncement = new()
            {
                Id = "torAnnouncement",
                Language = 0,
                Number = announcementNumber++,
                Title = title == "" ? "The Other Roles Announcement" : title,
                ShortTitle = shortTitle,
                SubTitle = "",
                PinState = false,
                Date = date == "" ? DateTime.Now.Date.ToString() : date,
                Text = announcement,
            };
            mgr.StartCoroutine(Effects.Lerp(0.1f, new Action<float>((p) => {
                if (p == 1)
                {
                    var backup = DataManager.Player.Announcements.allAnnouncements;
                    DataManager.Player.Announcements.allAnnouncements = new();
                    popUp.Init(false);
                    DataManager.Player.Announcements.SetAnnouncements(new Announcement[] { creditsAnnouncement });
                    popUp.CreateAnnouncementList();
                    popUp.UpdateAnnouncementText(creditsAnnouncement.Number);
                    popUp.visibleAnnouncements[0].PassiveButton.OnClick.RemoveAllListeners();
                    DataManager.Player.Announcements.allAnnouncements = backup;
                }
            })));
        }

        [HideFromIl2Cpp]
        public static IEnumerator CoCheckUpdates()
        {
            // Since running the update check task causes a crash for some users, allow the user to disable the updater by creating a file called noupdater.txt
            // in their Among Us folder (root)
            if (System.IO.File.Exists(System.IO.Directory.GetCurrentDirectory() + "\\noupdater.txt")) yield break;
            var torUpdateCheck = Task.Run(() => Instance.GetGithubUpdate("miru-y", "TheOtherRoles-MR"));
            while (!torUpdateCheck.IsCompleted) yield return null;
            if (torUpdateCheck.Result != null && torUpdateCheck.Result.IsNewer(Version.Parse(TheOtherRolesPlugin.VersionString)))
            {
                Instance.TORUpdate = torUpdateCheck.Result;
            }

            if (CheckForSubmergedUpdates)
            {
                //var submergedUpdateCheck = Task.Run(() => Instance.GetGithubUpdate("SubmergedAmongUs", "Submerged"));
                //while (!submergedUpdateCheck.IsCompleted) yield return null;
                //if (submergedUpdateCheck.Result != null && (!SubmergedCompatibility.Loaded || submergedUpdateCheck.Result.IsNewer(SubmergedCompatibility.Version)))
                //{
                //    Instance.SubmergedUpdate = submergedUpdateCheck.Result;
                //if (Instance.SubmergedUpdate.Tag.Equals("2022.10.26")) Instance.SubmergedUpdate = null;
                //}
            }

            //Instance.OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        [HideFromIl2Cpp]
        public async Task<UpdateData> GetGithubUpdate(string owner, string repo)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "TheOtherRoles MR Updater");

            try
            {
                var req = await client.GetAsync($"https://api.github.com/repos/{owner}/{repo}/releases/latest", HttpCompletionOption.ResponseContentRead);
                if (!req.IsSuccessStatusCode) return null;

                if (!req.IsSuccessStatusCode) return null;

                var dataString = await req.Content.ReadAsStringAsync();
                JObject data = JObject.Parse(dataString);
                return new UpdateData(data);
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        private bool TryUpdateSubmergedInternally()
        {
            if (SubmergedUpdate == null) return false;
            try
            {
                if (!SubmergedCompatibility.LoadedExternally) return false;
                var thisAsm = Assembly.GetCallingAssembly();
                var resourceName = thisAsm.GetManifestResourceNames().FirstOrDefault(s => s.EndsWith("Submerged.dll"));
                if (resourceName == default) return false;

                using var submergedStream = thisAsm.GetManifestResourceStream(resourceName)!;
                var asmDef = AssemblyDefinition.ReadAssembly(submergedStream, TypeLoader.ReaderParameters);
                var pluginType = asmDef.MainModule.Types.FirstOrDefault(t => t.IsSubtypeOf(typeof(BasePlugin)));
                var info = IL2CPPChainloader.ToPluginInfo(pluginType, "");
                if (SubmergedUpdate.IsNewer(info.Metadata.Version)) return false;
                File.Delete(SubmergedCompatibility.Assembly.Location);

            }
            catch (Exception e)
            {
                TheOtherRolesPlugin.Logger.LogError(e);
                return false;
            }
            return true;
        }


        [HideFromIl2Cpp]
        public async Task<bool> DownloadUpdate()
        {
            var isSubmerged = TORUpdate is null;
            if (isSubmerged && TryUpdateSubmergedInternally()) return true;
            var data = isSubmerged ? SubmergedUpdate : TORUpdate;

            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "TheOtherRoles MR Updater");

            JToken assets = data.Request["assets"];
            string downloadURI = "";
            for (JToken current = assets.First; current != null; current = current.Next)
            {
                string browser_download_url = current["browser_download_url"]?.ToString();
                if (browser_download_url != null && current["content_type"] != null)
                {
                    if (current["content_type"].ToString().Equals("application/x-msdownload") &&
                        browser_download_url.EndsWith(".dll"))
                    {
                        downloadURI = browser_download_url;
                        break;
                    }
                }
            }

            if (downloadURI.Length == 0) return false;

            var res = await client.GetAsync(downloadURI, HttpCompletionOption.ResponseContentRead);
            string filePath = Path.Combine(Paths.PluginPath, isSubmerged ? "Submerged.dll" : "TheOtherRoles.dll");
            if (File.Exists(filePath + ".old")) File.Delete(filePath + ".old");
            if (File.Exists(filePath)) File.Move(filePath, filePath + ".old");

            await using var responseStream = await res.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(filePath);
            await responseStream.CopyToAsync(fileStream);

            return true;
        }
    }
}
