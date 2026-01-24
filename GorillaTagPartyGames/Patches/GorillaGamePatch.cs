using GorillaTagPartyGames.GameModes;
using GorillaTagPartyGames.GameModes.TeamTag;
using HarmonyLib;

namespace GorillaTagPartyGames.Patches;

[HarmonyPatch(typeof(GorillaGameManager), "GameTypeName")]
public static class GorillaGamePatch
{
    [HarmonyPostfix]
    private static void GameTypeName(GorillaGameManager __instance, ref string __result)
    {
        if (__instance is TeamInfection)
        {
            __result = GameModeInfo.TeamTagGuid;
        }
    }
}