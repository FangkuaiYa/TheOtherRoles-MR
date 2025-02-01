using System;
using HarmonyLib;
using TheOtherRoles.CustomGameModes;
using TheOtherRoles.Players;
using TheOtherRoles.Utilities;
using TMPro;
using UnityEngine;
using static UnityEngine.UI.Button;

namespace TheOtherRoles.Patches
{

    [HarmonyPatch]
    public static class CredentialsPatch
    {
        [HarmonyPatch(typeof(PingTracker), nameof(PingTracker.Update))]
        public static class PingTrackerPatch
        {
            public static GameObject modStamp;
            public static GameObject customPreset;
            static void Prefix(PingTracker __instance)
            {
                if (modStamp == null)
                {
                    modStamp = new GameObject("ModStamp");
                    var rend = modStamp.AddComponent<SpriteRenderer>();
                    rend.sprite = TheOtherRolesPlugin.GetModStamp();
                    rend.color = new Color(1, 1, 1, 0.5f);
                    ModManager.Instance.ModStamp.enabled = false;
                    modStamp.transform.parent = __instance.transform.parent;
                    modStamp.transform.localScale *= SubmergedCompatibility.Loaded ? 0 : 0.6f;
                }
                if (customPreset == null)
                {
                    var buttonBehaviour = UnityEngine.Object.Instantiate(FastDestroyableSingleton<HudManager>.Instance.GameMenu.CensorChatButton);
                    buttonBehaviour.Text.text = "";
                    buttonBehaviour.Background.sprite = TheOtherRolesPlugin.GetCustomPreset();
                    buttonBehaviour.Background.color = new Color(1, 1, 1, 1);
                    customPreset = buttonBehaviour.gameObject;
                    customPreset.name = "CustomPreset";
                    customPreset.transform.parent = __instance.transform.parent;
                    customPreset.transform.localScale = new Vector3(0.2f, 1.2f, 1.2f) * 1.2f;
                    customPreset.SetActive(true);
                    var button = buttonBehaviour.GetComponent<PassiveButton>();
                    button.ClickSound = null;
                    button.OnMouseOver = new UnityEngine.Events.UnityEvent();
                    button.OnMouseOut = new UnityEngine.Events.UnityEvent();
                    button.OnClick = new ButtonClickedEvent();
                    button.OnClick.AddListener((Action)(() =>
                    {
                        ClientOptionsPatch.isOpenPreset = true;
                        FastDestroyableSingleton<HudManager>.Instance.GameMenu.Open();
                    }));
                }
                float offset = (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started) ? 0.75f : 0f;
                if (AmongUsClient.Instance.IsGameStarted)
                {
                    modStamp.transform.position = FastDestroyableSingleton<HudManager>.Instance.MapButton.transform.position + Vector3.down * offset + Vector3.down * 0.75f;
                }
                else
                {
                    modStamp.transform.position = FastDestroyableSingleton<HudManager>.Instance.MapButton.transform.position + Vector3.down * offset;
                }
                if (customPreset)
                {
                    customPreset.transform.position = modStamp.transform.position + Vector3.down * offset + Vector3.down * 0.75f;
                    if (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started && customPreset.gameObject.activeSelf)
                        customPreset.gameObject.SetActive(false);
                }
            }

            static void Postfix(PingTracker __instance)
            {
                __instance.text.alignment = TextAlignmentOptions.TopRight;
                var position = __instance.GetComponent<AspectPosition>();
                if (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
                {
                    string gameModeText = $"";
                    if (CustomGameModes.HideNSeek.isHideNSeekGM) gameModeText = ModTranslation.GetString("Credentials", 1);
                    else if (HandleGuesser.isGuesserGm) gameModeText = ModTranslation.GetString("Credentials", 2);
                    else if (PropHunt.isPropHuntGM) gameModeText = ModTranslation.GetString("Credentials", 4);
                    if (gameModeText != "") gameModeText = Helpers.cs(Color.yellow, gameModeText) + "\n";
                    __instance.text.text = $"<size=130%><color=#ff351f>TheOtherRoles MR</color></size> v{TheOtherRolesPlugin.Version.ToString() + (TheOtherRolesPlugin.betaDays > 0 ? "-BETA" : "")}\n{gameModeText}" + __instance.text.text;
                    position.DistanceFromEdge = new Vector3(2.25f, 0.11f, 0);
                }
                else
                {
                    string gameModeText = $"";
                    if (TORMapOptions.gameMode == CustomGamemodes.HideNSeek) gameModeText = ModTranslation.GetString("Credentials", 1);
                    else if (TORMapOptions.gameMode == CustomGamemodes.Guesser) gameModeText = ModTranslation.GetString("Credentials", 2);
                    else if (TORMapOptions.gameMode == CustomGamemodes.PropHunt) gameModeText = ModTranslation.GetString("Credentials", 4);
                    if (gameModeText != "") gameModeText = Helpers.cs(Color.yellow, gameModeText) + "\n";
                    var host = $"Host: {GameData.Instance?.GetHost()?.PlayerName}";
                    __instance.text.text = $"<size=130%><color=#ff351f>TheOtherRoles MR</color></size> v{TheOtherRolesPlugin.Version.ToString() + (TheOtherRolesPlugin.betaDays > 0 ? "-BETA" : "")}\n  {gameModeText + ModTranslation.GetString("Credentials", 5)}\n {host}\n {__instance.text.text}";
                    position.DistanceFromEdge = new Vector3(3.5f, 0.1f, 0);
                }
                position.AdjustPosition();
            }
        }

        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
        public static class LogoPatch
        {
            public static SpriteRenderer renderer;
            public static Sprite bannerSprite;
            public static Sprite horseBannerSprite;
            public static Sprite banner2Sprite;
            private static PingTracker instance;

