using System;
using System.Net;

namespace Kfp
{
    public interface IConnection : IMessageSink, IMessageSource { }

    public interface IMessageSink
    {
        ulong SendAck(MessageType originalType, ulong msgNumber);
        ulong SendDebug(string format, params object[] args);
        ulong SendVesselUpdate(Guid vesselId, Diff<VesselStatus> diff);
    }

    public interface IMessageSource
    {
        IPEndPoint Remote { get; }

        event MessageHandler MessageReceived;
    }

    public delegate void MessageHandler(
        IConnection conn, MessageType type, ulong number, byte[] data);
}
