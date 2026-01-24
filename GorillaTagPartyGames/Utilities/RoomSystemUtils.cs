using System;
using System.Reflection;

namespace GorillaTagPartyGames.Utilities;

public static class RoomSystemUtils
{
    private static readonly MethodInfo SendStatusEffectMethod;
    private static readonly MethodInfo SendSoundEffectMethod;
    
    static RoomSystemUtils()
    {
        Assembly gorillaAssembly = typeof(NetPlayer).Assembly;
        Type roomSystemType = gorillaAssembly.GetType("RoomSystem", throwOnError: true);
        
        SendStatusEffectMethod = roomSystemType.GetMethod(
            "SendStatusEffectToPlayer",
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (SendStatusEffectMethod == null)
            UnityEngine.Debug.LogError("SendStatusEffectToPlayer not found!");
        
        SendSoundEffectMethod = roomSystemType.GetMethod(
            "SendSoundEffectToPlayer",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            [roomSystemType.GetNestedType("SoundEffect"), typeof(NetPlayer)],
            null);
        
        if (SendSoundEffectMethod == null)
            UnityEngine.Debug.LogError("SendSoundEffectToPlayer not found!");
    }
    
    public static void SendStatusEffectToPlayer(StatusEffects status, NetPlayer target)
    {
        if (SendStatusEffectMethod == null) return;
        SendStatusEffectMethod.Invoke(null, [status, target]);
    }

    public static void SendSoundEffectToPlayer(int soundID, float volume, NetPlayer target, bool stopCurrentAudio = false)
    {
        if (SendSoundEffectMethod == null) return;
        Type soundEffectType = SendSoundEffectMethod.GetParameters()[0].ParameterType;
        object soundEffectInstance = Activator.CreateInstance(
            soundEffectType,
            new object[] { soundID, volume, stopCurrentAudio });
        
        SendSoundEffectMethod.Invoke(null, [soundEffectInstance, target]);
    }
    
    public static void SendSoundEffectToAll(int soundID, float volume, bool stopCurrentAudio = false)
    {
        Type roomSystemType = typeof(NetPlayer).Assembly.GetType("RoomSystem");
        MethodInfo sendAllMethod = roomSystemType.GetMethod(
            "SendSoundEffectAll",
            BindingFlags.Public | BindingFlags.Static,
            null,
            [roomSystemType.GetNestedType("SoundEffect")],
            null);
        
        if (sendAllMethod == null)
        {
            UnityEngine.Debug.LogError("SendSoundEffectAll not found!");
            return;
        }
        
        Type soundEffectType = sendAllMethod.GetParameters()[0].ParameterType;
        object soundEffectInstance = Activator.CreateInstance(soundEffectType, soundID, volume, stopCurrentAudio);
        sendAllMethod.Invoke(null, [soundEffectInstance]);
    }
}


public enum StatusEffects { TaggedTime, JoinedTaggedTime, SetSlowedTime, UnTagged, FrozenTime, }