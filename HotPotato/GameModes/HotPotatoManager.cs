using Fusion;
using GorillaGameModes;
using Photon.Pun;

namespace HotPotato.GameModes;

public class HotPotatoManager : GorillaGameManager
{
    public override GameModeType GameType() => (GameModeType)GameModeInfo.HotPotatoId;
    public override string GameModeName() => GameModeInfo.HotPotatoGuid;
    public override string GameModeNameRoomLabel() => string.Empty;
    public override void OnSerializeRead(PhotonStream stream, PhotonMessageInfo info) { }
    public override void OnSerializeWrite(PhotonStream stream, PhotonMessageInfo info) { }
    public override void AddFusionDataBehaviour(NetworkObject behaviour) { }
    public override void OnSerializeRead(object newData) { }
    public override object OnSerializeWrite() => null;
}