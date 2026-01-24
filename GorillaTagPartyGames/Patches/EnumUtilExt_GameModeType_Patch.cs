using HarmonyLib;
using System.Reflection;
using GorillaGameModes;

namespace GorillaTagPartyGames.Patches
{
    [HarmonyPatch]
    public static class EnumUtilExt_GameModeType_Patch
    {
        static MethodBase TargetMethod()
        {
            return typeof(EnumUtilExt)
                .GetMethod(nameof(EnumUtilExt.GetName), BindingFlags.Public | BindingFlags.Static)
                ?.MakeGenericMethod(typeof(GameModeType));
        }

        static bool Prefix(GameModeType e, ref string __result)
        {
            if ((int)e == GameModeInfo.TeamTagId)
            {
                __result = GameModeInfo.TeamTagName;
                return false;
            }

            return true;
        }
    }
}