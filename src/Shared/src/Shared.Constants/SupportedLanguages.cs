using System.Reflection;

namespace Shared.Constants
{
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
