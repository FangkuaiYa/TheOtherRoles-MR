using HarmonyLib;

namespace TheOtherRoles.Patches
{
    [HarmonyPatch]
    public static class AirShipSetAntiTpPosition
    {

        // Save the position of the player prior to starting the climb / gap platform
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.ClimbLadder))]
        public static void prefix()
        {
            AntiTeleport.position = PlayerControl.LocalPlayer.transform.position;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MovingPlatformBehaviour), nameof(MovingPlatformBehaviour.UsePlatform))]
        public static void prefix2()
        {
            AntiTeleport.position = PlayerControl.LocalPlayer.transform.position;
        }
    }
}