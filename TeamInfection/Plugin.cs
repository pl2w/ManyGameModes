using BepInEx;
using BepInEx.Logging;
using TeamInfection.GameModes;
using Utilla.Attributes;

namespace TeamInfection;

[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
[BepInDependency("org.legoandmars.gorillatag.utilla", "1.6.25")]
[ModdedGamemode(GameModeInfo.Guid, GameModeInfo.Name, typeof(TeamInfectionManager))]
public class Plugin : BaseUnityPlugin
{
    private static ManualLogSource Log;
    
    public Plugin()
    {
        Log = Logger;
        Log.LogInfo($"Running on Gorilla Tag version: ({NetworkSystemConfig.AppVersion}).");
    }
}

public static class PluginInfo
{
    public const string Guid = "xyz.pl2w_chin.teaminfection";
    public const string Name = "Team Infection";
    public const string Version = "1.0.1";
}

public static class GameModeInfo
{
    public const string Guid = "xyz.pl2w_chin.teaminfection";
    public const string Name = "TEAM INFECTION";
    public const int Id = 4821;
}