using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SqlDemosApi.Tests.Fixtures;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public IBenchmarkService MockBenchmarkService { get; } = Substitute.For<IBenchmarkService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:InterviewDemo"] = "Server=fake;Database=fake;"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove all real registrations that touch the database (RemoveAll handles duplicates)
            services.RemoveAll<IDbConnectionFactory>();
            services.RemoveAll<IProcTimer>();
            services.RemoveAll<IBenchmarkService>();

            services.AddSingleton(Substitute.For<IDbConnectionFactory>());
            services.AddSingleton(Substitute.For<IProcTimer>());
            services.AddSingleton(MockBenchmarkService);
        });
    }
}
