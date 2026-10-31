using System.Reflection;

namespace Common.Constants
{
    /// <summary>
    /// Holds information about the supported languages across the application and
    /// provides a method to keep any given language within the supported langauge range.
    /// </summary>
    public static class SupportedLanguages
    {
        public const string DefaultLanguage = "en";

        public const string English = "en";
        public const string German = "de";

        public static readonly IReadOnlyList<string> AllLanguages = typeof(SupportedLanguages)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(l => l.Name != nameof(SupportedLanguages.DefaultLanguage))
            .Select(f => (string)f.GetValue(null)!)
            .ToList();

        /// <summary>
        /// Checks a given language and returns if it is valid. If not, it returns
        /// the default language.
        /// </summary>
        /// <param name="language">Language to check</param>
        /// <returns>Returns a supported language.</returns>
        public static string CheckAndGetValidLanguage(string? language)
        {
            if (language == null)
                return DefaultLanguage;

            if (AllLanguages.Contains(language))
                return language;

            return DefaultLanguage;
        }
    }
}
