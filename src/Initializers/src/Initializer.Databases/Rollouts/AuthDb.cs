using AuthService.Domain.Entities;
using AuthService.Persistence.DbContexts;
using Initializer.Databases.Exceptions;
using Initializer.Databases.Options;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;
using Shared.PostgreSql.Helpers.Options.Extensions;

namespace Initializer.Databases.Rollouts
{
    public static class AuthDb
    {
        public static async Task RolloutAsync(
            PostgreSqlConnectionOptions connectionOptions,
            CABundleOptions cABundleOptions,
            SeedDataOptions? seedDataOptions)
        {
            using var authDbContext = Utility.CreateDbContext<AuthDbContext>(connectionOptions, cABundleOptions.Value);

            // Migrate
            await authDbContext.Database.MigrateAsync();

            System.Console.WriteLine("Migration(s) completed.");

            // Seed data
            if (seedDataOptions != null)
            {
                await using var transaction = await authDbContext.Database.BeginTransactionAsync();
                try
                {
                    await SeedRolesAsync(authDbContext, seedDataOptions);
                    await SeedAccountsAsync(authDbContext, seedDataOptions);

                    await transaction.CommitAsync();

                    System.Console.WriteLine("Seed data is committed to the DB.");
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();

                    System.Console.WriteLine($"Something went wrong. Exiting without saving any seed data...");
                    throw;
                }
            }
        }

        private static async Task SeedRolesAsync(AuthDbContext authDbContext, SeedDataOptions seedDataOptions)
        {
            int counter = 0;
            foreach (var role in seedDataOptions.Roles)
            {
                if (await authDbContext.Roles.AnyAsync(r => r.Name == role))
                {
                    System.Console.WriteLine($"Role {role} is already in the DB. Skipping...");
                    continue;
                }

                await authDbContext.Roles.AddAsync(new Role(role));
                ++counter;
            }

            await authDbContext.SaveChangesAsync();

            System.Console.WriteLine($"{counter} role(s) are added to the DB.");
        }

        private static async Task SeedAccountsAsync(AuthDbContext authDbContext, SeedDataOptions seedDataOptions)
        {
            var counter = 0;
            foreach (var accountDetails in seedDataOptions.Accounts)
            {
                if (await authDbContext.Accounts.AnyAsync(a => a.Email == accountDetails.Email))
                {
                    System.Console.WriteLine($"Account with the email {accountDetails.Email} is already in the DB. Skipping...");
                    continue;
                }

                var newAccount = new Account(
                    accountDetails.Email,
                    "123",  // The hashed password is given as a random string, which means no password will match.
                            // The email owner must use the reset password feature to start using their account.
                    SupportedLanguages.DefaultLanguage);

                if (accountDetails.Role != null)
                {
                    var role = await authDbContext.Roles.FirstOrDefaultAsync(r => r.Name == accountDetails.Role);
                    if (role == null)
                        throw new RoleNotFoundException(accountDetails.Role);

                    newAccount.Roles.Add(role);
                }

                await authDbContext.Accounts.AddAsync(newAccount);
                ++counter;
            }

            await authDbContext.SaveChangesAsync();

            System.Console.WriteLine($"{counter} account(s) are added to the DB.");
        }

        public class SeedDataOptions
        {
            public required List<string> Roles { get; set; }
            public required List<AccountDetails> Accounts { get; set; }

            public class AccountDetails
            {
                public required string Email { get; set; }
                public string? Role { get; set; }
            }
        }
    }
}
