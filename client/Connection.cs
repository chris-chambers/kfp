using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Kfp
{
    public sealed class Connection : IDisposable
    {
        private readonly UdpClient _client;
        private readonly IPEndPoint _endpoint;
        private ulong _msgNumber;

        public Connection(IPEndPoint endpoint) {
            _client = new UdpClient();
            _endpoint = endpoint;

            _client.Connect(_endpoint);
            _client.BeginReceive(OnReceive, null);
        }

        ~Connection() {
            Dispose(false);
        }

        private void OnReceive(IAsyncResult res) {
            var endpoint = default(IPEndPoint);
            byte[] datagram = _client.EndReceive(res, ref endpoint);

            UnityEngine.Debug.LogFormat("kfp: got some bytes! {0}", datagram.Length);

            _client.BeginReceive(OnReceive, null);
        }

        public ulong SendDebug(string format, params object[] args) {
            var encoding = System.Text.Encoding.UTF8;

            var msg = string.Format(format, args);
            var buffer = AllocBuffer(MsgType.DebugMsg, encoding.GetByteCount(msg));
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

        private ulong Send(byte[] buffer) {
            return Send(buffer, buffer.Length);
        }

        private ulong Send(MemoryStream ms) {
            return Send(ms.GetBuffer(), (int)ms.Length);
        }

        private ulong Send(byte[] buffer, int length) {
            _client.Send(buffer, length);
            return _msgNumber++;
        }

        private byte[] AllocBuffer(MsgType type, int size) {
            var buffer = new byte[size + 1];
            buffer[0] = (byte)type;
            return buffer;
        }

        private MemoryStream AllocStream(MsgType type, int? size) {
            MemoryStream ms;
            if (size.HasValue) {
                ms = new MemoryStream(new byte[size.Value + 1]);
            } else {
                ms = new MemoryStream();
            }
            ms.WriteByte((byte)type);
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

    public enum MsgType : byte
    {
        DebugMsg = 0,
        VesselUpdate = 1,
    }
}
