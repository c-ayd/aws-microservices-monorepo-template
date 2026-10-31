namespace Common.Test.Generators
{
    internal static class AsciiHelper
    {
        private static List<(int min, int max)> _printableSpecialCharsRanges = new List<(int min, int max)>()
        {
            (33, 48), (58, 65), (91, 97), (123, 127)
        };

        internal static char GetRandomPrintableChar()
        {
            return (char)Random.Shared.Next(32, 127);
        }

        internal static char GetRandomPrintableSpecialChar()
        {
            var range = _printableSpecialCharsRanges[Random.Shared.Next(0, _printableSpecialCharsRanges.Count)];
            return (char)Random.Shared.Next(range.min, range.max);
        }

        internal static char GetRandomLowercaseChar()
        {
            return (char)Random.Shared.Next(97, 123);
        }

        internal static char GetRandomUppercaseChar()
        {
            return (char)Random.Shared.Next(65, 91);
        }

        internal static char GetRandomDigitChar()
        {
            return (char)Random.Shared.Next(48, 58);
        }
    }
}
