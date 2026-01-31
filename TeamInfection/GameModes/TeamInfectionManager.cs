using System.Collections.Generic;
using System.Linq;
using Fusion;
using GorillaGameModes;
using Photon.Pun;
using TeamInfection.RoomSystem;
using UnityEngine;

namespace TeamInfection.GameModes;

public class TeamInfectionManager : GorillaGameManager
{
    private GameState _gameState = GameState.WaitingForPlayers;
    private float _stateStartTime;

    private readonly Dictionary<int, Team> _playerTeams = new();
    
    private const float CountdownTime = 5f;
    
    public override GameModeType GameType() => (GameModeType)GameModeInfo.TeamInfectionId;
    public override string GameModeName() => GameModeInfo.TeamInfectionGuid;
    public override string GameModeNameRoomLabel() => string.Empty;

    public override void StartPlaying()
    {
        base.StartPlaying();

        slowJumpLimit = 6.5f;
        slowJumpMultiplier = 1.1f;
        fastJumpLimit = 8.5f;
        fastJumpMultiplier = 1.3f;
        
        _playerTeams.Clear();
        _gameState = GameState.WaitingForPlayers;
        _stateStartTime = 0f;
    }
    
    public override void Tick()
    {
        base.Tick();
        
        if(!NetworkSystem.Instance.IsMasterClient)
            return;

        switch (_gameState)
        {
            case GameState.WaitingForPlayers:
                if (EnoughPlayersToStart())
                    SetState(GameState.StartingRound);
                break;
            case GameState.StartingRound:
                if (Time.time - _stateStartTime >= CountdownTime)
                {
                    AssignTeams();
                    SetState(GameState.PlayingRound);
                }
                break;
            case GameState.PlayingRound:
                CheckWinCondition();
                break;
            case GameState.RoundComplete:
                SetState(GameState.StartingRound);
                break;
        }
    }

    public override void StopPlaying()
    {
        base.StopPlaying();
        
        _gameState = GameState.WaitingForPlayers;
        _stateStartTime = 0f;
        _playerTeams.Clear();   
    }

    private bool EnoughPlayersToStart()
    {
        return currentNetPlayerArray.Length >= 2;
    }

    private void CheckWinCondition()
    {
        if (currentNetPlayerArray.Length < 2)
        {
            SetState(GameState.WaitingForPlayers);
            return;
        }
        
        if (_playerTeams.Values.All(t => t == Team.Red) || 
            _playerTeams.Values.All(t => t == Team.Blue))
            EndRound();
    }

    private void EndRound()
    {
        foreach (NetPlayer participatingPlayer in GorillaGameModes.GameMode.ParticipatingPlayers)
            RoomSystemWrapper.SendSoundEffectToPlayer(2, 0.25f, participatingPlayer, true);
        
        SetState(GameState.RoundComplete);
    }
    
    private void SetState(GameState state)
    {
        _stateStartTime = Time.time;
        _gameState = state;

        switch (state)
        {
            case GameState.WaitingForPlayers:
                _playerTeams.Clear();
                _stateStartTime = 0f;
                break;
        }
    }

    private void AssignTeams()
    {
        _playerTeams.Clear();
        
        int redCount = 0;
        int blueCount = 0;

        foreach (var player in currentNetPlayerArray)
        {
            if (!_playerTeams.ContainsKey(player.ActorNumber))
            {
                Team assignedTeam = (redCount <= blueCount) ? Team.Red : Team.Blue;
                _playerTeams[player.ActorNumber] = assignedTeam;
                if (assignedTeam == Team.Red) redCount++; else blueCount++;
            }
        }
    }

    public override void NewVRRig(NetPlayer player, int vrrigPhotonViewID, bool didTutorial)
    {
        base.NewVRRig(player, vrrigPhotonViewID, didTutorial);
        
        if(!NetworkSystem.Instance.IsMasterClient)
            return;
        
        if (!_playerTeams.ContainsKey(player.ActorNumber))
        {
            int redCount = _playerTeams.Values.Count(t => t == Team.Red);
            int blueCount = _playerTeams.Values.Count(t => t == Team.Blue);

            Team assignedTeam = (redCount <= blueCount) ? Team.Red : Team.Blue;
            _playerTeams[player.ActorNumber] = assignedTeam;
        }
    }
    
