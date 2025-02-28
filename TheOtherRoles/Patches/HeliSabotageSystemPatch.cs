using HarmonyLib;
using TheOtherRoles.Utilities;

namespace TheOtherRoles.Patches
{
    [HarmonyPatch(typeof(HeliSabotageSystem), nameof(HeliSabotageSystem.Deteriorate))]
    public static class HeliSabotageSystemPatch
    {
        static void Prefix(HeliSabotageSystem __instance, float deltaTime)
        {
            if (!__instance.IsActive)
                return;
            if (MapUtilities.CachedShipStatus == null)
                return;

            if (__instance.Countdown > CustomOptionHolder.airshipHeliSabotageSystemTimeLimit.getFloat())
                __instance.Countdown = CustomOptionHolder.airshipHeliSabotageSystemTimeLimit.getFloat();
        }
    }
}

