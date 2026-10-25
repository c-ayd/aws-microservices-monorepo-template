using System.Text.Json;
using StackExchange.Redis;

namespace Shared.Redis.Helpers.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Converts a given value to a JSON string and saves the string to Redis DB.
        /// </summary>
        /// <typeparam name="T">Type of the given value</typeparam>
        /// <param name="key">Key of the Redis entry</param>
        /// <param name="value">Value to serialize to a JSON string</param>
        /// <param name="expirationTime">Lifespan of the entry. The default value is 1 hour</param>
        public static async Task SaveAsStringAsync<T>(this IDatabase db, string key, T value, TimeSpan? expirationTime = null)
        {
            var json = JsonSerializer.Serialize(value);

            await db.StringSetAsync(key, json, expirationTime ?? TimeSpan.FromHours(1));
        }

        /// <summary>
        /// Converts a JSON string entry from Redis DB to a given type and returns the value.
        /// </summary>
        /// <typeparam name="T">Type to deserialize the JSON string to</typeparam>
        /// <param name="key">Key of the Redis entry</param>
        /// <param name="cancellationToken">Token to cancel the fetching operation</param>
        /// <returns>Returns the converted value.</returns>
        public static async Task<(bool isKeyFound, T? value)> LoadFromStringAsync<T>(this IDatabase db, string key)
        {
            var json = await db.StringGetAsync(key);
            if (!json.HasValue)
                return (false, default);

            var value = JsonSerializer.Deserialize<T>(json.ToString());
            return (true, value);
        }
    }
}
