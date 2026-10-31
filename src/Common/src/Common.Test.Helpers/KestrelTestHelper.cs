using System.Net;
using System.Net.Sockets;

namespace Common.Test.Helpers
{
    /// <summary>
    /// Provides methods related to Kestrel for tests.
    /// </summary>
    public static class KestrelTestHelper
    {
        /// <summary>
        /// Checks an available port on the local machine and returns the port.
        /// </summary>
        /// <returns>Returns an available port.</returns>
        public static int GetAvailablePortNumber()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            return port;
        }
    }
}
