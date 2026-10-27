using Microsoft.Extensions.Configuration;

namespace Shared.Test.Helpers
{
    /// <summary>
    /// Provides methods to create configuration.
    /// </summary>
    public static class ConfigurationHelper
    {
        /// <summary>
        /// Creates configuration from a given JSON file.
        /// </summary>
        /// <param name="filePath">Path to the JSON file</param>
        /// <returns>Returns the created configuration.</returns>
        public static IConfiguration CreateConfigurationFromFile(string filePath, bool optional = false)
        {
            return new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(filePath, optional)
                .Build();
        }
    }
}
