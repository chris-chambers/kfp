using System;
using System.Collections.Generic;
using System.IO;

namespace Kfp.ServerApp
{
    internal class GameServer
    {
        private readonly Server _server;
        private readonly HashSet<IConnection> _clients;
        private readonly Dictionary<Guid, Reckoning<VesselStatus, IConnection>> _vessels;

        internal GameServer(Server server)
        {
            _vessels = new Dictionary<Guid, Reckoning<VesselStatus, IConnection>>();
            _clients = new HashSet<IConnection>();
            _server = server;
            _server.ClientConnected += ClientConnected;
            _server.ClientDisconnected += ClientDisconnected;
        }

        private void ClientConnected(Server server, IConnection conn) {
            _clients.Add(conn);
            conn.MessageReceived += MessageReceived;
        }

        private void ClientDisconnected(Server server, IConnection conn) {
            conn.MessageReceived -= MessageReceived;
            _clients.Remove(conn);
            // FIXME: Remove from _vessels and other Reckoning collections.
        }

        private void MessageReceived(
            IConnection conn, MessageType type, ulong number, byte[] data)
        {
            // FIXME: Be lock-free
            lock (this) {

            switch (type) {
                case MessageType.Ack:
                    HandleAck(conn, type, number, data);
                    break;
                case MessageType.Debug:
                    HandleDebug(conn, type, number, data);
                    break;
                case MessageType.VesselUpdate:
                    HandleVesselUpdate(conn, type, number, data);
                    break;
                default:
                    Console.WriteLine("unknown message type: {0}", type);
                    break;
            }
            }
        }

        private void HandleAck(
            IConnection conn, MessageType type, ulong number, byte[] data)
        {
            // use number in payload to update our understanding of remote client
            // state
        }

        private void HandleDebug(
            IConnection conn, MessageType type, ulong number, byte[] data)
        {
            var encoding = System.Text.Encoding.UTF8;
            var msg = encoding.GetString(data, 9, data.Length - 9);
            Console.WriteLine("{0}", msg);
        }

        private void HandleVesselUpdate(
            IConnection conn, MessageType type, ulong number, byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms)) {
                reader.ReadByte(); // type
                var msgNumber = reader.ReadUInt64(); // msgNumber

                var id = new Guid(reader.ReadBytes(16));

                Reckoning<VesselStatus, IConnection> reckoning;
                if (!_vessels.TryGetValue(id, out reckoning)) {
                    reckoning = new Reckoning<VesselStatus, IConnection>();
                    foreach (var client in _clients) {
                        reckoning.AddObserver(client);
                    }

                    _vessels.Add(id, reckoning);
                }

                var diff = DiffSerializer.Deserialize<VesselStatus>(reader);
                reckoning.AddMoment(msgNumber, diff);
                reckoning.NotifyObserverPosition(conn, msgNumber);

                // var noopDiff = reckoning.GetDiff(conn);

                // TODO: At some point, notify other clients (batch it).

                Console.WriteLine(
                    "VesselUpdate: {0} {1,32}",
                    id, Convert.ToString(diff.Changed, 2));
            }
        }
    }
}
