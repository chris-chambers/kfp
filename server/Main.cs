using System;

using Kfp;

// FIXME: Decide on a namespace that isn't silly and doesn't conflict with the
// Kfp.Server class.
namespace Kfp.ServerApp
{
    public static class Program
    {
        public static void Main(string[] args) {
            new GameServer(new Server(6754));
            Console.WriteLine("Press a key to exit...");
            Console.ReadKey();
        }
    }
}
