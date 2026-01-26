using System;
using System.Reflection;
using UnityEngine;

namespace GorillaTagPartyGames.RoomSystem;

public static class RoomSystemWrapper
{
    private static readonly MethodInfo SendStatusEffectMethod;
    private static readonly MethodInfo SendSoundEffectToPlayerMethod;
    private static readonly MethodInfo SendSoundEffectOnOtherMethod;
    
    private static readonly Type StatusEnumType; 
    
    static RoomSystemWrapper()
    {
        var assembly = GorillaTagger.Instance.GetType().Assembly;
        var roomSystemType = assembly.GetType("RoomSystem", throwOnError: false);
        StatusEnumType = roomSystemType.GetNestedType("StatusEffects", BindingFlags.Public | BindingFlags.NonPublic);
        
        SendStatusEffectMethod = roomSystemType.GetMethod(
            "SendStatusEffectToPlayer",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public
        );
        
        SendSoundEffectToPlayerMethod = roomSystemType.GetMethod(
            "SendSoundEffectToPlayer",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
            null,
            [
                typeof(int),
                typeof(float),
                typeof(NetPlayer),
                typeof(bool)
            ],
            null
        );
        
        SendSoundEffectOnOtherMethod = roomSystemType.GetMethod(
            "SendSoundEffectOnOther",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
            null,
            [
                typeof(int),
                typeof(float),
                typeof(NetPlayer),
                typeof(bool)
            ],
            null
        );
    }
    
    public static void SendStatusEffectToPlayer(StatusEffects statusEffect, NetPlayer targetPlayer)
    {    
        var statusValue = Enum.Parse(StatusEnumType, statusEffect.GetName());
        SendStatusEffectMethod.Invoke(
            null,
            [statusValue, targetPlayer]
        );
    }

    public static void SendSoundEffectToPlayer(
        int soundIndex,
        float soundVolume,
        NetPlayer targetPlayer,
        bool stopCurrentAudio = false)
    {
        SendSoundEffectToPlayerMethod.Invoke(
            null,
            [soundIndex, soundVolume, targetPlayer, stopCurrentAudio]
        );
    }
    
    public static void SendSoundEffectOnOther(
        int soundIndex,
        float soundVolume,
        NetPlayer targetPlayer,
        bool stopCurrentAudio = false)
    {
        SendSoundEffectToPlayerMethod.Invoke(
            null,
            [soundIndex, soundVolume, targetPlayer, stopCurrentAudio]
        );
    }
}

public enum StatusEffects
{
    TaggedTime,
    JoinedTaggedTime,
    SetSlowedTime,
    UnTagged,
    FrozenTime,
}