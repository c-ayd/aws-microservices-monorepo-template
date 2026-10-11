using Shared.Test.Helpers.Fixtures;

namespace Shared.Test.Integration.RabbitMq.Helpers.Collections
{
    [CollectionDefinition(nameof(RabbitMqCollection))]
    public class RabbitMqCollection : ICollectionFixture<RabbitMqCollectionCluster>
    {
    }

    public class RabbitMqCollectionCluster : IAsyncLifetime
    {
        public const string CABundlePath = "Certs/test-ca-bundle.crt";

        public RabbitMqFixture RabbitMqFixture { get; private set; }

        public RabbitMqCollectionCluster()
        {
            RabbitMqFixture = new RabbitMqFixture();
        }

        public async Task InitializeAsync()
        {
            await RabbitMqFixture.InitializeAsync(
                sslEnabled: true,
                caBundlePath: CABundlePath,
                rabbitMqCertificateFolder: "Certs/RabbitMq");
        }

        public async Task DisposeAsync()
        {
            await RabbitMqFixture.DisposeAsync();
        }
    }
}
