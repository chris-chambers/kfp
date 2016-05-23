using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Kfp
{
    public delegate void MsgReceivedHandler(
        MsgType type, ulong number, byte[] data, IPEndPoint removeEndPoint);

    public sealed class Connection : IDisposable
    {
        private const int HeaderSize = 9;

        private readonly UdpClient _client;
        private ulong _msgNumber;

        public static Connection CreateServer(int port) {
            var client = new UdpClient(port);
            return new Connection(client);
        }

        public static Connection CreateClient(IPEndPoint serverEndPoint) {
            var client = new UdpClient();
            client.Connect(serverEndPoint);

            return new Connection(client);
        }

        private Connection(UdpClient client) {
            _client = client;
            _client.BeginReceive(ReceiveCallback, null);
        }

        ~Connection() {
            Dispose(false);
        }

        public event MsgReceivedHandler MsgReceived;

        private void ReceiveCallback(IAsyncResult ar) {
            var endPoint = default(IPEndPoint);
            byte[] data;
            try {
                data = _client.EndReceive(ar, ref endPoint);
            } catch (SocketException) {
                // TODO: Close the connection (or mark it as closed).
                return;
            } catch (ObjectDisposedException) {
                // TODO: Close the connection (or mark it as closed).
                return;
            }

            _client.BeginReceive(ReceiveCallback, null);
            if (MsgReceived == null) {
                return;
            }

            var type = (MsgType)data[0];
            // FIXME: Get the msgNumber.
            ulong msgNumber = 0;

            MsgReceived(type, msgNumber, data, endPoint);
        }

        public ulong SendAck(MsgType originalType, ulong msgNumber) {
            const int size = sizeof(MsgType) + sizeof(ulong);

            using (var ms = AllocStream(MsgType.Ack, size))
            using (var w = new BinaryWriter(ms)) {
                w.Write((byte)originalType);
                w.Write(msgNumber);

                return Send(ms);
            }
        }

        public ulong SendDebug(string format, params object[] args) {
            var encoding = System.Text.Encoding.UTF8;

            var msg = string.Format(format, args);
            var buffer = AllocBuffer(MsgType.Debug, encoding.GetByteCount(msg));
            encoding.GetBytes(msg, 0, msg.Length, buffer, 1);

            return Send(buffer);
        }

        public ulong SendVesselUpdate(Guid vesselId, MagicDiff<VesselStatus> diff) {
            using (var ms = AllocStream(MsgType.VesselUpdate, null))
            using (var w = new BinaryWriter(ms)) {
                w.Write(vesselId.ToByteArray());
                MagicSerializer.Write(w, diff);
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
            _client.Send(buffer, length);
            // FIXME: Really get the message number.
            return 0;
        }

        private byte[] AllocBuffer(MsgType type, int size) {
            using (var ms = AllocStream(type, size)) {
                return ms.GetBuffer();
            }
        }

        private MemoryStream AllocStream(MsgType type, int? size) {
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
                _client.Close();
            }
        }

        #endregion IDisposable
    }
}
