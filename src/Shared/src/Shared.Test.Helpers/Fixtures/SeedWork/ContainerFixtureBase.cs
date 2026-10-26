using DotNet.Testcontainers.Containers;

namespace Shared.Test.Helpers.Fixtures.SeedWork
{
    public abstract class ContainerFixtureBase
    {
        protected abstract IContainer Container { get; }

        public async Task BlockTrafficAsync()
        {
            await Container.PauseAsync();
        }

        public async Task UnblockTrafficAsync()
        {
            await Container.UnpauseAsync();
        }
    }
}
