using DotNet.Testcontainers.Containers;

namespace Common.Test.Helpers.Fixtures.SeedWork
{
    /// <summary>
    /// Provides common functionalities to fixtures using docker containers.
    /// </summary>
    public abstract class ContainerFixtureBase
    {
        protected abstract IContainer Container { get; }

        /// <summary>
        /// Pauses the container.
        /// </summary>
        public async Task BlockTrafficAsync()
        {
            await Container.PauseAsync();
        }

        /// <summary>
        /// Unpauses the container.
        /// </summary>
        public async Task UnblockTrafficAsync()
        {
            await Container.UnpauseAsync();
        }

        /// <summary>
        /// Returns the mapped port of the container.
        /// </summary>
        /// <param name="port">Port number inside the container. If the container has only one mapped port, this parameter has no effect.</param>
        /// <returns>Returns the mapped port.</returns>
        public abstract int GetMappedPort(int? port = null);
    }
}
