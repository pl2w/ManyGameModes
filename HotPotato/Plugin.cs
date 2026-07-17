using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HotPotato.GameModes;
using Utilla.Attributes;
using UnityEngine;

namespace HotPotato;

[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
[BepInDependency("org.legoandmars.gorillatag.utilla", "1.6.25")]
[ModdedGamemode(GameModeInfo.Guid, GameModeInfo.Name, typeof(HotPotatoManager))]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    public static Texture2D PotatoTexture, BurntPotatoTexture;

    public Plugin()
    {
        Log = Logger;
        Log.LogInfo($"Running on Gorilla Tag version: ({NetworkSystemConfig.AppVersion}).");
        PotatoTexture = LoadTextureFromEmbed("HotPotato.Assets.potato.png");
        BurntPotatoTexture = LoadTextureFromEmbed("HotPotato.Assets.burntpotato.png");
    }

    private static Texture2D LoadTextureFromEmbed(string resourcePath)
    {
        var assembly = Assembly.GetCallingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourcePath);
        
        if (stream == null)
        {
            Debug.LogError($"[MonkeLib] Resource not found: {resourcePath}");
            foreach (var name in assembly.GetManifestResourceNames())
                Debug.Log($"Available resource: {name}");
            return null;
        }

        byte[] buffer = new byte[stream.Length];
        stream.Read(buffer, 0, buffer.Length);

        var texture = new Texture2D(2, 2);
        return texture.LoadImage(buffer) ? texture : null;
    }
}

public static class PluginInfo
{
    public const string Guid = "xyz.pl2w_chin.hotpotato";
    public const string Name = "Hot Potato";
    public const string Version = "0.1.0";
}

public static class GameModeInfo
{
    public const string Guid = "xyz.pl2w_chin.hotpotato";
    public const string Name = "HOT POTATO";
    public const int Id = 4822;
}