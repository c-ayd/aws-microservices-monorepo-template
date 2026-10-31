using Grpc.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Shared.Grpc.Helpers.DependencyInjection;
using Shared.Http.Authentication;
using Shared.Http.Authentication.Constants;
using Shared.Http.Response.Middlewares;
using Shared.Test.Helpers.Fixtures;

namespace Shared.Test.Integration.Grpc.Helpers.Collections
{
    [CollectionDefinition(nameof(GrpcCollection))]
    public class GrpcCollection : ICollectionFixture<GrpcCollectionCluster>
    {
    }

    public class GrpcCollectionCluster : IAsyncLifetime
    {
        public const string CABundlePath = "Certs/test-ca-bundle.crt";

        private const string _webApiServerCertificatePath = "Certs/WebApi/test-webapi-server.crt";
        private const string _webApiServerPrivateKeyPath = "Certs/WebApi/test-webapi-server.key";

        public WebApiFixture GrpcServerFixture { get; private set; }
        public WebApiFixture GrpcClientFixture { get; private set; }

        public GrpcCollectionCluster()
        {
            GrpcServerFixture = new WebApiFixture(
                sslEnabled: true,
                caBundlePath: CABundlePath,
                serverCertificatePath: _webApiServerCertificatePath,
                serverCertificatePrivateKeyPath: _webApiServerPrivateKeyPath);
            GrpcClientFixture = new WebApiFixture(
                sslEnabled: true,
                caBundlePath: CABundlePath,
                serverCertificatePath: _webApiServerCertificatePath,
                serverCertificatePrivateKeyPath: _webApiServerPrivateKeyPath);
        }

        public async Task InitializeAsync()
        {
            await GrpcServerFixture.InitializeAsync(
                overrideConfiguration: null,
                configureServices: (services, config) =>
                {
                    services.AddGrpcServer(false);

                    services.AddAuthentication(ApiGatewayAuthKeys.AuthenticationScheme)
                        .AddScheme<AuthenticationSchemeOptions, ApiGatewayAuthHandler>(ApiGatewayAuthKeys.AuthenticationScheme, options => { });
                    services.AddAuthorization(optios =>
                    {
                        optios.AddPolicy("Admin", policy =>
                            policy.RequireRole("Admin"));
                    });
                },
                configureApp: app =>
                {
                    app.Use(async (context, next) =>
                    {
                        try
                        {
                            await next(context);
                        }
                        catch (Exception exception)
                        {
                            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                            await context.Response.WriteAsync(exception.Message);
                        }
                    });

                    app.UseMiddleware<AuthErrorResponseMiddleware>();
                    app.UseAuthentication();
                    app.UseAuthorization();
                },
                configureEndpoints: endpoints =>
                {
                    endpoints.MapGrpcService<TestGrpcServiceImplementation>();
                });

            var serverPort = GrpcServerFixture.GetPort(WebApiFixture.HttpsPortConfigName);

            await GrpcClientFixture.InitializeAsync(
                overrideConfiguration: null,
                configureServices: (services, config) =>
                {
                    services.AddGrpcClients(
                        urlsAndClients: new Dictionary<string, Type>()
                        {
                            { $"https://localhost:{serverPort}", typeof(TestGrpcService.TestGrpcServiceClient) }
                        },
                        caBundleValue: File.ReadAllText(Path.Combine(AppContext.BaseDirectory, CABundlePath)));

                    services.AddAuthentication(ApiGatewayAuthKeys.AuthenticationScheme)
                        .AddScheme<AuthenticationSchemeOptions, ApiGatewayAuthHandler>(ApiGatewayAuthKeys.AuthenticationScheme, options => { });
                    services.AddAuthorization();
                },
                configureApp: app =>
                {
                    app.Use(async (context, next) =>
                    {
                        try
                        {
                            await next(context);
                        }
                        catch (RpcException rpcException)
                        {
                            switch (rpcException.StatusCode)
                            {
                                case StatusCode.Unauthenticated:
                                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                                    break;
                                case StatusCode.PermissionDenied:
                                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                                    break;
                                default:
                                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                                    break;
                            }
                        }
                        catch (Exception exception)
                        {
                            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                            await context.Response.WriteAsync(exception.Message);
                        }
                    });

                    app.UseAuthentication();
                    app.UseAuthorization();
                },
                configureEndpoints: endpoints =>
                {
                    // Authentication and authorization middleware is not used here to test
                    // the authentication on the GRPC server.
                    endpoints.MapGet("/anonymous", async (context) =>
                    {
                        await using var scope = context.RequestServices.CreateAsyncScope();

                        var client = scope.ServiceProvider.GetRequiredService<TestGrpcService.TestGrpcServiceClient>();
                        var response = await client.AnonymousAsync(new TestGrpcRequest());

                        await Results.Ok(response).ExecuteAsync(context);
                    });
                    endpoints.MapGet("/authenticated", async (context) =>
                    {
                        await using var scope = context.RequestServices.CreateAsyncScope();

                        var client = scope.ServiceProvider.GetRequiredService<TestGrpcService.TestGrpcServiceClient>();
                        var response = await client.AuthenticatedAsync(new TestGrpcRequest());

                        await Results.Ok(response).ExecuteAsync(context);
                    });
                    endpoints.MapGet("/admin", async (context) =>
                    {
                        await using var scope = context.RequestServices.CreateAsyncScope();

                        var client = scope.ServiceProvider.GetRequiredService<TestGrpcService.TestGrpcServiceClient>();
                        var response = await client.AdminAsync(new TestGrpcRequest());

                        await Results.Ok(response).ExecuteAsync(context);
                    });
                    endpoints.MapGet("/exception", async (context) =>
                    {
                        await using var scope = context.RequestServices.CreateAsyncScope();

                        var client = scope.ServiceProvider.GetRequiredService<TestGrpcService.TestGrpcServiceClient>();
                        var response = await client.ExceptionAsync(new TestGrpcRequest());

                        await Results.Ok(response).ExecuteAsync(context);
                    });
                });
        }

        public async Task DisposeAsync()
        {
            await Task.WhenAll(
                GrpcServerFixture.DisposeWebApiAsync(),
                GrpcClientFixture.DisposeWebApiAsync()
            );
        }

        public class TestGrpcServiceImplementation : TestGrpcService.TestGrpcServiceBase
        {
            public override Task<TestGrpcResponse> Anonymous(TestGrpcRequest request, ServerCallContext context)
            {
                return Task.FromResult(new TestGrpcResponse()
                {
                    IntValue = 5
                });
            }

            [Authorize]
            public override Task<TestGrpcResponse> Authenticated(TestGrpcRequest request, ServerCallContext context)
            {
                return Task.FromResult(new TestGrpcResponse()
                {
                    StrValue = "Authenticated",
                    IntValue = 10
                });
            }

            [Authorize(Policy = "Admin")]
            public override Task<TestGrpcResponse> Admin(TestGrpcRequest request, ServerCallContext context)
            {
                return Task.FromResult(new TestGrpcResponse()
                {
                    StrValue = "Admin",
                    IntValue = 15
                });
            }

            public override Task<TestGrpcResponse> Exception(TestGrpcRequest request, ServerCallContext context)
            {
                throw new Exception("Test exception");
            }
        }
    }
}
