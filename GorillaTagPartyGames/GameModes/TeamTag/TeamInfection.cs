using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using GorillaGameModes;
using Photon.Pun;
using UnityEngine;

namespace GorillaTagPartyGames.GameModes.TeamTag;

public class TeamInfection : GorillaGameManager
{
    private Dictionary<int, Team> _playerTeams = new();

    private readonly Dictionary<Team, int> _teamMaterialIndex = new()
    {
        { Team.Teamless, 0 },
        { Team.Red, 2 },
        { Team.Blue, 3 }
    };
    
    private TeamInfectionData _teamInfectionData =  new();

    public override GameModeType GameType() => (GameModeType)GameModeInfo.TeamTagId;
    public override string GameModeName() => GameModeInfo.TeamTagGuid;
    public override string GameModeNameRoomLabel() => string.Empty;

    public override void StartPlaying()
    {
        base.StartPlaying();
        if (!NetworkSystem.Instance.IsMasterClient)
            return;

        ResetGame();
    }

    public override void ResetGame()
    {
        Debug.Log("[TeamInfection] ResetGame called.");

        List<NetPlayer> allPlayers = NetworkSystem.Instance.AllNetPlayers.ToList();
        allPlayers = allPlayers.OrderBy(_ => Random.value).ToList();

        for (var i = 0; i < allPlayers.Count; i++)
        {
            var team = i switch
            {
                0 => Team.Red,
                1 => Team.Blue,
                _ => Team.Teamless
            };

            Debug.Log($"[TeamInfection] Assigning {allPlayers[i]?.ActorNumber ?? -1} to team {team}");
            ChangePlayerTeam(allPlayers[i], team);
        }
    }

    public override void NewVRRig(NetPlayer player, int vrrigPhotonViewID, bool didTutorial)
    {
        base.NewVRRig(player, vrrigPhotonViewID, didTutorial);
        Debug.Log($"[TeamInfection] NewVRRig called for actor {player.ActorNumber}");

        if (!NetworkSystem.Instance.IsMasterClient)
        {
            Debug.Log("[TeamInfection] Not master client. Skipping team assignment.");
            return;
        }

        if (!_playerTeams.ContainsKey(player.ActorNumber))
        {
            Debug.Log($"[TeamInfection] Actor {player.ActorNumber} had no team, assigning Teamless.");
            ChangePlayerTeam(player, Team.Teamless);
        }
    }

    public override void ReportTag(NetPlayer taggedPlayer, NetPlayer taggingPlayer)
    {
        Debug.Log($"[TeamInfection] ReportTag called: {taggingPlayer.ActorNumber} -> {taggedPlayer.ActorNumber}");

        if (!NetworkSystem.Instance.IsMasterClient)
        {
            Debug.Log("[TeamInfection] Not master client. Ignoring tag.");
            return;
        }

        if (!LocalCanTag(taggingPlayer, taggedPlayer))
        {
            Debug.Log($"[TeamInfection] LocalCanTag returned false: {taggingPlayer.ActorNumber} cannot tag {taggedPlayer.ActorNumber}");
            return;
        }

        if (!_playerTeams.TryGetValue(taggingPlayer.ActorNumber, out var taggingPlayerTeam))
        {
            Debug.LogWarning($"[TeamInfection] Tagging player {taggingPlayer.ActorNumber} has no team; tag ignored");
            return;
        }

        Debug.Log($"[TeamInfection] Tag successful! Assigning actor {taggedPlayer.ActorNumber} to team {taggingPlayerTeam}");
        ChangePlayerTeam(taggedPlayer, taggingPlayerTeam);

        CheckGameStatus();
    }

    private IEnumerator GameRestartCountdown()
    {
        Debug.Log("[TeamInfection] GameRestartCountdown started.");

        var allPlayers = NetworkSystem.Instance.AllNetPlayers;
        foreach (var player in allPlayers)
        {
            if (player == null) continue;
            Debug.Log($"[TeamInfection] Resetting actor {player.ActorNumber} to Teamless.");
            ChangePlayerTeam(player, Team.Teamless);
        }

        Debug.Log("[TeamInfection] Restarting game in 3 seconds...");

        yield return new WaitForSecondsRealtime(3);

        ResetGame();
    }

