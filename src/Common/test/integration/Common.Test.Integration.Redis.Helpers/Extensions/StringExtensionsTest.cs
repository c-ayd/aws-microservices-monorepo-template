using System.Reflection;
using Common.Redis.Helpers.Extensions;
using Common.Test.Generators;
using Common.Test.Helpers.Fixtures;

namespace Common.Test.Integration.Redis.Helpers.Extensions
{
    public class StringExtensionsTest : IClassFixture<RedisFixture>, IAsyncLifetime
    {
        private readonly RedisFixture _redisFixture;

        public StringExtensionsTest(RedisFixture redisFixture)
        {
            _redisFixture = redisFixture;
        }

        public async Task InitializeAsync()
        {
            await _redisFixture.InitializeAsync(
                sslEnabled: true,
                caBundlePath: "Certs/test-ca-bundle.crt",
                redisCertificateFolder: "Certs/Redis");
        }

        public async Task DisposeAsync()
        {
            await _redisFixture.DisposeAsync();
        }

        public static TheoryData<int, Type, object> Values()
        {
            return new TheoryData<int, Type, object>()
            {
                { 10, typeof(int), 1 },
                { 20, typeof(double), 1.23 },
                { 30, typeof(long), 5L },
                { 40, typeof(string), "Test" },
                { 50, typeof(char), 'c' },
                { 60, typeof(bool), true },
                { 70, typeof(TimeSpan), TimeSpan.FromSeconds(30) },
                { 80, typeof(DateTimeOffset), DateTimeOffset.UtcNow },
                { 90, typeof(TestRecord), new TestRecord("TestValue1", 1, 1.1) },
                { 100, typeof(TestClass), new TestClass() { StrValue = "TestValue2", IntValue = 2, DoubleValue = 2.2 } },
            };
        }

        [Theory]
        [MemberData(nameof(Values))]
        public async Task SaveAndLoadAsync_WhenValueIsSaved_ShouldLoadValueProperly(int keyLength, Type type, object value)
        {
            // Arrange
            var key = StringGenerator.GenerateAlphanumeric(keyLength);
            var saveMethodInfo = typeof(StringExtensions).GetMethod(nameof(StringExtensions.SaveAsStringAsync), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod([type]);
            var loadMethodInfo = typeof(StringExtensions).GetMethod(nameof(StringExtensions.LoadFromStringAsync), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod([type]);

            // Act
            await (Task)saveMethodInfo.Invoke(null, [_redisFixture.GetDatabase(), key, value, TimeSpan.FromMinutes(1)])!;
            var result = await (dynamic)loadMethodInfo.Invoke(null, [_redisFixture.GetDatabase(), key])!;

            // Assert
            Assert.True(result.Item1, "The key is not found.");
            Assert.NotNull(result.Item2);

            if (type == typeof(int)) Assert.Equal((int)value, (int)result.Item2);
            else if (type == typeof(double)) Assert.Equal((double)value, (double)result.Item2);
            else if (type == typeof(float)) Assert.Equal((float)value, (float)result.Item2);
            else if (type == typeof(long)) Assert.Equal((long)value, (long)result.Item2);
            else if (type == typeof(string)) Assert.Equal((string)value, (string)result.Item2);
            else if (type == typeof(char)) Assert.Equal((char)value, (char)result.Item2);
            else if (type == typeof(bool)) Assert.Equal((bool)value, (bool)result.Item2);
            else if (type == typeof(TimeSpan)) Assert.Equal((TimeSpan)value, (TimeSpan)result.Item2);
            else if (type == typeof(DateTimeOffset)) Assert.Equal((DateTimeOffset)value, (DateTimeOffset)result.Item2);
            else if (type == typeof(TestRecord)) Assert.Equal((TestRecord)value, (TestRecord)result.Item2);
            else if (type == typeof(TestClass)) Assert.Equal((TestClass)value, (TestClass)result.Item2);
            else Assert.Fail($"The given type {type} is not correct for the value.");
        }

        [Fact]
        public async Task LoadFromStringAsync_WhenKeyIsNotFound_ShouldReturnNull()
        {
            // Act
            var key = StringGenerator.GenerateAlphanumeric();
            var result = await _redisFixture.GetDatabase().LoadFromStringAsync<TestClass>(key);

            // Assert
            Assert.False(result.isKeyFound, $"There is a value with the {key} key.");
            Assert.Null(result.value);
        }

        private record TestRecord(
            string StrValue,
            int IntValue,
            double DoubleValue
        );

        private class TestClass
        {
            public required string StrValue { get; set; }
            public required int IntValue { get; set; }
            public required double DoubleValue { get; set; }

            public override bool Equals(object? obj)
            {
                if (obj is not TestClass otherObj)
                    return false;

                return StrValue == otherObj.StrValue && 
                    IntValue == otherObj.IntValue &&
                    DoubleValue == otherObj.DoubleValue;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(StrValue, IntValue, DoubleValue);
            }
        }
    }
}