            public static GameObject motdObject;
            public static TextMeshPro motdText;

            static void Postfix(PingTracker __instance)
            {
                var torLogo = new GameObject("bannerLogo_TOR");
                torLogo.transform.SetParent(GameObject.Find("RightPanel").transform, false);
                torLogo.transform.localPosition = new Vector3(-0.4f, 1f, 5f);

                renderer = torLogo.AddComponent<SpriteRenderer>();
                loadSprites();
                renderer.sprite = Helpers.loadSpriteFromResources("TheOtherRoles.Resources.Banner.png", 300f);

                instance = __instance;
                loadSprites();
                // renderer.sprite = TORMapOptions.enableHorseMode ? horseBannerSprite : bannerSprite;
                renderer.sprite = EventUtility.isEnabled ? banner2Sprite : bannerSprite;
                var credentialObject = new GameObject("credentialsTOR");
                var credentials = credentialObject.AddComponent<TextMeshPro>();
                credentials.SetText($"v{TheOtherRolesPlugin.Version.ToString() + (TheOtherRolesPlugin.betaDays > 0 ? "-BETA" : "")}\n<size=30f%>\n</size>{ModTranslation.GetString("Credentials", 7)}\n<size=30%>\n</size>{ModTranslation.GetString("Credentials", 6)}");
                credentials.alignment = TMPro.TextAlignmentOptions.Center;
                credentials.fontSize *= 0.05f;

                credentials.transform.SetParent(torLogo.transform);
                credentials.transform.localPosition = Vector3.down * 1.25f;
                motdObject = new GameObject("torMOTD");
                motdText = motdObject.AddComponent<TextMeshPro>();
                motdText.alignment = TMPro.TextAlignmentOptions.Center;
                motdText.fontSize *= 0.04f;

                motdText.transform.SetParent(torLogo.transform);
                motdText.enableWordWrapping = true;
                var rect = motdText.gameObject.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(5.2f, 0.25f);

                motdText.transform.localPosition = Vector3.down * 2.25f;
                motdText.color = new Color(1, 53f / 255, 31f / 255);
                Material mat = motdText.fontSharedMaterial;
                mat.shaderKeywords = new string[] { "OUTLINE_ON" };
                motdText.SetOutlineColor(Color.white);
                motdText.SetOutlineThickness(0.025f);
            }

            public static void loadSprites()
            {
                if (bannerSprite == null) bannerSprite = Helpers.loadSpriteFromResources("TheOtherRoles.Resources.Banner.png", 300f);
                if (banner2Sprite == null) banner2Sprite = Helpers.loadSpriteFromResources("TheOtherRoles.Resources.Banner2.png", 300f);
                if (horseBannerSprite == null) horseBannerSprite = Helpers.loadSpriteFromResources("TheOtherRoles.Resources.bannerTheHorseRoles.png", 300f);
            }

            public static void updateSprite()
            {
                loadSprites();
                if (renderer != null)
                {
                    float fadeDuration = 1f;
                    instance.StartCoroutine(Effects.Lerp(fadeDuration, new Action<float>((p) => {
                        renderer.color = new Color(1, 1, 1, 1 - p);
                        if (p == 1)
                        {
                            renderer.sprite = TORMapOptions.enableHorseMode ? horseBannerSprite : bannerSprite;
                            instance.StartCoroutine(Effects.Lerp(fadeDuration, new Action<float>((p) => {
                                renderer.color = new Color(1, 1, 1, p);
                            })));
                        }
                    })));
                }
            }
        }
    }
}
