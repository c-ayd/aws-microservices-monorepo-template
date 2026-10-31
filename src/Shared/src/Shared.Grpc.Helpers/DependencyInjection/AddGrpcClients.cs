using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;
using Shared.AspNetCore.Helpers.Kestrel.Utilities;
using Shared.Grpc.Helpers.Interceptors;

namespace Shared.Grpc.Helpers.DependencyInjection
{
    public static partial class DependencyInjection
    {
        public static void AddGrpcClients(this IServiceCollection services,
            Dictionary<string, Type> urlsAndClients,
            string? caBundleValue)
        {
            services.AddHttpContextAccessor();
            services.AddSingleton<GrpcClientAuthInterceptor>();

            var addGrpcClientMethodInfo = typeof(GrpcClientServiceExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)!
                .Where(m => m.Name == nameof(GrpcClientServiceExtensions.AddGrpcClient) &&
                    m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 1)
                .FirstOrDefault(m =>
                {
                    var p = m.GetParameters();
                    return p.Length == 2
                        && p[0].ParameterType == typeof(IServiceCollection)
                        && p[1].ParameterType == typeof(Action<GrpcClientFactoryOptions>);
                })!;

            X509Certificate2Collection? caBundle = null;
            if (caBundleValue != null)
            {
                caBundle = TlsUtility.LoadCertificateChain(caBundleValue);
            }

            foreach (var urlAndClient in urlsAndClients)
            {
                var addGrpcClientMethod = addGrpcClientMethodInfo.MakeGenericMethod([urlAndClient.Value]);
                ((IHttpClientBuilder)addGrpcClientMethod.Invoke(null, [services, new Action<GrpcClientFactoryOptions>(options =>
                {
                    options.Address = new Uri(urlAndClient.Key);
                })])!)
                    .AddInterceptor<GrpcClientAuthInterceptor>()
                    .ConfigurePrimaryHttpMessageHandler(() =>
                    {
                        var handler = new SocketsHttpHandler()
                        {
                            EnableMultipleHttp2Connections = true,
                            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
                            KeepAlivePingDelay = TimeSpan.FromSeconds(20),
                            KeepAlivePingTimeout = TimeSpan.FromSeconds(10)
                        };

                        if (caBundle != null)
                        {
                            handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                            {
                                return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                            };
                        }

                        return handler;
                    });
            }
        }
    }
}
