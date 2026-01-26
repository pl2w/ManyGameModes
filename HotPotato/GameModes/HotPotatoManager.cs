using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using GorillaGameModes;
using GorillaNetworking;
using HotPotato.RoomSystem;
using Photon.Pun;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HotPotato.GameModes;

public class HotPotatoManager : GorillaGameManager
{
    private readonly List<int> _eliminatedPlayers = [];
    private readonly Dictionary<int, double> _slowDownCooldowns = new();
    private int _currentPotatoHolder = -1;
    
    private float _maxPotatoTime = 30f;
    private float _minPotatoTime = 10f;
    private float _potatoTimeDecrease = 0.5f;
    private float _potatoTimer = 30f;

    private const float SlowDownCoolDown = 5f;
    
    public override GameModeType GameType() => (GameModeType)GameModeInfo.HotPotatoId;
    public override string GameModeName() => GameModeInfo.HotPotatoGuid;
    public override string GameModeNameRoomLabel() => string.Empty;

    public override void StartPlaying()
    {
        base.StartPlaying();
        
        slowJumpLimit = 6.5f;
        slowJumpMultiplier = 1.1f;
        
        fastJumpLimit = 8.5f;
        fastJumpMultiplier = 1.3f;
        
        if (!NetworkSystem.Instance.IsMasterClient)
        {
            Plugin.Log.LogInfo("Not master client, skipping HotPotato StartPlaying logic");
            return;
        }

        ResetGame();
    }

    public override void StopPlaying()
    {
        base.StopPlaying();
        
        ResetData();
    }

    private void ResetData()
    {
        _eliminatedPlayers.Clear();
        _slowDownCooldowns.Clear();
        
        _maxPotatoTime = 30f;
        _minPotatoTime = 10f;
        _potatoTimeDecrease = 0.5f;
    }

    public override void ResetGame()
    {
        base.ResetGame();
        
        if (!NetworkSystem.Instance.IsMasterClient)
        {
            Plugin.Log.LogInfo("Not master client, skipping HotPotato ResetGame logic");
            return;
        }

        ResetData();
        RestartRound();
    }

    void RestartRound()
    {
        if (!NetworkSystem.Instance.IsMasterClient)
        {
            Plugin.Log.LogInfo("Not master client, skipping HotPotato RestartRound logic");
            return;
        }
        
        var alivePlayers = currentNetPlayerArray
            .Where(p => p != null && !_eliminatedPlayers.Contains(p.ActorNumber))
            .ToList();

        if (alivePlayers.Count <= 1)
        {
            ResetGame();
            return;
        }
        
        _currentPotatoHolder = alivePlayers[Random.Range(0, alivePlayers.Count)].ActorNumber;

        _potatoTimer = _maxPotatoTime;
        _maxPotatoTime = Mathf.Max(_minPotatoTime, _maxPotatoTime - _potatoTimeDecrease);

        Plugin.Log.LogInfo($"New round: Potato held by Actor {_currentPotatoHolder}, " +
                           $"Timer: {_potatoTimer}, Next max: {_maxPotatoTime}");
    }

    public override void Tick()
    {
        base.Tick();
        
        if (!NetworkSystem.Instance.IsMasterClient)
            return;
        
        _potatoTimer -= Time.deltaTime;

        if (_potatoTimer <= 0f)
            ExplodePotato();
    }

    void ExplodePotato()
    {
        Plugin.Log.LogInfo("Potato exploded, starting the next round");

        _eliminatedPlayers.Add(_currentPotatoHolder);
        
        RestartRound();
    }
    
