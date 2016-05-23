using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Kfp;

namespace Kfp.Server
{
    public static class Program
    {
        public static void Main(string[] args) {
            var conn = Connection.CreateServer(6754);
            conn.MsgReceived += MsgReceived;

            Console.WriteLine("Press a key to exit...");
            Console.ReadKey();
        }

        static void MsgReceived(
            MsgType type, ulong number, byte[] data, IPEndPoint remoteEndPoint)
        {
            switch (type) {
                case MsgType.Ack: {
                    // do nothing
                    break;
                }
                case MsgType.Debug: {
                    var encoding = System.Text.Encoding.UTF8;
                    var msg = encoding.GetString(data, 9, data.Length - 9);
                    Console.WriteLine("{0}", msg);
                    break;
                }
                case MsgType.VesselUpdate: {
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

        static async Task Serve() {
            var listener = new UdpClient(6754);
            var token = new CancellationToken();
            try {
                while (true) {
                    var datagram = await listener
                        .ReceiveAsync()
                        .WithCancellation(token);

                    var type = (MsgType)datagram.Buffer[0];

                    // TODO: set the message type to Ack
                    // TODO: get the message number and repeat it
                    listener.Send(new byte[5], 5, datagram.RemoteEndPoint);

                    switch (type) {
                        case MsgType.Debug: {
                            var encoding = System.Text.Encoding.UTF8;
                            var msg = encoding.GetString(datagram.Buffer, 1,
                                                         datagram.Buffer.Length - 1);
                            Console.WriteLine("{0}", msg);
                            break;
                        }
                        case MsgType.VesselUpdate: {
                            using (var ms = new MemoryStream(datagram.Buffer))
                            using (var reader = new BinaryReader(ms))
                            {
                                reader.ReadByte();
                                var vesselId = new Guid(reader.ReadBytes(16));
                                var changed = reader.ReadInt32();

                                Console.WriteLine(
                                    "vessel update: {0} {1}",
                                    vesselId, Convert.ToString(changed, 2));
                            }
                            break;
                        }
                        default:
                            Console.WriteLine("unknown message type: {0}", type);
                            break;
                    }
                }
            } catch (OperationCanceledException) {
                Console.WriteLine("shutting down.");
            }
        }
    }

    public static class AsyncExtensions
    {
        public static async Task<T> WithCancellation<T>(
            this Task<T> task, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            using (ct.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs))
            {
                if (task != await Task.WhenAny(task, tcs.Task)) {
                    throw new OperationCanceledException(ct);
                }
            }

            return task.Result;
        }
    }
}
