using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Kfp;

// FIXME: Decide on a namespace that isn't silly and doesn't conflict with the
// Kfp.Server class.
namespace Kfp.Servery
{
    public static class Program
    {
        public static void Main(string[] args) {
            var server = new Server(6754);
            server.ClientConnected +=
                (_, conn) => conn.MessageReceived += MessageReceived;
            server.ClientDisconnected +=
                (_, conn) => conn.MessageReceived -= MessageReceived;

            Console.WriteLine("Press a key to exit...");
            Console.ReadKey();
        }

        static void MessageReceived(
            IConnection conn, MessageType type, ulong number, byte[] data)
        {
            switch (type) {
                case MessageType.Ack: {
                    // do nothing
                    break;
                }
                case MessageType.Debug: {
                    var encoding = System.Text.Encoding.UTF8;
                    var msg = encoding.GetString(data, 9, data.Length - 9);
                    Console.WriteLine("{0}", msg);
                    break;
                }
                case MessageType.VesselUpdate: {
                    using (var ms = new MemoryStream(data))
                    using (var reader = new BinaryReader(ms)) {
                        reader.ReadByte(); // type
                        reader.ReadUInt64(); // msgNumber

                        var vesselId = new Guid(reader.ReadBytes(16));
                        var changed = reader.ReadInt32();

                        Console.WriteLine(
                            "VesselUpdate: {0} {1}",
                            vesselId, Convert.ToString(changed, 2));
                    }
                    break;
                }
                default:
                    Console.WriteLine("unknown message type: {0}", type);
                    break;
            }
        }
    }
}
