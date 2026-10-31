using Common.Test.Generators;

namespace Common.Test.Unit.Test.Generators
{
    public class PasswordGeneratorTest
    {
        [Theory]
        [InlineData(true, "", 10)]
        [InlineData(true, "ABC", 0)]
        [InlineData(true, "ABC", -1)]
        public void Generate_WhenParametersAreInvalid_ShouldThrowException(bool includeSpecialChars, string specialChars, int length)
        {
            // Act
            var exception = Record.Exception(() =>
            {
                PasswordGenerator.Generate(
                    includeSpecialChars: includeSpecialChars,
                    specialChars: specialChars,
                    length: length);
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void Generate_WhenOnlyUppercaseIsIncluded_ShouldGeneratePasswordWithUppercase()
        {
            // Act
            var generatedPassword = PasswordGenerator.Generate(
                includeLowercase: false,
                includeDigit: false,
                includeSpecialChars: false);

            // Assert
            Assert.NotNull(generatedPassword);
            Assert.NotEmpty(generatedPassword);

            for (int i = 0; i < generatedPassword.Length; ++i)
            {
                if (generatedPassword[i] < 65 || generatedPassword[i] > 90)
                    Assert.Fail($"The generated password includes something other than uppercase characters. Password: {generatedPassword}");
            }
        }

        [Fact]
        public void Generate_WhenOnlyLowercaseIsIncluded_ShouldGeneratePasswordWithLowercase()
        {
            // Act
            var generatedPassword = PasswordGenerator.Generate(
                includeUppercase: false,
                includeDigit: false,
                includeSpecialChars: false);

            // Assert
            Assert.NotNull(generatedPassword);
            Assert.NotEmpty(generatedPassword);

            for (int i = 0; i < generatedPassword.Length; ++i)
            {
                if (generatedPassword[i] < 97 || generatedPassword[i] > 122)
                    Assert.Fail($"The generated password includes something other than lowercase characters. Password: {generatedPassword}");
            }
        }

        [Fact]
        public void Generate_WhenOnlyDigitIsIncluded_ShouldGeneratePasswordWithDigit()
        {
            // Act
            var generatedPassword = PasswordGenerator.Generate(
                includeUppercase: false,
                includeLowercase: false,
                includeSpecialChars: false);

            // Assert
            Assert.NotNull(generatedPassword);
            Assert.NotEmpty(generatedPassword);

            for (int i = 0; i < generatedPassword.Length; ++i)
            {
                if (generatedPassword[i] < 48 || generatedPassword[i] > 57)
                    Assert.Fail($"The generated password includes something other than digits. Password: {generatedPassword}");
            }
        }

        [Fact]
        public void Generate_WhenOnlySpecialCharsAreIncluded_ShouldGeneratePasswordWithSpecialChars()
        {
            // Arrange
            var specialChars = "[];',./";

            // Act
            var generatedPassword = PasswordGenerator.Generate(
                includeUppercase: false,
                includeLowercase: false,
                includeDigit: false,
                includeSpecialChars: true,
                specialChars: specialChars);

            // Assert
            Assert.NotNull(generatedPassword);
            Assert.NotEmpty(generatedPassword);

            for (int i = 0; i < generatedPassword.Length; ++i)
            {
                if (!specialChars.Contains(generatedPassword[i]))
                    Assert.Fail($"The generated password does not contain special characters only. Password: {generatedPassword}");
            }
        }

        [Fact]
        public void Generate_WhenAllRulesAreIncluded_ShouldGeneratePasswordWithAllRules()
        {
            // Arrange
            var specialChars = "[];',./";
            var passwordLength = 10;

            // Act
            var generatedPassword = PasswordGenerator.Generate(
                includeUppercase: true,
                includeLowercase: true,
                includeDigit: true,
                includeSpecialChars: true,
                specialChars: specialChars,
                length: passwordLength);

            // Assert
            Assert.NotNull(generatedPassword);
            Assert.Equal(passwordLength, generatedPassword.Length);

            bool hasUppercase = false, hasLowercase = false, hasDigit = false, hasSpecialChar = false;
            for (int i = 0; i < generatedPassword.Length; ++i)
            {
                if (generatedPassword[i] >= 65 || generatedPassword[i] <= 90) hasUppercase = true;
                if (generatedPassword[i] >= 97 || generatedPassword[i] <= 122) hasLowercase = true;
                if (generatedPassword[i] >= 48 || generatedPassword[i] <= 57) hasDigit = true;
                if (specialChars.Contains(generatedPassword[i])) hasSpecialChar = true;
            }

            Assert.True(hasUppercase && hasLowercase && hasDigit && hasSpecialChar,
                $"The generated password is missing a rule. Uppercase: {hasUppercase}, Lowercase: {hasLowercase}, Digit: {hasDigit}, Special Char: {hasSpecialChar}");
        }
    }
}
