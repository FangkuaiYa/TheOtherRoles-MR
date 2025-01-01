using System.Reflection;
using UnityEngine;
using Reactor.Utilities.Extensions;
using System.IO;
using System.Collections.Generic;

namespace TheOtherRoles.Modules
{
    public static class AssetLoader
    {
        private static readonly Assembly dll = Assembly.GetExecutingAssembly();
        private static bool flag = false;
        public static CustomAssets customAssets = new CustomAssets();
        public static List<Sprite> AllHatSprites { get; }
        public static void LoadAssets()
        {
            if (flag) return;
            flag = true;
            LoadSoundEffectAssets();
        }
        private static void LoadSoundEffectAssets()
        {
            var resourceSoundEffectAssetBundleStream = dll.GetManifestResourceStream("TheOtherRoles.Resources.AssetBundles.soundeffects");
            var assetBundleBundle = AssetBundle.LoadFromMemory(resourceSoundEffectAssetBundleStream.ReadFully());
            customAssets.arsonistDouse = assetBundleBundle.LoadAsset<AudioClip>("arsonistDouse.mp3").DontUnload();
            customAssets.bombDefused = assetBundleBundle.LoadAsset<AudioClip>("bombDefused.mp3").DontUnload();
            customAssets.bombExplosion = assetBundleBundle.LoadAsset<AudioClip>("bombExplosion.mp3").DontUnload();
            customAssets.bombFuseBurning = assetBundleBundle.LoadAsset<AudioClip>("bombFuseBurning.mp3").DontUnload();
            customAssets.bombTick = assetBundleBundle.LoadAsset<AudioClip>("bombTick.mp3").DontUnload();
            customAssets.cleanerClean = assetBundleBundle.LoadAsset<AudioClip>("cleanerClean.mp3").DontUnload();
            customAssets.deputyHandcuff = assetBundleBundle.LoadAsset<AudioClip>("deputyHandcuff.mp3").DontUnload();
            customAssets.engineerRepair = assetBundleBundle.LoadAsset<AudioClip>("engineerRepair.mp3").DontUnload();
            customAssets.eraserErase = assetBundleBundle.LoadAsset<AudioClip>("eraserErase.mp3").DontUnload();
            customAssets.fail = assetBundleBundle.LoadAsset<AudioClip>("fail.mp3").DontUnload();
            customAssets.garlic = assetBundleBundle.LoadAsset<AudioClip>("garlic.mp3").DontUnload();
            customAssets.hackerHack = assetBundleBundle.LoadAsset<AudioClip>("hackerHack.mp3").DontUnload();
            customAssets.jackalSidekick = assetBundleBundle.LoadAsset<AudioClip>("jackalSidekick.mp3").DontUnload();
            customAssets.knockKnock = assetBundleBundle.LoadAsset<AudioClip>("knockKnock.mp3").DontUnload();
            customAssets.lighterLight = assetBundleBundle.LoadAsset<AudioClip>("lighterLight.mp3").DontUnload();
            customAssets.medicShield = assetBundleBundle.LoadAsset<AudioClip>("medicShield.mp3").DontUnload();
            customAssets.mediumAsk = assetBundleBundle.LoadAsset<AudioClip>("mediumAsk.mp3").DontUnload();
            customAssets.morphlingMorph = assetBundleBundle.LoadAsset<AudioClip>("morphlingMorph.mp3").DontUnload();
            customAssets.morphlingSample = assetBundleBundle.LoadAsset<AudioClip>("morphlingSample.mp3").DontUnload();
            customAssets.portalUse = assetBundleBundle.LoadAsset<AudioClip>("portalUse.mp3").DontUnload();
            customAssets.pursuerBlank = assetBundleBundle.LoadAsset<AudioClip>("pursuerBlank.mp3").DontUnload();
            customAssets.securityGuardPlaceCam = assetBundleBundle.LoadAsset<AudioClip>("securityGuardPlaceCam.mp3").DontUnload();
            customAssets.shifterShift = assetBundleBundle.LoadAsset<AudioClip>("shifterShift.mp3").DontUnload();
            customAssets.timemasterShield = assetBundleBundle.LoadAsset<AudioClip>("timemasterShield.mp3").DontUnload();
            customAssets.trackerTrackCorpses = assetBundleBundle.LoadAsset<AudioClip>("trackerTrackCorpses.mp3").DontUnload();
            customAssets.trackerTrackPlayer = assetBundleBundle.LoadAsset<AudioClip>("trackerTrackPlayer.mp3").DontUnload();
            customAssets.trapperTrap = assetBundleBundle.LoadAsset<AudioClip>("trapperTrap.mp3").DontUnload();
            customAssets.tricksterPlaceBox = assetBundleBundle.LoadAsset<AudioClip>("tricksterPlaceBox.mp3").DontUnload();
            customAssets.tricksterUseBoxVent = assetBundleBundle.LoadAsset<AudioClip>("tricksterUseBoxVent.mp3").DontUnload();
            customAssets.vampireBite = assetBundleBundle.LoadAsset<AudioClip>("vampireBite.mp3").DontUnload();
            customAssets.vultureEat = assetBundleBundle.LoadAsset<AudioClip>("vultureEat.mp3").DontUnload();
            customAssets.warlockCurse = assetBundleBundle.LoadAsset<AudioClip>("warlockCurse.mp3").DontUnload();
            customAssets.witchSpell = assetBundleBundle.LoadAsset<AudioClip>("witchSpell.mp3").DontUnload();
        }
    }
    public class CustomAssets
    {
        // Sound Effects (AudioClip)
        public AudioClip arsonistDouse;
        public AudioClip bombDefused;
        public AudioClip bombExplosion;
        public AudioClip bombFuseBurning;
        public AudioClip bombTick;
        public AudioClip cleanerClean;
        public AudioClip deputyHandcuff;
        public AudioClip engineerRepair;
        public AudioClip eraserErase;
        public AudioClip fail;
        public AudioClip garlic;
        public AudioClip hackerHack;
        public AudioClip jackalSidekick;
        public AudioClip knockKnock;
        public AudioClip lighterLight;
        public AudioClip medicShield;
        public AudioClip mediumAsk;
        public AudioClip morphlingMorph;
        public AudioClip morphlingSample;
        public AudioClip portalUse;
        public AudioClip pursuerBlank;
        public AudioClip securityGuardPlaceCam;
        public AudioClip shifterShift;
        public AudioClip timemasterShield;
        public AudioClip trackerTrackCorpses;
        public AudioClip trackerTrackPlayer;
        public AudioClip trapperTrap;
        public AudioClip tricksterPlaceBox;
        public AudioClip tricksterUseBoxVent;
        public AudioClip vampireBite;
        public AudioClip vultureEat;
        public AudioClip warlockCurse;
        public AudioClip witchSpell;
    }
}
