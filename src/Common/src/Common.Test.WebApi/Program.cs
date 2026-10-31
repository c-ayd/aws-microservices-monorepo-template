// This Web API is used as an entrypoint for test cases that require TLS
// but don't have any web project for WebAppFactoryFixture.

// Since this project is referenced by Shared.Test.Helpers, it creates a conflict with other
// test cases that have their own Web APIs. To prevent this conflict, this Web API does not
// use top-level statement and Program.cs is surrounded with a namespace and is used in
// WebApiFixture with the namespace.
namespace Common.Test.WebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var app = builder.Build();

            app.Run();
        }
    }
}
