using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.AspNetCore.Helpers.Options;
using Shared.PostgreSql.Helpers.EntityFramework.Extensions;
using Shared.PostgreSql.Helpers.Options.Extensions;

namespace Initializer.Databases
{
    public static class Utility
    {
        public static string? GetArgsValue(string[] args, IEnumerable<string> keys)
        {
            for (int i = 0; i < args.Length; ++i)
            {
                if (keys.Contains(args[i]))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        public static T? DeserializeOptions<T>(string jsonString)
            where T : class, IOptions
        {
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var key = (string)typeof(T).GetProperty(nameof(IOptions.Key), BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!.GetValue(null)!;
            if (!jsonDoc.RootElement.TryGetProperty(key, out var jsonElement))
                return null;

            return jsonElement.Deserialize<T>();
        }

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
