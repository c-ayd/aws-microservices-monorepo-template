using System.Text.Json;

namespace Lambda.SecretsRotations.Utilities
{
    public static partial class Utility
    {
        /// <summary>
        /// Converts a JSON string to a given <see cref="Common.AspNetCore.Helpers.Options.IOptions"/> type.
        /// </summary>
        /// <typeparam name="T">Type of the options</typeparam>
        /// <param name="json">JSON string to convert</param>
        /// <param name="elementName">JSON key that contains the values of the options</param>
        /// <returns>Returns the converted options class if the JSON keys exists. Otherwise null.</returns>
        public static T? GetOptionsFromJson<T>(string json, string elementName)
            where T : class
        {
            using var jsonDoc = JsonDocument.Parse(json);
            if (jsonDoc.RootElement.TryGetProperty(elementName, out var element))
            {
                return JsonSerializer.Deserialize<T>(element);
            }

            return null;
        }
    }
}
