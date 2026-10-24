using AuthService.Domain.Entities;
using AuthService.Persistence.DbContexts;
using Initializer.Databases.Options;
using Initializer.Databases.Rollouts;
using Microsoft.EntityFrameworkCore;
using Shared.PostgreSql.Helpers.Options.Extensions;
using Shared.Test.Generators;
using Shared.Test.Helpers.Fixtures;

namespace Initializer.Test.Integration.Databases
{
    public class AuthDbTest : IAsyncLifetime
    {
        private readonly PostgreSqlFixture _postgreSqlFixture;

        private PostgreSqlConnectionOptions _connectionOptions = null!;
        private readonly CABundleOptions _caBundleOptions = new CABundleOptions()
        {
            Value = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Certs/test-ca-bundle.crt"))
        };
        private readonly AuthDb.SeedDataOptions _seedDataOptions = new AuthDb.SeedDataOptions()
        {
            Roles = new List<string>() { "Admin" },
            Accounts = new List<AuthDb.SeedDataOptions.AccountDetails>()
            {
                new AuthDb.SeedDataOptions.AccountDetails() { Email = "test@test.com", Roles = ["Admin"] }
            }
        };

        public AuthDbTest()
        {
            _postgreSqlFixture = new PostgreSqlFixture();
        }

        public async Task InitializeAsync()
        {
            await Utility.InitializePostgreSqlFixture(_postgreSqlFixture);
            _connectionOptions = _postgreSqlFixture.GetConnectionOptions("auth-db");
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
            await using var dbContext = _postgreSqlFixture.CreateDbContext<AuthDbContext>();
            var rolesFromDb = await dbContext.Roles.ToListAsync();
            var accountsFromDb = await dbContext.Accounts.Include(a => a.Roles).ToListAsync();
            Assert.Single(rolesFromDb);
            Assert.Single(accountsFromDb);

            Assert.Equal("Admin", rolesFromDb[0].Name);

            Assert.Equal("test@test.com", accountsFromDb[0].Email);
            Assert.Equal("Admin", accountsFromDb[0].Roles.ElementAt(0).Name);
        }

        [Fact]
        public async Task RolloutAsync_WhenThereIsData_ShouldSeedOnlyUniqueData()
        {
            // Arrange
            await _postgreSqlFixture.ClearDatabaseAsync<AuthDbContext>();

            await using var dbContext = _postgreSqlFixture.CreateDbContext<AuthDbContext>();
            await dbContext.Roles.AddAsync(new Role(StringGenerator.GenerateAlphanumeric()));
            await dbContext.Accounts.AddAsync(new Account(EmailGenerator.Generate(), PasswordGenerator.Generate(), "abc"));
            await dbContext.SaveChangesAsync();

            // Act
            await AuthDb.RolloutAsync(_connectionOptions, _caBundleOptions, _seedDataOptions);

            // Assert
            dbContext.ChangeTracker.Clear();
            var rolesFromDb = await dbContext.Roles.ToListAsync();
            var accountsFromDb = await dbContext.Accounts.Include(a => a.Roles).ToListAsync();

            Assert.NotNull(rolesFromDb.FirstOrDefault(r => r.Name == "Admin"));
            Assert.NotNull(accountsFromDb.FirstOrDefault(a => a.Email == "test@test.com"));
        }
    }
}
