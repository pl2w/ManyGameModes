using System.Collections.Generic;
using System.Linq;
using Fusion;
using GorillaGameModes;
using GorillaTagPartyGames.RoomSystem;
using Photon.Pun;
using UnityEngine;

namespace GorillaTagPartyGames.GameModes.TeamInfection;

public class TeamInfection : GorillaGameManager
{
    private readonly Dictionary<int, Team> _playerTeams = new();
    private readonly Dictionary<int, double> _lastTagTime = new();

    private readonly Dictionary<Team, int> _teamMaterialIndex = new()
    {
        { Team.Teamless, 0 },
        { Team.Red, 2 },
        { Team.Blue, 3 }
    };
    
    private bool _isRestarting;
    private float _restartTimer;

    private const float RestartDelay = 5f;

    public override GameModeType GameType() => (GameModeType)GameModeInfo.TeamTagId;
    public override string GameModeName() => GameModeInfo.TeamTagGuid;
    public override string GameModeNameRoomLabel() => string.Empty;

    public override void StartPlaying()
    {
        base.StartPlaying();
        
        Plugin.Log.LogInfo("StartPlaying");

        slowJumpLimit = 6.5f;
        slowJumpMultiplier = 1.1f;

        fastJumpLimit = 8.5f;
        fastJumpMultiplier = 1.3f;

        if (!NetworkSystem.Instance.IsMasterClient)
        {
            Plugin.Log.LogInfo("Not master client, skipping StartPlaying logic");
            return;
        }

        RestartRound();
    }

    public override void Tick()
    {
        base.Tick();

        if (!_isRestarting) return;

        _restartTimer += Time.deltaTime;

        if (_restartTimer >= RestartDelay)
        {
            Plugin.Log.LogInfo("Restart delay elapsed, restarting round");
            RestartRound();
        }
    }

    public void RestartRound()
    {
        if (!NetworkSystem.Instance.IsMasterClient) return;

        Plugin.Log.LogInfo("Restarting round");

        _lastTagTime.Clear();

        var shuffledPlayers = currentNetPlayerArray
            .OrderBy(_ => Random.value)
            .ToList();

        for (var i = 0; i < shuffledPlayers.Count; i++)
        {
            var team = i switch
            {
                0 => Team.Red,
                1 => Team.Blue,
                _ => Team.Teamless
            };

            Plugin.Log.LogInfo($"Assigning Actor {shuffledPlayers[i].ActorNumber} to {team}");
            SetPlayerTeam(shuffledPlayers[i], team);
        }

        _isRestarting = false;
        CheckGameStatus();
    }

    public void CheckGameStatus()
    {
        if (!NetworkSystem.Instance.IsMasterClient || _isRestarting) return;

        var totalPlayers = currentNetPlayerArray.Length;

        var red = GetTeamCount(Team.Red);
        var blue = GetTeamCount(Team.Blue);
        var teamless = GetTeamCount(Team.Teamless);

        Plugin.Log.LogInfo($"Team counts — Red:{red} Blue:{blue} Teamless:{teamless}");

        if (red != totalPlayers && blue != totalPlayers && teamless != totalPlayers) 
            return;
        
        Plugin.Log.LogInfo("Win condition met, scheduling restart");

        _isRestarting = true;
        _restartTimer = 0f;

        foreach (NetPlayer player in GorillaGameModes.GameMode.ParticipatingPlayers)
        {
            RoomSystemWrapper.SendSoundEffectToPlayer(2, 0.25f, player, true);
        }
    }

    private int GetTeamCount(Team team) =>
        currentNetPlayerArray.Count(player =>
            player != null &&
            _playerTeams.TryGetValue(player.ActorNumber, out var t) &&
            t == team);

    public override void ReportTag(NetPlayer taggedPlayer, NetPlayer taggingPlayer)
    {
        if (!NetworkSystem.Instance.IsMasterClient) return;

        if (!LocalCanTag(taggingPlayer, taggedPlayer))
        {
            Plugin.Log.LogWarning(
                $"Invalid tag attempt: {taggingPlayer?.ActorNumber} -> {taggedPlayer?.ActorNumber}");
            return;
        }

        if (!_playerTeams.TryGetValue(taggingPlayer.ActorNumber, out var taggingTeam))
        {
            Plugin.Log.LogWarning("Tagging player has no team");
            return;
        }

        _lastTagTime[taggingPlayer.ActorNumber] = Time.timeAsDouble;

        Plugin.Log.LogInfo(
            $"Actor {taggingPlayer.ActorNumber} tagged {taggedPlayer.ActorNumber} (Team {taggingTeam})");

        RoomSystemWrapper.SendStatusEffectToPlayer(StatusEffects.TaggedTime, taggedPlayer);
        RoomSystemWrapper.SendSoundEffectOnOther(0, 0.25f, taggedPlayer, true);

        SetPlayerTeam(taggedPlayer, taggingTeam);
        
        CheckGameStatus();
    }

