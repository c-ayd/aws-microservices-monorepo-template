using System.Net;
using System.Net.Sockets;

namespace Shared.Test.Helpers
{
    public static class KestrelHelper
    {
        public static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            return port;
        }
    }
}
