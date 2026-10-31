using AuthService.Domain.SeedWork;
using Common.Test.Generators;

namespace AuthService.Test.Unit.Domain.SeedWork
{
    public class ValueObjectBaseTest
    {
        private class TestValueObject : ValueObjectBase
        {
            public string MyString { get; set; } = null!;
            public int MyInt { get; set; }

            public override IEnumerable<object?> GetEqualityComponents()
            {
                yield return MyString;
                yield return MyInt;
            }
        }

        [Fact]
        public void Equals_WhenTwoValueObjectsHaveSameValues_ShouldReturnTrue()
        {
            // Arrange
            var str = StringGenerator.GeneratePrintableAscii();
            var number = Random.Shared.Next();

            var vo1 = new TestValueObject()
            {
                MyString = str,
                MyInt = number
            };
            var vo2 = new TestValueObject()
            {
                MyString = str,
                MyInt = number
            };

            // Act
            var result = vo1.Equals(vo2);
            var resultObj = vo1.Equals((object)vo2);

            // Assert
            Assert.True(result, "The equals method accepting the same type did not return true.");
            Assert.True(resultObj, "The equals method accepting an object type did not return true.");
        }

        [Fact]
        public void Equals_WhenTwoValueObjectsHaveAtLeastOneDifferentValue_ShouldReturnFalse()
        {
            // Arrange
            var number = Random.Shared.Next();
            var vo1 = new TestValueObject()
            {
                MyString = StringGenerator.GeneratePrintableAscii(length: 10),
                MyInt = number
            };
            var vo2 = new TestValueObject()
            {
                MyString = StringGenerator.GeneratePrintableAscii(length: 5),
                MyInt = number
            };

            // Act
            var result = vo1.Equals(vo2);
            var resultObj = vo1.Equals((object)vo2);

            // Assert
            Assert.False(result, "The equals method accepting the same type did not return false.");
            Assert.False(resultObj, "The equals method accepting an object type did not return false.");
        }

        [Fact]
        public void Equals_WhenOneValueObjectIsNull_ShouldReturnFalse()
        {
            // Arrange
            var vo = new TestValueObject()
            {
                MyString = StringGenerator.GeneratePrintableAscii(),
                MyInt = Random.Shared.Next()
            };

            // Act
            var result = vo.Equals(null);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            var resultObj = vo.Equals((object)null);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            // Assert
            Assert.False(result, "The equals method accepting the same type did not return false.");
            Assert.False(resultObj, "The equals method accepting an object type did not return false.");
        }

        [Fact]
        public void EqualOperator_WhenTwoValueObjectsHaveSameValues_ShouldReturnTrue()
        {
            // Arrange
            var str = StringGenerator.GeneratePrintableAscii();
            var number = Random.Shared.Next();

            var vo1 = new TestValueObject()
            {
                MyString = str,
                MyInt = number
            };
            var vo2 = new TestValueObject()
            {
                MyString = str,
                MyInt = number
            };

            // Act
            var result = vo1 == vo2;

            // Assert
            Assert.True(result, "The equals operator did not return true.");
        }

        [Fact]
        public void EqualOperator_WhenTwoValueObjectsHaveAtLeastOneDifferentValue_ShouldReturnFalse()
        {
            // Arrange
            var number = Random.Shared.Next();
            var vo1 = new TestValueObject()
            {
                MyString = StringGenerator.GeneratePrintableAscii(length: 10),
                MyInt = number
            };
            var vo2 = new TestValueObject()
            {
                MyString = StringGenerator.GeneratePrintableAscii(length: 5),
                MyInt = number
            };

            // Act
            var result = vo1 == vo2;

            // Assert
            Assert.False(result, "The equal operator did not return false.");
        }

        [Fact]
        public void EqualOperator_WhenOneValueObjectIsNull_ShouldReturnFalse()
        {
            // Arrange
            var vo = new TestValueObject()
            {
                MyString = StringGenerator.GeneratePrintableAscii(),
                MyInt = Random.Shared.Next()
            };

            // Act
            var result = vo == null;

            // Assert
            Assert.False(result, "The equal operator did not return false.");
        }

        [Fact]
        public void NotEqualOperator_WhenTwoValueObjectsHaveValues_ShouldReturnFalse()
        {
            // Arrange
            var str = StringGenerator.GeneratePrintableAscii();
            var number = Random.Shared.Next();

            var vo1 = new TestValueObject()
            {
                MyString = str,
                MyInt = number
            };
            var vo2 = new TestValueObject()
            {
                MyString = str,
                MyInt = number
            };

            // Act
            var result = vo1 != vo2;

            // Assert
            Assert.False(result, "The equals operator did not return false.");
        }

        [Fact]
        public void NotEqualOperator_WhenTwoValueObjectHaveAtLeastOneDifferentValue_ShouldReturnTrue()
        {
            // Arrange
            var number = Random.Shared.Next();
            var vo1 = new TestValueObject()
            {
                MyString = StringGenerator.GeneratePrintableAscii(length: 10),
                MyInt = number
            };
            var vo2 = new TestValueObject()
            {
                MyString = StringGenerator.GeneratePrintableAscii(length: 5),
                MyInt = number
            };

            // Act
            var result = vo1 != vo2;

            // Assert
            Assert.True(result, "The equal operator did not return true.");
        }

        [Fact]
        public void NotEqualOperator_WhenOneEntityIsNull_ShouldReturnTrue()
        {
            // Arrange
            var vo = new TestValueObject()
            {
                MyString = StringGenerator.GeneratePrintableAscii(),
                MyInt = Random.Shared.Next()
            };

            // Act
            var result = vo != null;

            // Assert
            Assert.True(result, "The equal operator did not return true.");
        }

        [Fact]
        public void GetHashCode_WhenIsCalled_ShouldAggerateAllEqualityComponents()
        {
            // Arrange
            var str = StringGenerator.GeneratePrintableAscii();
            var number = Random.Shared.Next();
            var valuesHashCode = str.GetHashCode() ^ number.GetHashCode();

            var vo = new TestValueObject()
            {
                MyString = str,
                MyInt = number
            };

            // Act
            var hashCode = vo.GetHashCode();

            // Assert
            Assert.Equal(valuesHashCode, hashCode);
        }
    }
}
