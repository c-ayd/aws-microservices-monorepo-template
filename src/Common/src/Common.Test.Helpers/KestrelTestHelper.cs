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

        /// <summary>
        /// Check a given number of available ports on the local machine and returns the port.
        /// </summary>
        /// <param name="numberOfPorts">Number of ports to return</param>
        /// <returns>Returns a list of available ports.</returns>
        public static List<int> GetMultipleAvailablePortNumbers(int numberOfPorts)
        {
            var listeners = new List<TcpListener>();
            var ports = new List<int>();
            for (int i = 0; i < numberOfPorts; ++i)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();

                ports.Add(((IPEndPoint)listener.LocalEndpoint).Port);
            }

            foreach (var listener in listeners)
            {
                listener.Stop();
            }

            return ports;
        }
    }
}