    public override void OnPlayerLeftRoom(NetPlayer leavingPlayer)
    {
        base.OnPlayerLeftRoom(leavingPlayer);
                
        if(!NetworkSystem.Instance.IsMasterClient)
            return;
        
        _playerTeams.Remove(leavingPlayer.ActorNumber);

        if (!EnoughPlayersToStart())
        {
            SetState(GameState.WaitingForPlayers);
            return;
        }
        
        if (_gameState == GameState.PlayingRound)
            CheckWinCondition();
    }

    public override int MyMatIndex(NetPlayer forPlayer)
    {
        Team team = _playerTeams.GetValueOrDefault(forPlayer.ActorNumber, Team.Teamless);
        int matIndex = team switch
        {
            Team.Red => 8,
            Team.Blue => 4,
            _ => 0
        };
        
        return matIndex;
    }

    public override bool LocalCanTag(NetPlayer myPlayer, NetPlayer otherPlayer)
    {
        if (!_playerTeams.TryGetValue(myPlayer.ActorNumber, out var taggingTeam) ||
            !_playerTeams.TryGetValue(otherPlayer.ActorNumber, out var taggedTeam))
        {
            return false;
        }

        if (taggingTeam == Team.Teamless)
            return false;
        
        return taggingTeam != taggedTeam;
    }

    public override void ReportTag(NetPlayer taggedPlayer, NetPlayer taggingPlayer)
    {
        if (_gameState != GameState.PlayingRound) 
            return;
        
        if (!LocalCanTag(taggingPlayer, taggedPlayer)) 
            return;

        if (!_playerTeams.TryGetValue(taggingPlayer.ActorNumber, out var taggerTeam)) 
            return;
        
        _playerTeams[taggedPlayer.ActorNumber] = taggerTeam;
        RoomSystemWrapper.SendStatusEffectToPlayer(StatusEffects.TaggedTime, taggedPlayer);
        RoomSystemWrapper.SendSoundEffectOnOther(0, 0.25f, taggedPlayer);
        
        CheckWinCondition();
    }

    public override float[] LocalPlayerSpeed()
    {
        var myTeam = _playerTeams.GetValueOrDefault(NetworkSystem.Instance.LocalPlayer.ActorNumber, Team.Teamless);

        if (myTeam != Team.Teamless)
        {
            playerSpeed[0] = fastJumpLimit;
            playerSpeed[1] = fastJumpMultiplier;
            return playerSpeed;
        }
        
        playerSpeed[0] = slowJumpLimit;
        playerSpeed[1] = slowJumpMultiplier;
        return playerSpeed;
    }

    public override void AddFusionDataBehaviour(NetworkObject behaviour) { }
    public override void OnSerializeRead(object newData) { }
    
    public override void OnSerializeRead(PhotonStream stream, PhotonMessageInfo info)
    {
        if(NetworkSystem.Instance.IsMasterClient)
            return;
        
        _gameState = (GameState)(byte)stream.ReceiveNext();
        int count = (int)stream.ReceiveNext();
        for (int i = 0; i < count; i++)
        {
            int actorNumber = (int)stream.ReceiveNext();
            Team team = (Team)(byte)stream.ReceiveNext();
            _playerTeams[actorNumber] = team;
        }
    }
    
    public override void OnSerializeWrite(PhotonStream stream, PhotonMessageInfo info)
    {
        if(!NetworkSystem.Instance.IsMasterClient)
            return;
        
        stream.SendNext((byte)_gameState);
        stream.SendNext(_playerTeams.Count);
        foreach (var kvp in _playerTeams)
        {
            stream.SendNext(kvp.Key);         
            stream.SendNext((byte)kvp.Value); 
        }
    }
    
    public override object OnSerializeWrite() => null;
}

public enum Team : byte
{
    Teamless,
    Red,
    Blue
}

public enum GameState : byte
{
    WaitingForPlayers,
    StartingRound,
    PlayingRound,
    RoundComplete
}