    public void CheckGameStatus()
    {
        if (!NetworkSystem.Instance.IsMasterClient)
            return;

        var redTeamPlayers = GetTeamCount(Team.Red);
        var blueTeamPlayers = GetTeamCount(Team.Blue);
        var teamlessTeamPlayers = GetTeamCount(Team.Teamless);

        Debug.Log($"[TeamInfection] Team counts - Red: {redTeamPlayers}, Blue: {blueTeamPlayers}, Teamless: {teamlessTeamPlayers}");

        if (redTeamPlayers == NetworkSystem.Instance.AllNetPlayers.Length)
        {
            Debug.Log("[TeamInfection] Red team has won. Restarting round.");
            StartCoroutine(GameRestartCountdown());
        }

        if (blueTeamPlayers == NetworkSystem.Instance.AllNetPlayers.Length)
        {
            Debug.Log("[TeamInfection] Blue team has won. Restarting round.");
            StartCoroutine(GameRestartCountdown());
        }

        if (teamlessTeamPlayers == NetworkSystem.Instance.AllNetPlayers.Length)
        {
            Debug.Log("[TeamInfection] Everyone is teamless? Restarting round to fix.");
            StartCoroutine(GameRestartCountdown());
        }
    }

    public void ChangePlayerTeam(NetPlayer netPlayer, Team newTeam)
    {
        if (netPlayer == null)
            return;

        _playerTeams[netPlayer.ActorNumber] = newTeam;
    }

    public override bool LocalCanTag(NetPlayer myPlayer, NetPlayer otherPlayer)
    {
        if (myPlayer == null || otherPlayer == null) return false;

        var myTeam = GetPlayerTeam(myPlayer);
        var otherTeam = GetPlayerTeam(otherPlayer);

        var canTag = myTeam != Team.Teamless && myTeam != otherTeam;
        return canTag;
    }

    public override int MyMatIndex(NetPlayer player)
    {
        var team = _playerTeams.GetValueOrDefault(player.ActorNumber, Team.Teamless);
        var matIndex = _teamMaterialIndex[team];
        return matIndex;
    }

    public override void OnPlayerLeftRoom(NetPlayer otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);
        if (!NetworkSystem.Instance.IsMasterClient)
            return;

        _playerTeams.Remove(otherPlayer.ActorNumber);
    }

    public override void OnSerializeRead(object newData)
    {
        Debug.Log("Received new TeamInfectionData");
        _teamInfectionData = (TeamInfectionData)newData;
        
        Debug.Log(_teamInfectionData.testData);
    }

    public override object OnSerializeWrite() => _teamInfectionData;

    public override void OnSerializeRead(PhotonStream stream, PhotonMessageInfo info)
    {
        if (NetworkSystem.Instance.IsMasterClient) 
            return;
        
        _teamInfectionData = stream.ReceiveNext() as TeamInfectionData;
    }
    
    public override void OnSerializeWrite(PhotonStream stream, PhotonMessageInfo info)
    {    
        if (!NetworkSystem.Instance.IsMasterClient) 
            return;
        
        stream.SendNext(_teamInfectionData);
    }
    
    public override void AddFusionDataBehaviour(NetworkObject behaviour) { }

    private Team GetPlayerTeam(NetPlayer player) =>
        _playerTeams.GetValueOrDefault(player.ActorNumber, Team.Teamless);

    private int GetTeamCount(Team team) =>
        NetworkSystem.Instance.AllNetPlayers.Count(player =>
            player != null &&
            _playerTeams.TryGetValue(player.ActorNumber, out var t) &&
            t == team
        );
}

public enum Team
{
    Teamless,
    Red,
    Blue,
}
