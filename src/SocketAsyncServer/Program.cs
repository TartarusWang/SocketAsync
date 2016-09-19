using System;

namespace SocketAsyncServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var listener = new SocketListener(100, 2048);
            listener.Start(9900);
            Console.ReadLine();
        }
    }
}
