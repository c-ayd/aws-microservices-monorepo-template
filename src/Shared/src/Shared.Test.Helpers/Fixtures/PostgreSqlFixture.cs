using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.PostgreSql.Helpers.Options.Extensions;
using Testcontainers.PostgreSql;

namespace Shared.Test.Helpers.Fixtures
{
    /// <summary>
    /// Is a DbContext for test cases requiring a DB with EF Core.
    /// </summary>
    public class PostgreSqlFixture
    {
        private PostgreSqlContainer _container = null!;
        private BidirectionalDictionary<string, Type> _dbContexts = new BidirectionalDictionary<string, Type>();

        private bool _sslEnabled = false;
        private string? _caBundlePath = null;

        public async Task InitializeAsync(
            bool sslEnabled,
            string? caBundlePath = null,
            string? certificateKeyFilePath = null,
            string? certificateFilePath = null,
            Dictionary<string, Type>? dbContexts = null,
            bool runMigrations = true)
        {
            var builder = new PostgreSqlBuilder("postgres:18.4")
                .WithPortBinding(5432, true)
                .WithUsername("postgres")
                .WithPassword("postgres")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready"));

            if (sslEnabled)
            {
                _sslEnabled = true;
                _caBundlePath = caBundlePath;
                _container = builder
                    .WithSsl(certificateFilePath, certificateKeyFilePath)
                    .Build();
            }
            else
            {
                _sslEnabled = false;
                _caBundlePath = null;
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
                Ssl = !_sslEnabled ? null : new PostgreSqlConnectionOptions.SslOptions()
                {
                    CABundlePath = _caBundlePath!
                },
                RetryMechanism = null
            };
        }

        private string GetConnectionString(string dbName)
        {
            var options = GetConnectionOptions(dbName);
            var connStringBuilder = new NpgsqlConnectionStringBuilder()
            {
                Host = options.Host,
                Port = options.Port,
                Database = dbName,
                Username = options.Username,
                Password = options.Password,
            };

            if (_sslEnabled)
            {
                connStringBuilder.SslMode = SslMode.VerifyFull;
                connStringBuilder.RootCertificate = _caBundlePath;
            }

            return connStringBuilder.ConnectionString;
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
            var connStringBuilder = GetConnectionOptions(dbName);

            var ctor = dbContextType.GetConstructor([
                typeof(DbContextOptions<>).MakeGenericType(dbContextType)
            ])!;

            var options = ((DbContextOptionsBuilder)Activator.CreateInstance(typeof(DbContextOptionsBuilder<>).MakeGenericType(dbContextType))!)
                .UseNpgsql(GetConnectionString(dbName))
                .Options;

            return (T)ctor.Invoke([options]);
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