    public override bool LocalCanTag(NetPlayer myPlayer, NetPlayer otherPlayer)
    {
        if (myPlayer == null || otherPlayer == null)
            return false;

        var myTeam = GetPlayerTeam(myPlayer);
        var otherTeam = GetPlayerTeam(otherPlayer);

        return myTeam != Team.Teamless && myTeam != otherTeam;
    }

    private Team GetPlayerTeam(NetPlayer player) =>
        _playerTeams.GetValueOrDefault(player.ActorNumber, Team.Teamless);

    private void SetPlayerTeam(NetPlayer player, Team newTeam)
    {
        if (player == null)
        {
            Plugin.Log.LogWarning("Attempted to set team on null player");
            return;
        }

        if (_playerTeams.TryGetValue(player.ActorNumber, out var currentTeam) &&
            currentTeam == newTeam)
            return;

        _playerTeams[player.ActorNumber] = newTeam;

        Plugin.Log.LogInfo($"Actor {player.ActorNumber} team set to {newTeam}");

        var rig = FindPlayerVRRig(player);
        if (rig != null)
            UpdatePlayerAppearance(rig);
    }

    public override int MyMatIndex(NetPlayer player)
    {
        var team = GetPlayerTeam(player);
        return _teamMaterialIndex[team];
    }

    public override void OnPlayerLeftRoom(NetPlayer player)
    {
        base.OnPlayerLeftRoom(player);

        if (!NetworkSystem.Instance.IsMasterClient) return;

        Plugin.Log.LogInfo($"Player left: Actor {player.ActorNumber}");

        _playerTeams.Remove(player.ActorNumber);
        CheckGameStatus();
    }

    public override void NewVRRig(NetPlayer player, int vrrigPhotonViewID, bool didTutorial)
    {
        base.NewVRRig(player, vrrigPhotonViewID, didTutorial);

        if (!NetworkSystem.Instance.IsMasterClient) return;

        Plugin.Log.LogInfo($"New VRRig for Actor {player.ActorNumber}");

        if (!_playerTeams.ContainsKey(player.ActorNumber))
            SetPlayerTeam(player, Team.Teamless);

        CheckGameStatus();
    }

    public override void OnSerializeRead(PhotonStream stream, PhotonMessageInfo info)
    {
        if (NetworkSystem.Instance.IsMasterClient) return;

        int size = (int)stream.ReceiveNext();
        _playerTeams.Clear();

        for (int i = 0; i < size; i++)
        {
            int actor = (int)stream.ReceiveNext();
            Team team = (Team)(byte)stream.ReceiveNext();
            _playerTeams[actor] = team;
        }

        Plugin.Log.LogInfo($"Client deserialized {_playerTeams.Count} team entries");
    }

    public override void OnSerializeWrite(PhotonStream stream, PhotonMessageInfo info)
    {
        if (!NetworkSystem.Instance.IsMasterClient) return;

        stream.SendNext(_playerTeams.Count);

        foreach (var (actor, team) in _playerTeams)
        {
            stream.SendNext(actor);
            stream.SendNext((byte)team);
        }
    }
    
    public override float[] LocalPlayerSpeed()
    {
        var localPlayer = NetworkSystem.Instance.LocalPlayer;
        if (localPlayer == null)
            return playerSpeed;

        var team = GetPlayerTeam(localPlayer);
        var isInfectedTeam = team is Team.Red or Team.Blue;

        playerSpeed[0] = isInfectedTeam ? fastJumpLimit : slowJumpLimit;
        playerSpeed[1] = isInfectedTeam ? fastJumpMultiplier : slowJumpMultiplier;

        return playerSpeed;
    }
    
    public override void AddFusionDataBehaviour(NetworkObject behaviour) { }
    public override void OnSerializeRead(object newData) { }
    public override object OnSerializeWrite() => null;
}

public enum Team : byte
{
    Teamless,
    Red,
    Blue
}
