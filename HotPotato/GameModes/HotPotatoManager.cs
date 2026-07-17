using Fusion;
using GorillaGameModes;
using Photon.Pun;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HotPotato.GameModes;

public class HotPotatoManager : GorillaGameManager
{
    public List<int> currentPotatoHolders = [];

    public GameState gameState = GameState.WaitingForPlayers;

    public float stateStartTime;
    private const float CountdownTime = 5f;
    private float _hotPotatoExplodeTime = 30f;

    public float tagCoolDown = 3f;
    public double lastTag;

    private bool _isPlayingSound;

    public int burntPotatoMaterialIndex = 17;
    public int hotPotatoMaterialIndex = 16;
    public int defaultMaterial = 0;

    private Texture _paintBrawlNoTeamStunned, _paintBrawlNoTeamEliminated;

    public override GameModeType GameType() => (GameModeType)GameModeInfo.Id;
    public override string GameModeName() => GameModeInfo.Guid;
    public override string GameModeNameRoomLabel() => string.Empty;

    public HotPotatoManager()
    {
        slowJumpLimit = 6.5f;
        slowJumpMultiplier = 1.1f;
        fastJumpLimit = 8.5f;
        fastJumpMultiplier = 1.3f;
    }
    
    private void Start()
    {
        _paintBrawlNoTeamEliminated = GorillaTagger.Instance.offlineVRRig.materialsToChangeTo[hotPotatoMaterialIndex].mainTexture;
        _paintBrawlNoTeamStunned = GorillaTagger.Instance.offlineVRRig.materialsToChangeTo[burntPotatoMaterialIndex].mainTexture;
    }

    public override void StartPlaying()
    {
        base.StartPlaying();

        slowJumpLimit = 6.5f;
        slowJumpMultiplier = 1.1f;
        fastJumpLimit = 8.5f;
        fastJumpMultiplier = 1.3f;

        currentPotatoHolders.Clear();
        gameState = GameState.WaitingForPlayers;
        stateStartTime = 0f;

        lastTag = 0.0;

        GorillaTagger.Instance.offlineVRRig.materialsToChangeTo[hotPotatoMaterialIndex].mainTexture = Plugin.PotatoTexture;
        GorillaTagger.Instance.offlineVRRig.materialsToChangeTo[burntPotatoMaterialIndex].mainTexture = Plugin.BurntPotatoTexture;
    }

    public override void StopPlaying()
    {
        base.StopPlaying();

        _hotPotatoExplodeTime = 30f;
        currentPotatoHolders.Clear();
        gameState = GameState.WaitingForPlayers;
        stateStartTime = 0f;
        lastTag = 0.0;

        GorillaTagger.Instance.offlineVRRig.materialsToChangeTo[hotPotatoMaterialIndex].mainTexture = _paintBrawlNoTeamEliminated;
        GorillaTagger.Instance.offlineVRRig.materialsToChangeTo[burntPotatoMaterialIndex].mainTexture = _paintBrawlNoTeamStunned;
    }

    public override void Tick()
    {
        base.Tick();

        if (!NetworkSystem.Instance.IsMasterClient)
            return;

        switch (gameState)
        {
            case GameState.WaitingForPlayers:
                if (EnoughPlayersToStart())
                    SetState(GameState.StartingRound);
                break;
            case GameState.StartingRound:
                if (Time.time - stateStartTime >= CountdownTime)
                {
                    SetState(GameState.PlayingRound);
                }
                break;
            case GameState.PlayingRound:
                CheckGameEnded();
                break;
            case GameState.RoundComplete:
                SetState(GameState.StartingRound);
                break;
        }
    }

    private void CheckGameEnded()
    {
        if (currentNetPlayerArray.Length < 2)
        {
            SetState(GameState.WaitingForPlayers);
            return;
        }

        _hotPotatoExplodeTime -= Time.deltaTime;
        if (_hotPotatoExplodeTime <= 0f)
        {
            EndRound();
            Plugin.Log.LogInfo("Round ended");
        }

        if (_hotPotatoExplodeTime <= 10f && !_isPlayingSound)
        {
            _isPlayingSound = true;
            StartCoroutine(TickingSound());
        }
    }

    private void ResetRound()
    {
        _hotPotatoExplodeTime = 30f;
        stateStartTime = 0f;
        lastTag = 0.0;
        currentPotatoHolders.Clear();

        List<NetPlayer> selected = currentNetPlayerArray.OrderBy(x => UnityEngine.Random.value)
            .Take(GetPotatoCount())
            .ToList();

        selected.ForEach(p => currentPotatoHolders.Add(p.ActorNumber));
        Plugin.Log.LogInfo($"Selected players: {string.Join(", ", selected.Select(p => p.NickName))}");
        selected.ForEach(p => RoomSystem.SendSoundEffectToPlayer(0, 0.25f, p));
    }

