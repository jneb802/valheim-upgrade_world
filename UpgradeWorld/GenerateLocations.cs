using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

namespace UpgradeWorld;

[HarmonyPatch]
public class PreventGenloc
{
  static IEnumerable<MethodBase> TargetMethods()
  {
    yield return AccessTools.Method(typeof(ZoneSystem), nameof(ZoneSystem.Load));
    yield return AccessTools.Method(typeof(ZoneSystem), nameof(ZoneSystem.LoadOld));
  }

  static void Postfix(ZoneSystem __instance)
  {
    if (Settings.DisableAutomaticGenloc && !__instance.m_locationsGenerated && __instance.m_locationInstances.Count > 0)
    {
      __instance.LocationsGenerated = true;
      UpgradeWorld.Log.LogWarning("Skipped automatic genloc. Run the command manually if needed.");
    }
  }
}
