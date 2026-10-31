using System.Text;

namespace Common.Test.Generators.Extensions
{
    internal static class StringBuilderShuffle
    {
        internal static StringBuilder Shuffle(this StringBuilder builder)
        {
            for (int i = 0; i < builder.Length; ++i)
            {
                var swapIndex = Random.Shared.Next(0, builder.Length);
                
                var temp = builder[i];
                builder[i] = builder[swapIndex];
                builder[swapIndex] = temp;
            }

            return builder;
        }
    }
}
