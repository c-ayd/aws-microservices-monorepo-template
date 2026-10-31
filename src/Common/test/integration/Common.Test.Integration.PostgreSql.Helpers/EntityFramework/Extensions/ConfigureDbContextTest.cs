using Microsoft.EntityFrameworkCore;
using Common.PostgreSql.Helpers.EntityFramework.Extensions;
using Common.Test.Integration.PostgreSql.Helpers.Collections;

namespace Common.Test.Integration.PostgreSql.Helpers.EntityFramework.Extensions
{
    [Collection(nameof(PostgreSqlCollection))]
    public class ConfigureDbContextTest
    {
        private readonly PostgreSqlCollectionCluster _collectionCluster;
        private readonly string _caBundleValue;

        public ConfigureDbContextTest(PostgreSqlCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;

            _caBundleValue = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, PostgreSqlCollectionCluster.CABundlePath));
        }

        [Fact]
        public async Task ConfigureDbContext_WhenThereIsNoConnectionError_ShouldCreateDatabase()
        {
            // Arrange
            var connection = _collectionCluster.PostgreSqlFixture.GetConnectionOptions("test-db-1");
            var builder = new DbContextOptionsBuilder<TestDbContext1>();
            builder.ConfigureDbContext(connection, _caBundleValue);

            await using var dbContext = new TestDbContext1(builder.Options);
            
            // Act
            await dbContext.Database.EnsureCreatedAsync();

            // Assert
            var exception = await Record.ExceptionAsync(async () =>
            {
                await dbContext.TestEntities.CountAsync();
            });

            Assert.Null(exception);
        }

        [Fact]
        public async Task ConfigureDbContext_WhenRetryMechanismIsDefined_ShouldRetryMechanismStart()
        {
            // Arrange
            var connection = _collectionCluster.PostgreSqlFixture.GetConnectionOptions("test-db-2");
            var builder = new DbContextOptionsBuilder<TestDbContext2>();
            builder.ConfigureDbContext(connection, _caBundleValue);

            await using var dbContext = new TestDbContext2(builder.Options);

            await _collectionCluster.PostgreSqlFixture.BlockTrafficAsync();

            // Act
            bool isFailed = false;
            try
            {
                await dbContext.Database.EnsureCreatedAsync();
            }
            catch (Exception)
            {
                isFailed = true;
            }
            finally
            {
                await _collectionCluster.PostgreSqlFixture.UnblockTrafficAsync();
            }

            // Assert
            Assert.True(isFailed, "The process did not fail");
        }

        public class TestDbContext1 : DbContext
        {
            public DbSet<TestEntity> TestEntities { get; set; }

            public TestDbContext1(DbContextOptions<TestDbContext1> options) : base(options)
            {
            }
        }

        public class TestDbContext2 : DbContext
        {
            public DbSet<TestEntity> TestEntities { get; set; }

            public TestDbContext2(DbContextOptions<TestDbContext2> options) : base(options)
            {
            }
        }

        public class TestEntity
        {
            public int Id { get; set; }
            public int Value { get; set; }
        }
    }
}
