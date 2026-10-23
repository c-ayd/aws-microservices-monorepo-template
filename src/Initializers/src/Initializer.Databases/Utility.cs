using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.AspNetCore.Helpers.Options;
using Shared.PostgreSql.Helpers.EntityFramework.Extensions;
using Shared.PostgreSql.Helpers.Options.Extensions;

namespace Initializer.Db
{
    public static class Utility
    {
        public static T CreateDbContext<T>(PostgreSqlConnectionOptions connectionOptions, string? caBundleValue)
        {
            var dbContextType = typeof(T);
            var ctor = dbContextType.GetConstructor([
                typeof(DbContextOptions<>).MakeGenericType(dbContextType)
            ])!;

            var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(typeof(DbContextOptionsBuilder<>).MakeGenericType(dbContextType))!;
            optionsBuilder.ConfigureDbContext(connectionOptions, caBundleValue);

            return (T)ctor.Invoke([optionsBuilder.Options]);
        }
    }
}
