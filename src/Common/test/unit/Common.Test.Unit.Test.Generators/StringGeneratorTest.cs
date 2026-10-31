using Common.Test.Generators;

namespace Common.Test.Unit.Test.Generators
{
    public class StringGeneratorTest
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GeneratePrintableAscii_WhenLengthIsInvalid_ShouldThrowException(int length)
        {
            // Act
            var exception = Record.Exception(() =>
            {
                StringGenerator.GeneratePrintableAscii(length);
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void GeneratePrintableAscii_WhenLengthIsValid_ShouldGenerateString()
        {
            // Act
            var length = 10;
            var generatedString = StringGenerator.GeneratePrintableAscii(length);

            // Assert
            Assert.NotNull(generatedString);
            Assert.Equal(length, generatedString.Length);
            
            for (int i = 0; i < generatedString.Length; ++i)
            {
                if (generatedString[i] <= 31 || generatedString[i] >= 127)
                    Assert.Fail($"The generated string has a non-printable character. Char code: {(int)generatedString[i]}");
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GenerateAlpha_WhenLengthIsInvalid_ShouldThrowException(int length)
        {
            // Act
            var exception = Record.Exception(() =>
            {
                StringGenerator.GenerateAlpha(length);
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void GenerateAlpha_WhenLengthIsLowerThanOrEqualToTwo_ShouldGenerateStringWithLengthOfTwo(int length)
        {
            // Act
            var generatedString = StringGenerator.GenerateAlpha(length);

            // Assert
            Assert.NotNull(generatedString);
            Assert.Equal(2, generatedString.Length);

            bool hasUppercase = false, hasLowercase = false;
            for (int i = 0; i < generatedString.Length; ++i)
            {
                if (generatedString[i] >= 65 || generatedString[i] <= 90) hasUppercase = true;
                if (generatedString[i] >= 97 || generatedString[i] <= 122) hasLowercase = true;
            }

            Assert.True(hasUppercase && hasLowercase, $"The generated string is missing a character type. Uppercase: {hasUppercase}, Lowercase: {hasLowercase}");
        }

        [Fact]
        public void GenerateAlpha_WhenLengthIsGreaterThanTwo_ShouldGenerateString()
        {
            // Act
            var length = 10;
            var generatedString = StringGenerator.GenerateAlpha(length);

            // Assert
            Assert.NotNull(generatedString);
            Assert.Equal(length, generatedString.Length);

            bool hasUppercase = false, hasLowercase = false;
            for (int i = 0; i < generatedString.Length; ++i)
            {
                if (generatedString[i] >= 65 || generatedString[i] <= 90) hasUppercase = true;
                if (generatedString[i] >= 97 || generatedString[i] <= 122) hasLowercase = true;
            }

            Assert.True(hasUppercase && hasLowercase,
                $"The generated string is missing a character type. Uppercase: {hasUppercase}, Lowercase: {hasLowercase}");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GenerateAlphanumeric_WhenLengthIsInvalid_ShouldThrowException(int length)
        {
            // Act
            var exception = Record.Exception(() =>
            {
                StringGenerator.GenerateAlphanumeric(length);
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void GenerateAlphanumeric_WhenLengthIsLowerThanOrEqualToThree_ShouldGenerateStringWithLengthOfThree(int length)
        {
            // Act
            var generatedString = StringGenerator.GenerateAlphanumeric(length);

            // Assert
            Assert.NotNull(generatedString);
            Assert.Equal(3, generatedString.Length);

            bool hasUppercase = false, hasLowercase = false, hasDigit = false;
            for (int i = 0; i < generatedString.Length; ++i)
            {
                if (generatedString[i] >= 65 || generatedString[i] <= 90) hasUppercase = true;
                if (generatedString[i] >= 97 || generatedString[i] <= 122) hasLowercase = true;
                if (generatedString[i] >= 48 || generatedString[i] <= 57) hasDigit = true;
            }

            Assert.True(hasUppercase && hasLowercase && hasDigit,
                $"The generated string is missing a character type. Uppercase: {hasUppercase}, Lowercase: {hasLowercase}, Digit: {hasDigit}");
        }

        [Fact]
        public void GenerateAlphanumeric_WhenLengthIsGreaterThanThree_ShouldGenerateString()
        {
            // Act
            var length = 10;
            var generatedString = StringGenerator.GenerateAlphanumeric(length);

            // Assert
            Assert.NotNull(generatedString);
            Assert.Equal(length, generatedString.Length);

            bool hasUppercase = false, hasLowercase = false, hasDigit = false;
            for (int i = 0; i < generatedString.Length; ++i)
            {
                if (generatedString[i] >= 65 || generatedString[i] <= 90) hasUppercase = true;
                if (generatedString[i] >= 97 || generatedString[i] <= 122) hasLowercase = true;
                if (generatedString[i] >= 48 || generatedString[i] <= 57) hasDigit = true;
            }

            Assert.True(hasUppercase && hasLowercase && hasDigit,
                $"The generated string is missing a character type. Uppercase: {hasUppercase}, Lowercase: {hasLowercase}, Digit: {hasDigit}");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GenerateNumeric_WhenLengthIsInvalid_ShouldThrowException(int length)
        {
            // Act
            var exception = Record.Exception(() =>
            {
                StringGenerator.GenerateNumeric(length);
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void GenerateNumeric_WhenLengthIsValid_ShouldGenerateString()
        {
            // Act
            var length = 10;
            var generatedString = StringGenerator.GenerateNumeric(length);

            // Assert
            Assert.NotNull(generatedString);
            Assert.Equal(length, generatedString.Length);

            for (int i = 0; i < generatedString.Length; ++i)
            {
                if (generatedString[i] <= 47 || generatedString[i] >= 58)
                    Assert.Fail($"The generated string has a non-numeric character. Char code: {(int)generatedString[i]}");
            }
        }

        [Theory]
        [InlineData(null, 10)]
        [InlineData("", 10)]
        [InlineData("ABC", 0)]
        [InlineData("ABC", -1)]
        public void GenerateCustom_WhenParametersAreInvalid_ShouldThrowException(string? charList, int length)
        {
            // Act
            var exception = Record.Exception(() =>
            {
                StringGenerator.GenerateCustom(charList!, length);
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void GenerateCustom_WhenParametersAreValid_ShouldGenerateString()
        {
            // Arrange
            var charList = "ABCD";
            var length = 10;

            // Act
            var generatedString = StringGenerator.GenerateCustom(charList, length);

            // Assert
            Assert.NotNull(generatedString);
            Assert.Equal(length, generatedString.Length);

            for (int i = 0; i < generatedString.Length; ++i)
            {
                if (!charList.Contains(generatedString[i]))
                    Assert.Fail($"The generated string contains a wrong character. Char: {generatedString[i]}");
            }
        }

        [Theory]
        [InlineData(null, "ABC", 10)]
        [InlineData("", "ABC", 10)]
        [InlineData("ABC", null, 10)]
        [InlineData("ABC", "", 10)]
        [InlineData("ABC", "ABC", 0)]
        [InlineData("ABC", "ABC", -1)]
        public void AppendCustom_WhenParametersAreInvalid_ShouldThrowException(string? value, string? charList, int length)
        {
            // Act
            var exception = Record.Exception(() =>
            {
                StringGenerator.AppendCustom(value!, charList!, length);
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void AppendCustom_WhenParametersAreValid_ShouldGenerateString()
        {
            // Arrange
            var value = "XYZT";
            var charList = "ABCD";
            var length = 10;

            // Act
            var generatedString = StringGenerator.AppendCustom(value, charList, length);

            // Assert
            Assert.NotNull(generatedString);
            Assert.Equal(value.Length + length, generatedString.Length);

            if (!generatedString.StartsWith(value))
                Assert.Fail($"The generated string does not start with {value}. Generated string: {generatedString}");

            var appenedPart = generatedString.Substring(value.Length);
            for (int i = 0; i < appenedPart.Length; ++i)
            {
                if (!charList.Contains(appenedPart[i]))
                    Assert.Fail($"The generated string contains a wrong character. Char: {appenedPart[i]}");
            }
        }
    }
}
