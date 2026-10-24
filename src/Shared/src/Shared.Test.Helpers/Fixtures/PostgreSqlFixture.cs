using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Shared.AspNetCore.Helpers.Kestrel.Options.Extensions;
using Shared.PostgreSql.Helpers.EntityFramework.Extensions;
using Shared.PostgreSql.Helpers.Options.Extensions;
using Shared.Test.Helpers.Fixtures.SeedWork;
using Testcontainers.PostgreSql;

namespace Shared.Test.Helpers.Fixtures
{
    /// <summary>
    /// Is a DbContext for test cases requiring a DB with EF Core.
    /// </summary>
    public class PostgreSqlFixture : ContainerFixtureBase
    {
        protected override IContainer Container => _container;
        private PostgreSqlContainer _container = null!;
        private BidirectionalDictionary<string, Type> _dbContexts = new BidirectionalDictionary<string, Type>();

        private bool _sslEnabled = false;
        private string? _caBundleValue = null;

        public async Task InitializeAsync(
            bool sslEnabled,
            string? caBundlePath = null,
            string? certificateKeyFilePath = null,
            string? certificateFilePath = null,
            Dictionary<string, Type>? dbContexts = null,
            bool runMigrations = true)
        {
            if (IsInitialized)
                return;
            
            var builder = new PostgreSqlBuilder("postgres:18.4")
                .WithPortBinding(5432, true)
                .WithUsername("postgres")
                .WithPassword("postgres")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready"));

            if (sslEnabled)
            {
                _sslEnabled = true;
                _caBundleValue = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, caBundlePath!));
                _container = builder
                    .WithSsl(
                        Path.Combine(AppContext.BaseDirectory, certificateFilePath!),
                        Path.Combine(AppContext.BaseDirectory, certificateKeyFilePath!))
                    .Build();
            }
            else
            {
                _sslEnabled = false;
                _caBundleValue = null;
                _container = builder.Build();
            }
            
            await _container.StartAsync();

            if (dbContexts != null)
            {
                _dbContexts.Add(dbContexts);

                if (runMigrations)
                {
                    foreach (var (dbName, dbContextType) in _dbContexts)
                    {
                        await using var dbContext = CreateDbContext(dbName);
                        await dbContext.Database.MigrateAsync();
                    }
                }
            }

            IsInitialized = true;
        }

        public PostgreSqlConnectionOptions GetConnectionOptions(string dbName)
        {
            return new PostgreSqlConnectionOptions()
            {
                Host = _container.Hostname,
                Port = _container.GetMappedPublicPort(5432),
                Database = dbName,
                Username = "postgres",
                Password = "postgres",
                Ssl = new SslOptions()
                {
                    Enabled = _sslEnabled,
                    ServerName = "localhost"
                },
                RetryMechanism = null
            };
        }

        public DbContext CreateDbContext(string dbName)
        {
            _dbContexts.TryGetValue(dbName, out var dbContexType);
            return CreateDbContext<DbContext>(dbName, dbContexType!); // No null check. If it is used wrong in test, it should throw an exception.
        }

        public T CreateDbContext<T>()
            where T : DbContext
        {
            _dbContexts.TryGetKey(typeof(T), out var dbName);
            return CreateDbContext<T>(dbName!, typeof(T)); // No null check. If it is used wrong in test, it should throw an exception.
        }

        private T CreateDbContext<T>(string dbName, Type dbContextType)
            where T : DbContext
        {
            var connectionOptions = GetConnectionOptions(dbName);

            var ctor = dbContextType.GetConstructor([
                typeof(DbContextOptions<>).MakeGenericType(dbContextType)
            ])!;

            var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(typeof(DbContextOptionsBuilder<>).MakeGenericType(dbContextType))!;
            optionsBuilder.ConfigureDbContext(connectionOptions, _caBundleValue);

            return (T)ctor.Invoke([optionsBuilder.Options]);
        }

        public async Task DropDatabaseAsync<T>()
            where T : DbContext
        {
            await using var dbContext = CreateDbContext<T>();

            if (await dbContext.Database.CanConnectAsync())
            {
                await dbContext.Database.EnsureDeletedAsync();
            }
        }

        public async Task ClearDatabaseAsync<T>()
            where T : DbContext
        {
            await using var dbContext = CreateDbContext<T>();

            if (await dbContext.Database.CanConnectAsync())
            {
                await dbContext.Database.EnsureDeletedAsync();
            }

            await dbContext.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }

        private class BidirectionalDictionary<TKey, TValue>
            where TKey : notnull
            where TValue : notnull
        {
            private Dictionary<TKey, TValue> _forward = new Dictionary<TKey, TValue>();
            private Dictionary<TValue, TKey> _backward = new Dictionary<TValue, TKey>();

            public void Add(Dictionary<TKey, TValue> dictionary)
            {
                foreach (var item in dictionary)
                {
                    _forward.Add(item.Key, item.Value);
                    _backward.Add(item.Value, item.Key);
                }
            }

            public bool TryGetValue(TKey key, out TValue? value)
            {
                return _forward.TryGetValue(key, out value);
            }

            public bool TryGetKey(TValue value, out TKey? key)
            {
                return _backward.TryGetValue(value, out key);
            }

            public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            {
                return _forward.GetEnumerator();
            }
        }
    }
}
