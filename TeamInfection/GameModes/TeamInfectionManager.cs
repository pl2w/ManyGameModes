using Fusion;
using GorillaGameModes;
using Photon.Pun;

namespace TeamInfection.GameModes;

public class TeamInfectionManager : GorillaGameManager
{
    public override GameModeType GameType() => (GameModeType)GameModeInfo.TeamInfectionId;
    public override string GameModeName() => GameModeInfo.TeamInfectionGuid;
    public override string GameModeNameRoomLabel() => string.Empty;
    public override void AddFusionDataBehaviour(NetworkObject behaviour) { }
    public override void OnSerializeRead(object newData) { }
    public override void OnSerializeRead(PhotonStream stream, PhotonMessageInfo info) { }
    public override void OnSerializeWrite(PhotonStream stream, PhotonMessageInfo info) { }
    public override object OnSerializeWrite() => null;
}

public enum Team : byte
{
    Teamless,
    Red,
    Blue
}
