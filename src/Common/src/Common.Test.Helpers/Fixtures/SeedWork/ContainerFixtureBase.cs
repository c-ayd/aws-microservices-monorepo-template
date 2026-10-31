using DotNet.Testcontainers.Containers;

namespace Common.Test.Helpers.Fixtures.SeedWork
{
    /// <summary>
    /// Provides common functionalities to fixtures using docker containers.
    /// </summary>
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
