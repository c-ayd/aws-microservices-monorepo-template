using System.Net.Http.Json;
using Testcontainers.Toxiproxy;

namespace Shared.Test.Helpers.Fixtures
{
    public class ToxiProxyFixture
    {
        private ToxiproxyContainer _container = null!;
        private HttpClient _client = null!;

        public async Task InitializeAsync()
        {
            _container = new ToxiproxyBuilder("shopify/toxiproxy:2.1.4")
                .WithPortBinding(8474, true)
                .WithPortBinding(8666, true)
                .Build();

            await _container.StartAsync();
            
            _client = new HttpClient()
            {
                BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(8474)}")
            };
        }

        public void GetHostAndPort(out string host, out int port)
        {
            host = _container.Hostname;
            port = _container.GetMappedPublicPort(8666);
        }

        public async Task AddProxyAsync(string name, string host, int port)
        {
            await _client.PostAsJsonAsync("/proxies", new
            {
                name = name,
                listen = $"0.0.0.0:{_container.GetMappedPublicPort(8666)}",
                upstream = $"{host}:{port}"
            });
        }

        public async Task BlockTrafficAsync(string proxyName, int timeoutInSeconds)
        {
            await _client.PostAsJsonAsync($"/proxies/{proxyName}/toxics", new
            {
                name = "block_traffic",
                type = "timeout",
                toxicity = 1.0,
                attributes = new
                {
                    timeout = timeoutInSeconds * 1000
                }
            });
        }

        public async Task RemoveProxyAsync(string name)
        {
            await _client.DeleteAsync($"/proxies/{name}");
        }

        public async Task DisposeAsync()
        {
            _client.Dispose();
            await _container.DisposeAsync();
        }
    }
}
