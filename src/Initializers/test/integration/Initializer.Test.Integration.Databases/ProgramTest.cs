using AuthService.Domain.Entities;
using AuthService.Persistence.DbContexts;
using Initializer.Databases.Test.Integration.Migrations.Collections;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;
using Shared.Test.Generators;

namespace Initializer.Databases.Test.Integration.Migrations
{
    [Collection(nameof(InitializerCollection))]
    public class ProgramTest
    {
        private readonly InitializerCollectionCluster _collectionCluster;

        public ProgramTest(InitializerCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task InitializeAsync_WhenThereIsNoDataInDb_ShouldAddSeedDataToDb()
        {
            // Arrange
            await _collectionCluster.PostgreSqlFixture.DropDatabaseAsync<AuthDbContext>();

            // Act
            await Program.Main([
                "--test",
                "--aws-url", $"http://localhost:{_collectionCluster.AwsFixture.GetPort()}"
            ]);

            // Assert
            await using var dbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();
            var rolesFromDb = await dbContext.Roles.ToListAsync();
            var accountsFromDb = await dbContext.Accounts.Include(a => a.Roles).ToListAsync();

            Assert.Single(rolesFromDb);
            Assert.Single(accountsFromDb);

            Assert.Equal("Admin", rolesFromDb[0].Name);

            Assert.Equal("test@test.com", accountsFromDb[0].Email);
            Assert.Equal("Admin", accountsFromDb[0].Roles.ElementAt(0).Name);
            Assert.Equal(SupportedLanguages.DefaultLanguage, accountsFromDb[0].PreferredLanguage);
        }

        [Fact]
        public async Task InitializeAsync_WhenThereIsDataInDb_ShouldAddUniqueOnes()
        {
            // Arrange
            await _collectionCluster.PostgreSqlFixture.ClearDatabaseAsync<AuthDbContext>();

            await using var dbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();
            await dbContext.Roles.AddAsync(new Role(StringGenerator.GenerateAlphanumeric()));
            await dbContext.Accounts.AddAsync(new Account(EmailGenerator.Generate(), PasswordGenerator.Generate(), "abc"));
            await dbContext.SaveChangesAsync();

            // Act
            await Program.Main([
                "--test",
                "--aws-url", $"http://localhost:{_collectionCluster.AwsFixture.GetPort()}"
            ]);

            // Assert
            dbContext.ChangeTracker.Clear();
            var rolesFromDb = await dbContext.Roles.ToListAsync();
            var accountsFromDb = await dbContext.Accounts.Include(a => a.Roles).ToListAsync();

            Assert.NotNull(rolesFromDb.FirstOrDefault(r => r.Name == "Admin"));
            Assert.NotNull(accountsFromDb.FirstOrDefault(a => a.Email == "test@test.com"));
        }
    }
}
