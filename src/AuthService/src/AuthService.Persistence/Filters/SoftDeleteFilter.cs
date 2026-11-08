using System.Linq.Expressions;
using AuthService.Domain.SeedWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Persistence.Filters
{
    public static partial class Filters
    {
        public static void ApplySoftDeleteFilter(this ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var type = entityType.ClrType;
                if (!type.IsAssignableTo(typeof(ISoftDelete)))
                    continue;

                var entityMethod = typeof(ModelBuilder).GetMethods()
                    .Where(m => m.Name == nameof(ModelBuilder.Entity) &&
                        m.IsGenericMethod &&
                        m.GetParameters().Length == 0)
                    .First()
                    .MakeGenericMethod(type);

                var entityMethodReturn = entityMethod.Invoke(modelBuilder, null)!;
                var hasQueryFilterMethod = entityMethodReturn.GetType().GetMethods()
                    .Where(m => m.Name == nameof(EntityTypeBuilder.HasQueryFilter) &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(LambdaExpression))
                    .First();

                var parameter = Expression.Parameter(type, "e");
                var property = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
                var condition = Expression.Equal(property, Expression.Constant(false));
                var lambdaFunc = Expression.Lambda(condition, parameter);

                hasQueryFilterMethod.Invoke(entityMethodReturn, [lambdaFunc]);
            }
        }
    }
}
