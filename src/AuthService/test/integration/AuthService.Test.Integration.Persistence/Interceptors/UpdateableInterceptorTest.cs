using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Domain.SeedWork;
using AuthService.Persistence.DbContexts;
using AuthService.Persistence.Exceptions;
using AuthService.Test.Integration.Persistence.Collections;
using Shared.Constants;
using Shared.Test.Generators;
using Shared.Test.Helpers.Fixtures;

namespace AuthService.Test.Integration.Persistence.Interceptors
{
    [Collection(nameof(AuthDbContextCollection))]
    public class UpdateableInterceptorTest
    {
        private readonly AuthDbContextCollectionCluster _collectionCluster;

        public UpdateableInterceptorTest(AuthDbContextCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task SavingChangesAsync_WhenEntityIsUpdateableAndIsUpdated_ShouldSetUpdatedDate()
        {
            // Arrange
            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            var now = DateTimeOffset.UtcNow;
            var passwordLength = 10;
            var account = new Account(
                EmailGenerator.Generate(),
                PasswordGenerator.Generate(length: passwordLength),
                SupportedLanguages.DefaultLanguage);
            var accountId = account.Id;

            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            if (account.UpdatedDate != null)
                Assert.Fail($"The {nameof(IUpdateable.UpdatedDate)} property was set while creating the entity in the database");

            // Act
            account.PasswordHashed = PasswordGenerator.Generate(length: passwordLength + 1);
            await authDbContext.SaveChangesAsync();

            // Assert
            authDbContext.ChangeTracker.Clear();
            var updatedAccount = await authDbContext.Accounts.FindAsync(accountId);

            Assert.NotNull(updatedAccount);
            Assert.NotNull(updatedAccount.UpdatedDate);
            Assert.True(DateTimeOffset.Compare(now, updatedAccount.UpdatedDate.Value) <= 0, $"The {nameof(IUpdateable.UpdatedDate)} property is in the past.");
        }

        [Fact]
        public async Task SavingChangesAsync_WhenEntityIsNotUpdateableAndIsUpdated_ShouldThrowException()
        {
            // Arrange
            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            var account = new Account(
                EmailGenerator.Generate(),
                PasswordGenerator.Generate(),
                SupportedLanguages.DefaultLanguage);
            var tokenLength = 10;
            var token = new Token(
                account.Id,
                ETokenPurpose.EmailVerification,
                valueHashed: StringGenerator.GeneratePrintableAscii(length: tokenLength),
                expirationDate: DateTimeOffset.UtcNow);

            account.Tokens.Add(token);
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            // Act
            token.ValueHashed = StringGenerator.GeneratePrintableAscii(length: tokenLength + 1);
            var exception = await Record.ExceptionAsync(async () =>
            {
                await authDbContext.SaveChangesAsync();
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<NonUpdateableEntityException>(exception);
        }
    }
}
