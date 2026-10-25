using Microsoft.EntityFrameworkCore;
using Shared.PostgreSql.Helpers.EntityFramework.Extensions;
using Shared.Test.Integration.PostgreSql.Helpers.Collections;

namespace Shared.Test.Integration.PostgreSql.Helpers.EntityFramework.Extensions
{
    [Collection(nameof(PostgreSqlCollection))]
    public class ConfigureDbContextTest
    {
        private PostgreSqlCollectionCluster _collectionCluster;

        private string _caBundle;

        public ConfigureDbContextTest(PostgreSqlCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;

            _caBundle = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Certs/test-ca-bundle.crt"));
        }

        [Fact]
        public async Task EnsureCreated_WhenThereIsNoConnectionError_ShouldCreateDatabase()
        {
            // Arrange
            var connection = _collectionCluster.PostgreSqlFixture.GetConnectionOptions("test-db-1");
            var builder = new DbContextOptionsBuilder<TestDbContext1>();
            builder.ConfigureDbContext(connection, _caBundle);

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
        public async Task EnsureCreated_WhenRetryMechanismIsDefined_ShouldRetryMechanismStart()
        {
            // Arrange
            var connection = _collectionCluster.PostgreSqlFixture.GetConnectionOptions("test-db-2");
            var builder = new DbContextOptionsBuilder<TestDbContext2>();
            builder.ConfigureDbContext(connection, _caBundle);

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

            // Assert
            await _collectionCluster.PostgreSqlFixture.UnblockTrafficAsync();

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
