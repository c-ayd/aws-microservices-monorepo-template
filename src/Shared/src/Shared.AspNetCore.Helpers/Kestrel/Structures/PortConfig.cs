using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Shared.AspNetCore.Helpers.Kestrel.Structures
{
    public class PortConfigs
    {
        internal Dictionary<string, PortConfig> ports = new Dictionary<string, PortConfig>();
        public IReadOnlyDictionary<string, PortConfig> Ports => ports.AsReadOnly();

        public class PortConfig
        {
            public required bool TlsEnabled { get; set; }
            public required ListenOptions Configuration { get; set; }
        }
    }
}
