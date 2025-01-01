using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TheOtherRoles.Utilities;

namespace TheOtherRoles.Patches
{
    public class PatchInstaller
    {
        public void Install()
        {
            var harmony = new Harmony("com.example.patch");
            var method = typeof(NormalPlayerTask).GetMethod(nameof(NormalPlayerTask.PickRandomConsoles), BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(TaskTypes), typeof(byte[]) }, null);
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(NormalPlayerTaskPatch).GetMethod(nameof(NormalPlayerTaskPatch.Postfix))));
        }
    }
    public class NormalPlayerTaskPatch
    {
        public static void Postfix(NormalPlayerTask __instance, TaskTypes taskType, byte[] consoleIds)
        {
            if (!CustomOptionHolder.enableRandomizationInFixWiringTask.getBool() || taskType != TaskTypes.FixWiring)
            {
                return;
            }
            List<Console> list = (from t in MapUtilities.CachedShipStatus.AllConsoles
                                  where t.TaskTypes.Contains(taskType)
                                  select t).ToList<Console>();
            List<Console> list2 = new List<Console>(list);
            for (int i = 0; i < __instance.Data.Length; i++)
            {
                int index = UnityEngine.Random.Range(0, list2.Count<Console>());
                __instance.Data[i] = (byte)list2[index].ConsoleId;
                list2.RemoveAt(index);
            }
        }
    }

}