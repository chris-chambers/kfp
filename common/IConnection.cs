namespace Kfp
{
    // IConversation?
    public interface IConnection
    {
        public ulong SendAck(MsgType originalType, ulong msgNumber);
        public ulong SendDebug(string format, params object[] args);
        public ulong SendVesselUpdate(Guid vesselId, MagicDiff<VesselStatus> diff);
    }
}
