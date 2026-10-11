using Microsoft.EntityFrameworkCore;
using Shared.PostgreSql.Helpers.EntityFramework.Extensions;
using Shared.PostgreSql.Helpers.Options.Extensions;
using Shared.Test.Integration.PostgreSql.Helpers.Collections;

namespace Shared.Test.Integration.PostgreSql.Helpers.EntityFramework.Extensions
{
    [Collection(nameof(PostgreSqlCollection))]
    public class ConfigureDbContextTest
    {
        private PostgreSqlCollectionCluster _collectionCluster;

        public ConfigureDbContextTest(PostgreSqlCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task EnsureCreated_WhenThereIsNoConnectionError_ShouldCreateDatabase()
        {
            // Arrange
            var connection = _collectionCluster.PostgreSqlFixture.GetConnectionOptions("test-db-1");
            var builder = new DbContextOptionsBuilder<TestDbContext1>();
            builder.ConfigureDbContext(connection);

            using var dbContext = new TestDbContext1(builder.Options);
            
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
            var connection = _collectionCluster.PostgreSqlFixture.GetConnectionOptions("test-db-3");
            _collectionCluster.ToxiProxyFixture.GetHostAndPort(out var host, out var port);
            connection.Host = host;
            connection.Port = port;
            connection.RetryMechanism = new PostgreSqlConnectionOptions.RetryMechanismOptions()
            {
                MaxRetry = 3,
                MaxDelayInSeconds = 3
            };
            var builder = new DbContextOptionsBuilder<TestDbContext3>();
            builder.ConfigureDbContext(connection);

            await _collectionCluster.ToxiProxyFixture.AddProxyAsync("DbContextProxy", connection.Host, connection.Port);
            await _collectionCluster.ToxiProxyFixture.BlockTrafficAsync("DbContextProxy", 15);

            using var dbContext = new TestDbContext3(builder.Options);

            // Act
            bool isFailed = false;
            try
            {
                await dbContext.Database.EnsureCreatedAsync();
            }
            catch
            {
                isFailed = true;
            }
            finally
            {
                await _collectionCluster.ToxiProxyFixture.RemoveProxyAsync("DbContextProxy");
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

        public class TestDbContext3 : DbContext
        {
            public DbSet<TestEntity> TestEntities { get; set; }

            public TestDbContext3(DbContextOptions<TestDbContext3> options) : base(options)
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
