using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Kfp
{
    public sealed class Client : IConnection, IDisposable
    {
        private const int HeaderSize = 9;

        private readonly UdpClient _udp;
        private readonly IPEndPoint _remote;
        private ulong _msgNumber;

        public Client(IPEndPoint serverEndPoint) {
            _remote = serverEndPoint;

            _udp = new UdpClient();
            _udp.Connect(serverEndPoint);
            _udp.BeginReceive(ReceiveCallback, null);
        }

        ~Client() {
            Dispose(false);
        }

        public IPEndPoint Remote
        {
            get { return _remote; }
        }

        public event MessageHandler MessageReceived;

        private void ReceiveCallback(IAsyncResult ar) {
            var endPoint = default(IPEndPoint);
            byte[] data;
            try {
                data = _udp.EndReceive(ar, ref endPoint);
            } catch (SocketException) {
                // TODO: Close the connection (or mark it as closed).
                return;
            } catch (ObjectDisposedException) {
                // TODO: Close the connection (or mark it as closed).
                return;
            }

            _udp.BeginReceive(ReceiveCallback, null);
            if (MessageReceived == null) {
                return;
            }

            var type = (MessageType)data[0];
            // FIXME: Get the msgNumber.
            ulong msgNumber = 0;

            MessageReceived(this, type, msgNumber, data);
        }

        public ulong SendAck(MessageType originalType, ulong msgNumber) {
            const int size = sizeof(MessageType) + sizeof(ulong);

            using (var ms = AllocStream(MessageType.Ack, size))
            using (var w = new BinaryWriter(ms)) {
                w.Write((byte)originalType);
                w.Write(msgNumber);

                return Send(ms);
            }
        }

        public ulong SendDebug(string format, params object[] args) {
            var encoding = System.Text.Encoding.UTF8;

            var msg = string.Format(format, args);
            var buffer = AllocBuffer(
                MessageType.Debug, encoding.GetByteCount(msg));
            encoding.GetBytes(msg, 0, msg.Length, buffer, HeaderSize);

            return Send(buffer);
        }

        public ulong SendVesselUpdate(Guid vesselId, Diff<VesselStatus> diff) {
            using (var ms = AllocStream(MessageType.VesselUpdate, null))
            using (var w = new BinaryWriter(ms)) {
                w.Write(vesselId.ToByteArray());
                w.Write(diff.Changed);
                // DiffSerializer.Serialize(w, diff);
                return Send(ms);
            }
        }

        private ulong Send(MemoryStream ms) {
            return Send(ms.GetBuffer(), (int)ms.Length);
        }

        private ulong Send(byte[] buffer) {
            return Send(buffer, buffer.Length);
        }

        private ulong Send(byte[] buffer, int length) {
            _udp.Send(buffer, length);
            // FIXME: Really get the message number.
            return 0;
        }

        private byte[] AllocBuffer(MessageType type, int size) {
            using (var ms = AllocStream(type, size)) {
                return ms.GetBuffer();
            }
        }

        private MemoryStream AllocStream(MessageType type, int? size) {
            var ms = new MemoryStream(HeaderSize + size.GetValueOrDefault(0));
            var w = new BinaryWriter(ms);
            w.Write((byte)type);
            w.Write(_msgNumber++);
            // Flush, _but don't Dispose_ the writer. This way the stream will
            // stay open. It would be more semantically correct to create a
            // non-closing stream implementation, but doing it this way is
            // unlikely to cause problems.
            w.Flush();

            return ms;
        }

        #region IDisposable

        public void Dispose() {
            Dispose(true);
        }

        private void Dispose(bool disposing) {
            if (disposing) {
                _udp.Close();
            }
        }

        #endregion IDisposable
    }

}