    public override void ReportTag(NetPlayer taggedPlayer, NetPlayer taggingPlayer)
    {
        if (!NetworkSystem.Instance.IsMasterClient) 
            return;

        if (!LocalCanTag(taggingPlayer, taggedPlayer))
            return;

        if (taggingPlayer.ActorNumber == _currentPotatoHolder)
        {
            PassPotato(taggedPlayer.ActorNumber);
            Plugin.Log.LogInfo($"Potato passed to Actor {taggedPlayer.ActorNumber}");
        }

        if (_eliminatedPlayers.Contains(taggedPlayer.ActorNumber) && taggedPlayer.ActorNumber != _currentPotatoHolder)
        {
            if (_slowDownCooldowns.TryGetValue(taggedPlayer.ActorNumber, out var slowDownTime))
            {
                if (Time.timeAsDouble < slowDownTime + SlowDownCoolDown)
                    return;
            }

            _slowDownCooldowns[taggedPlayer.ActorNumber] = Time.timeAsDouble;
            RoomSystemWrapper.SendStatusEffectToPlayer(StatusEffects.SetSlowedTime, taggedPlayer);
            Plugin.Log.LogInfo($"Eliminated Actor {taggingPlayer.ActorNumber} tagged alive player {taggedPlayer.ActorNumber}");
        }
    }

    private void PassPotato(int taggedPlayerActorNumber)
    {
        _currentPotatoHolder = taggedPlayerActorNumber;
        _potatoTimer += 3f;
    }

    public override bool LocalCanTag(NetPlayer myPlayer, NetPlayer otherPlayer)
    {
        if (myPlayer == null || otherPlayer == null)
            return false;

        if (myPlayer.ActorNumber == _currentPotatoHolder && !_eliminatedPlayers.Contains(otherPlayer.ActorNumber))
            return true;

        if (_eliminatedPlayers.Contains(myPlayer.ActorNumber) &&
            !_eliminatedPlayers.Contains(otherPlayer.ActorNumber) &&
            otherPlayer.ActorNumber != _currentPotatoHolder)
            return true;

        return false;
    }
    
    public override void OnPlayerEnteredRoom(NetPlayer newPlayer)
    {
        base.OnPlayerEnteredRoom(newPlayer);

        if (!NetworkSystem.Instance.IsMasterClient)
        {
            Plugin.Log.LogInfo("Not master client, skipping HotPotato OnPlayerEnteredRoom logic");
            return;
        }
        
        _eliminatedPlayers.Add(newPlayer.ActorNumber);
    }

    public override void OnPlayerLeftRoom(NetPlayer otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        
        if (!NetworkSystem.Instance.IsMasterClient)
        {
            Plugin.Log.LogInfo("Not master client, skipping HotPotato OnPlayerLeftRoom logic");
            return;
        }
        
        _eliminatedPlayers.Remove(otherPlayer.ActorNumber);
        _slowDownCooldowns.Remove(otherPlayer.ActorNumber);
    }

    public override int MyMatIndex(NetPlayer forPlayer)
    {
        if (_eliminatedPlayers.Contains(forPlayer.ActorNumber))
            return 2;
        
        if (_currentPotatoHolder == forPlayer.ActorNumber)
            return 1;

        return 0;
    }

    public override void OnSerializeRead(PhotonStream stream, PhotonMessageInfo info)
    {        
        if (NetworkSystem.Instance.IsMasterClient) 
            return;
        
        _currentPotatoHolder = (int)stream.ReceiveNext();
        var count = (int)stream.ReceiveNext();
        
        _eliminatedPlayers.Clear();
        for (var i = 0; i < count; i++)
            _eliminatedPlayers.Add((int)stream.ReceiveNext());
    }

    public override void OnSerializeWrite(PhotonStream stream, PhotonMessageInfo info)
    {
        if (!NetworkSystem.Instance.IsMasterClient) 
            return;

        stream.SendNext(_currentPotatoHolder);
        stream.SendNext(_eliminatedPlayers.Count);
        foreach (var actor in _eliminatedPlayers)
            stream.SendNext(actor);
    }
    
    public override void AddFusionDataBehaviour(NetworkObject behaviour) { }
    public override void OnSerializeRead(object newData) { }
    public override object OnSerializeWrite() => null;
}