using System.Text.Json;

namespace Lambda.SecretsRotations.Utilities
{
    public static partial class Utility
    {
        /// <summary>
        /// Tries to deserialize a given JSON string to a given type.
        /// </summary>
        /// <typeparam name="T">Type of wrapper class for the given JSON string</typeparam>
        /// <param name="json">JSON string to deserialize</param>
        /// <param name="result">Deserialization result</param>
        /// <returns>Returns true if the deserialization was successful. Otherwise false.</returns>
        public static bool TryDeserializeJson<T>(string json, out T? result)
            where T : class
        {
            try
            {
                result = JsonSerializer.Deserialize<T>(json);
                return result != null;
            }
            catch
            {
                result = null;
                return false;
            }
        }
    }
}
