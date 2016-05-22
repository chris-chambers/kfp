using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Kfp.Server
{
    public static class Program
    {
        public static void Main(string[] args) {
            Task.WaitAll(Serve());
        }

        static async Task Serve() {
            var listener = new UdpClient(6754);
            var token = new CancellationToken();
            try {
                while (true) {
                    var datagram = await listener
                        .ReceiveAsync()
                        .WithCancellation(token);

                    var type = datagram.Buffer[0];

                    // TODO: set the message type to Ack
                    // TODO: get the message number and repeat it
                    listener.Send(new byte[5], 5, datagram.RemoteEndPoint);

                    switch (type) {
                        case 0: {
                            var encoding = System.Text.Encoding.UTF8;
                            var msg = encoding.GetString(datagram.Buffer, 1,
                                                         datagram.Buffer.Length - 1);
                            Console.WriteLine("{0}", msg);
                            break;
                        }
                        case 1: {
                            using (var ms = new MemoryStream(datagram.Buffer))
                            using (var reader = new BinaryReader(ms))
                            {
                                reader.ReadByte();
                                var vesselId = new Guid(reader.ReadBytes(16));
                                var changed = reader.ReadInt32();

                                Console.WriteLine("vessel update: {0} {1}",
                                                  vesselId,
                                                  Convert.ToString(changed, 2));
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
