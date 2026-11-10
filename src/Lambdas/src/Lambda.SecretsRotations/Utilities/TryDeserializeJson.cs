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
        /// <param name="section">JSON key at the root level to start deserialization</param>
        /// <param name="result">Deserialization result</param>
        /// <returns>Returns true if the deserialization was successful. Otherwise false.</returns>
        public static bool TryDeserializeJson<T>(string json, string? section, out T? result)
            where T : class
        {
            result = null;

            try
            {
                if (section == null)
                {
                    result = JsonSerializer.Deserialize<T>(json);
                }
                else
                {
                    using var jsonDoc = JsonDocument.Parse(json);
                    if (jsonDoc.RootElement.TryGetProperty(section, out var element))
                    {
                        result = JsonSerializer.Deserialize<T>(element.GetRawText());
                    }
                }

                return result != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
