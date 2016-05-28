using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Kfp
{
    // TODO: Establish rules about when a client is considered disconnected.
    // There can be a message, of course, that indicates a proper goodbye. But,
    // in cases where the client vanishes mysteriously, another strategy will be
    // needed.
    public sealed class Server
    {
        private readonly Dictionary<IPEndPoint, Connection> _clients;
        private readonly UdpClient _udp;

        public Server(int port) {
            _clients = new Dictionary<IPEndPoint, Connection>();

            _udp = new UdpClient(port);
            _udp.BeginReceive(ReceiveCallback, null);
        }

        public event ServerConnectionHandler ClientConnected;
        private void OnClientConnected(IConnection conn) {
            if (ClientConnected != null) {
                ClientConnected(this, conn);
            }
        }

        public event ServerConnectionHandler ClientDisconnected;
        private void OnClientDisconnected(IConnection conn) {
            if (ClientDisconnected != null) {
                ClientDisconnected(this, conn);
            }
        }

        private void ReceiveCallback(IAsyncResult ar) {
            var remote = default(IPEndPoint);
            byte[] data;
            try {
                data = _udp.EndReceive(ar, ref remote);
            } catch (SocketException) {
                // TODO: Close the server (or mark it as closed).
                return;
            } catch (ObjectDisposedException) {
                // TODO: Close the server (or mark it as closed).
                return;
            }

            _udp.BeginReceive(ReceiveCallback, null);
            GetOrCreateConnection(remote)
                .NotifyMsgReceived(data);
        }

        private Connection GetOrCreateConnection(IPEndPoint remote) {
            Connection conn;
            if (_clients.TryGetValue(remote, out conn)) {
                return conn;
            }
            conn = new Connection(this, remote);
            _clients.Add(remote, conn);
            OnClientConnected(conn);
            return conn;
        }

        private class Connection : IConnection
        {
            private readonly Server _owner;
            private readonly IPEndPoint _remote;

            internal Connection(Server owner, IPEndPoint remote) {
                _owner = owner;
                _remote = remote;
            }

            internal void NotifyMsgReceived(byte[] data) {
                if (MessageReceived == null) {
                    return;
                }

                // FIXME: Improve this code and unify it with Client.cs
                var type = (MessageType)data[0];
                // FIXME: Get the msgNumber.
                ulong msgNumber = 0;

                MessageReceived(this, type, msgNumber, data);
            }

            #region IConnection
            public IPEndPoint Remote
            {
                get { return _remote; }
            }

            public event MessageHandler MessageReceived;

            public ulong SendAck(MessageType originalType, ulong msgNumber)
            {
                throw new NotImplementedException();
            }

            public ulong SendDebug(string format, params object[] args)
            {
                throw new NotImplementedException();
            }

            public ulong SendVesselUpdate(Guid vesselId, Diff<VesselStatus> diff)
            {
                throw new NotImplementedException();
            }

            #endregion IConnection
        }
    }

    public delegate void ServerConnectionHandler(Server server, IConnection conn);
}
