using AuthService.Domain.Entities;
using AuthService.Persistence.DbContexts;
using AuthService.Test.Integration.Persistence.Collections;
using Microsoft.EntityFrameworkCore;
using Common.Constants;
using Common.Test.Generators;

namespace AuthService.Test.Integration.Persistence.Filters
{
    [Collection(nameof(AuthDbContextCollection))]
    public class SoftDeleteFilterTest
    {
        private readonly AuthDbContextCollectionCluster _collectionCluster;

        public SoftDeleteFilterTest(AuthDbContextCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task SoftDeleteFilter_WhenEntityIsSoftDeleteableAndIsDeleted_ShouldNotAppearInResult()
        {
            // Arrange
            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            var account = new Account(
                EmailGenerator.Generate(),
                PasswordGenerator.Generate(),
                SupportedLanguages.DefaultLanguage);
            var accountId = account.Id;

            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            // Act
            authDbContext.Accounts.Remove(account);
            await authDbContext.SaveChangesAsync();

            // Assert
            authDbContext.ChangeTracker.Clear();
            var accountFromDb = await authDbContext.Accounts.FindAsync(accountId);
            var deletedAccount = await authDbContext.Accounts
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(a => a.Id == accountId);

            Assert.NotNull(deletedAccount);
            Assert.Null(accountFromDb);
        }

        [Fact]
        public async Task SoftDeleteFilter_WhenEntityIsSoftDeleteableAndIsNotDeleted_ShouldAppearInResult()
        {
            // Arrange
            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            var account = new Account(
                EmailGenerator.Generate(),
                PasswordGenerator.Generate(),
                SupportedLanguages.DefaultLanguage);
            var accountId = account.Id;

            // Act
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            // Assert
            authDbContext.ChangeTracker.Clear();
            var accountFromDb = await authDbContext.Accounts.FindAsync(accountId);

            Assert.NotNull(accountFromDb);
        }

        [Fact]
        public async Task SoftDeleteFilter_WhenEntityIsNotSoftDeleteableAndIsNotDeleted_ShouldAppearInResult()
        {
            // Arrange
            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            var account = new Account(
                EmailGenerator.Generate(),
                PasswordGenerator.Generate(),
                SupportedLanguages.DefaultLanguage);
            var session = new Session(
                account.Id,
                refreshTokenHashed: StringGenerator.GeneratePrintableAscii(),
                expirationDate: DateTimeOffset.UtcNow);
            var sessionId = session.Id;

            // Act
            account.Sessions.Add(session);
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            // Assert
            authDbContext.ChangeTracker.Clear();
            var sessionFromDb = await authDbContext.Sessions.FindAsync(sessionId);

            Assert.NotNull(sessionFromDb);
        }
    }
}
