using Initializer.Databases;
using Initializer.Test.Integration.Databases.Collections;
using Shared.Test.Helpers.Fixtures;

namespace Initializer.Test.Integration.Databases
{
    [Collection(nameof(InitializerCollection))]
    public class ProgramTest
    {
        private readonly InitializerCollectionCluster _collectionCluster;

        public ProgramTest(InitializerCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task Main_WhenAppStarts_ShouldDoMigrations()
        {
            // Act
            await Program.Main([
                "--test",
                "--aws-url", $"http://localhost:{_collectionCluster.AwsFixture.GetPort()}"
            ]);

            // Assert
            // Should not throw exception
        }
    }
}