    private void EndRound()
    {
        foreach (var participatingPlayer in GorillaGameModes.GameMode.ParticipatingPlayers)
            RoomSystem.SendSoundEffectToPlayer(2, 0.25f, participatingPlayer, true);

        SetState(GameState.RoundComplete);
    }

    private void SetState(GameState state)
    {
        stateStartTime = Time.time;
        gameState = state;

        switch (state)
        {
            case GameState.WaitingForPlayers:
                currentPotatoHolders.Clear();
                stateStartTime = 0f;
                break;
            case GameState.PlayingRound:
                ResetRound();
                break;
        }
    }

    public override int MyMatIndex(NetPlayer forPlayer)
    {
        if (!currentPotatoHolders.Contains(forPlayer.ActorNumber)) 
            return defaultMaterial;
        
        return gameState == GameState.StartingRound ? burntPotatoMaterialIndex : hotPotatoMaterialIndex;
    }

    public override bool LocalCanTag(NetPlayer myPlayer, NetPlayer otherPlayer)
    {
        if (currentPotatoHolders.Contains(myPlayer.ActorNumber) && !currentPotatoHolders.Contains(otherPlayer.ActorNumber))
            return true;

        return false;
    }

    public override void ReportTag(NetPlayer taggedPlayer, NetPlayer taggingPlayer)
    {
        if (gameState != GameState.PlayingRound)
            return;

        if (Time.time < lastTag + tagCoolDown)
            return;

        if (!LocalCanTag(taggingPlayer, taggedPlayer))
            return;

        currentPotatoHolders.Remove(taggingPlayer.ActorNumber);
        currentPotatoHolders.Add(taggedPlayer.ActorNumber);

        lastTag = Time.time;

        RoomSystem.SendStatusEffectToPlayer(RoomSystem.StatusEffects.TaggedTime, taggedPlayer);
        RoomSystem.SendSoundEffectOnOther(0, 0.25f, taggedPlayer);
    }

    public override void OnPlayerLeftRoom(NetPlayer leavingPlayer)
    {
        base.OnPlayerLeftRoom(leavingPlayer);

        if (!NetworkSystem.Instance.IsMasterClient)
            return;

        if (currentPotatoHolders.Contains(leavingPlayer.ActorNumber))
            currentPotatoHolders.Remove(leavingPlayer.ActorNumber);
    }

    public override float[] LocalPlayerSpeed()
    {
        if (currentPotatoHolders.Contains(NetworkSystem.Instance.LocalPlayer.ActorNumber))
        {
            playerSpeed[0] = fastJumpLimit;
            playerSpeed[1] = fastJumpMultiplier;
            return playerSpeed;
        }

        playerSpeed[0] = slowJumpLimit;
        playerSpeed[1] = slowJumpMultiplier;
        return playerSpeed;
    }

    public override void OnSerializeRead(PhotonStream stream, PhotonMessageInfo info)
    {
        if (NetworkSystem.Instance.IsMasterClient)
            return;

        gameState = (GameState)(byte)stream.ReceiveNext();
        currentPotatoHolders = ((int[])stream.ReceiveNext()).ToList();
        _hotPotatoExplodeTime = (float)stream.ReceiveNext();
    }

    public override void OnSerializeWrite(PhotonStream stream, PhotonMessageInfo info)
    {
        if (!NetworkSystem.Instance.IsMasterClient)
            return;

        stream.SendNext((byte)gameState);
        stream.SendNext(currentPotatoHolders.ToArray());
        stream.SendNext(_hotPotatoExplodeTime);
    }

    private bool EnoughPlayersToStart()
    {
        return currentNetPlayerArray.Length >= 2;
    }

    private int GetPotatoCount()
    {
        if (currentNetPlayerArray.Length < 2) return 0;

        return Math.Clamp((currentNetPlayerArray.Length - 2) / 3 + 1, 1, 3);
    }

    public override void AddFusionDataBehaviour(NetworkObject behaviour) { }
    public override void OnSerializeRead(object newData) { }
    public override object OnSerializeWrite() => null;

    private IEnumerator TickingSound()
    {
        for (int i = 1; i <= 5; i++)
        {
            currentNetPlayerArray.ForEach(p => RoomSystem.SendSoundEffectOnOther(6, 0.25f, p));
            yield return new WaitForSeconds(2f);
        }
        _isPlayingSound = false;
    }
}

public enum GameState : byte
{
    WaitingForPlayers,
    StartingRound,
    PlayingRound,
    RoundComplete
}