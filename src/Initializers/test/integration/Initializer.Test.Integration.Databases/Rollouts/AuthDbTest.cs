using AuthService.Domain.Entities;
using AuthService.Persistence.DbContexts;
using Initializer.Databases.Options;
using Initializer.Databases.Rollouts;
using Microsoft.EntityFrameworkCore;
using Common.PostgreSql.Helpers.Options.Extensions;
using Common.Test.Generators;
using Common.Test.Helpers.Fixtures;

namespace Initializer.Test.Integration.Databases
{
    public class AuthDbTest : IAsyncLifetime
    {
        private const string _roleName = "Admin";
        private const string _email = "test@test.com";

        private readonly PostgreSqlFixture _postgreSqlFixture;

        private PostgreSqlConnectionOptions _connectionOptions = null!;
        private readonly CABundleOptions _caBundleOptions = new CABundleOptions()
        {
            Value = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Certs/test-ca-bundle.crt"))
        };
        private readonly AuthDb.SeedDataOptions _seedDataOptions = new AuthDb.SeedDataOptions()
        {
            Roles = new List<string>() { _roleName },
            Accounts = new List<AuthDb.SeedDataOptions.AccountDetails>()
            {
                new AuthDb.SeedDataOptions.AccountDetails() { Email = _email, Roles = [_roleName] }
            }
        };

        public AuthDbTest()
        {
            _postgreSqlFixture = new PostgreSqlFixture();
        }

        public async Task InitializeAsync()
        {
            await Utility.InitializePostgreSqlFixtureAsync(_postgreSqlFixture);
            _connectionOptions = _postgreSqlFixture.GetConnectionOptions(Utility.AuthDbName);
        }

        public async Task DisposeAsync()
        {
            await _postgreSqlFixture.DisposeAsync();
        }

        [Fact]
        public async Task RolloutAsync_WhenThereIsNoData_ShouldSeedAllData()
        {
            // Arrange
            await _postgreSqlFixture.ClearDatabaseAsync<AuthDbContext>();

            // Act
            await AuthDb.RolloutAsync(_connectionOptions, _caBundleOptions, _seedDataOptions);

            // Assert
            await using var authDbContext = _postgreSqlFixture.CreateDbContext<AuthDbContext>();
            var rolesFromDb = await authDbContext.Roles.ToListAsync();
            var accountsFromDb = await authDbContext.Accounts.Include(a => a.Roles).ToListAsync();
            Assert.Single(rolesFromDb);
            Assert.Single(accountsFromDb);

            Assert.Equal(_roleName, rolesFromDb[0].Name);

            Assert.Equal(_email, accountsFromDb[0].Email);
            Assert.Equal(_roleName, accountsFromDb[0].Roles.ElementAt(0).Name);
        }

        [Fact]
        public async Task RolloutAsync_WhenThereIsData_ShouldSeedOnlyUniqueData()
        {
            // Arrange
            await _postgreSqlFixture.ClearDatabaseAsync<AuthDbContext>();

            await using var authDbContext = _postgreSqlFixture.CreateDbContext<AuthDbContext>();
            await authDbContext.Roles.AddAsync(new Role(StringGenerator.GenerateAlphanumeric()));
            await authDbContext.Accounts.AddAsync(new Account(EmailGenerator.Generate(), PasswordGenerator.Generate(), "abc"));
            await authDbContext.SaveChangesAsync();

            // Act
            await AuthDb.RolloutAsync(_connectionOptions, _caBundleOptions, _seedDataOptions);

            // Assert
            authDbContext.ChangeTracker.Clear();
            var rolesFromDb = await authDbContext.Roles.ToListAsync();
            var accountsFromDb = await authDbContext.Accounts.Include(a => a.Roles).ToListAsync();

            Assert.NotNull(rolesFromDb.FirstOrDefault(r => r.Name == _roleName));
            Assert.NotNull(accountsFromDb.FirstOrDefault(a => a.Email == _email));
        }
    }
}
