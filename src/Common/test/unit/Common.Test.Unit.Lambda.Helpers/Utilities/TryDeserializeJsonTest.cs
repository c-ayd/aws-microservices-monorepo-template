using System.Text.Json;
using Common.Lambda.Helpers.Utilities;

namespace Common.Test.Unit.Lambda.Helpers.Utilities
{
    public class TryDeserializeJsonTest
    {
        [Fact]
        public void TryDeserializeJson_WhenExceptionIsThrown_ShouldReturnFalseAndNullResult()
        {
            // Arrange
            var json = JsonSerializer.Serialize(new
            {
                Int = 5
            });

            // Act
            var isDeserialized = Utility.TryDeserializeJson<TestClass>(json, section: "test", out var result);

            // Assert
            Assert.False(isDeserialized);
            Assert.Null(result);
        }

        [Fact]
        public void TryDeserializeJson_WhenSectionIsNullAndIsCorrectFormat_ShouldReturnTrueAndDeserializedResult()
        {
            // Arrange
            var json = JsonSerializer.Serialize(new
            {
                Int = 5
            });

            // Act
            var isDeserialized = Utility.TryDeserializeJson<TestClass>(json, section: null, out var result);

            // Assert
            Assert.True(isDeserialized);
            Assert.NotNull(result);
            Assert.Equal(5, result.Int);
        }

        [Fact]
        public void TryDeserializeJson_WhenSectionIsNotNullAndIsCorrectFormat_ShouldReturnTrueAndDeserializedResult()
        {
            // Arrange
            var json = JsonSerializer.Serialize(new
            {
                Inner = new
                {
                    Int = 5
                }
            });

            // Act
            var isDeserialized = Utility.TryDeserializeJson<TestClass>(json, section: "Inner", out var result);

            // Assert
            Assert.True(isDeserialized);
            Assert.NotNull(result);
            Assert.Equal(5, result.Int);
        }

        private class TestClass
        {
            public int Int { get; set; }
        }

        private class InnerTestClass
        {
            public required TestClass Inner { get; set; }
        }
    }
}
