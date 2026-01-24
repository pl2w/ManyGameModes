using System.Reflection;
using BepInEx;
using GorillaTagPartyGames.GameModes.TeamTag;
using HarmonyLib;
using Utilla.Attributes;

namespace GorillaTagPartyGames;

[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
[BepInDependency("org.legoandmars.gorillatag.utilla", "1.5.0")]
[ModdedGamemode(GameModeInfo.TeamTagGuid, GameModeInfo.TeamTagName, typeof(TeamInfection))]
public class Plugin : BaseUnityPlugin
{
    public Plugin()
    {
        var harmony = new Harmony(PluginInfo.Guid);
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

public static class PluginInfo
{
    public const string Guid = "xyz.pl2w.gtag.partygames";
    public const string Name = "Gorilla Tag Party Games";
    public const string Version = "0.1.0";
}

public static class GameModeInfo
{
    public const string TeamTagGuid = "xyz.pl2w.gtag.partygames.teaminfection";
    public const string TeamTagName = "TEAM INFECTION";
    public const int TeamTagId = 4821;
}