using System.Reflection;
using Common.Http.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Http.DependencyInjection
{
    public static partial class DependencyInjection
    {
        public static void AddValidatorsFromAssembly(this IServiceCollection services, Assembly assembly)
        {
            var validators = new List<(Type serviceType, Type implementationType)>();
            var asyncValidators = new List<(Type serviceType, Type implementationType)>();

            FindValidators(assembly, validators, asyncValidators);

            RegisterValidators(services, validators, asyncValidators);
        }

        public static void AddValidatorsFromAssemblies(this IServiceCollection services, params Assembly[] assemblies)
        {
            var validators = new List<(Type serviceType, Type implementationType)>();
            var asyncValidators = new List<(Type serviceType, Type implementationType)>();

            foreach (var assembly in assemblies)
            {
                FindValidators(assembly, validators, asyncValidators);
            }

            RegisterValidators(services, validators, asyncValidators);
        }

        private static void FindValidators(
            Assembly assembly,
            List<(Type serviceType, Type implementationType)> validators,
            List<(Type serviceType, Type implementationType)> asyncValidators)
        {
            validators.AddRange(assembly.GetTypes()
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == typeof(IValidator<>).GetGenericTypeDefinition())
                    .Select(i => (
                        serviceType: typeof(IValidator<>).MakeGenericType(i.GenericTypeArguments[0]),
                        implementationType: t
                    ))));

            asyncValidators.AddRange(assembly.GetTypes()
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType &&
                           i.GetGenericTypeDefinition() == typeof(IAsyncValidator<>).GetGenericTypeDefinition())
                    .Select(i => (
                        serviceType: typeof(IAsyncValidator<>).MakeGenericType(i.GenericTypeArguments[0]),
                        implementationType: t
                    ))));
        }

        private static void RegisterValidators(
            IServiceCollection services,
            List<(Type serviceType, Type implementationType)> validators,
            List<(Type serviceType, Type implementationType)> asyncValidators)
        {
            // services.AddScoped<IValidator<T>, Validator>();
            // services.AddScoped<IAsyncValidator<T>, AsyncValidator>();

            var addScopedMethod = typeof(ServiceCollectionServiceExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "AddScoped" &&
                                !m.IsGenericMethod &&
                                m.GetParameters().Length == 3)!;

            foreach (var validator in validators)
            {
                addScopedMethod.Invoke(services, [services, validator.serviceType, validator.implementationType]);
            }

            foreach (var asyncValidator in asyncValidators)
            {
                addScopedMethod.Invoke(services, [services, asyncValidator.serviceType, asyncValidator.implementationType]);
            }
        }
    }
}
