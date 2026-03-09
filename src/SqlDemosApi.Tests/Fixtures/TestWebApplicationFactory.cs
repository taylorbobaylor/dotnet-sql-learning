using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            // Remove real registrations that touch the database
            var typesToRemove = new[]
            {
                typeof(IDbConnectionFactory),
                typeof(IProcTimer),
                typeof(IBenchmarkService),
            };

            foreach (var type in typesToRemove)
            {
                var descriptor = services.FirstOrDefault(d => d.ServiceType == type);
                if (descriptor is not null) services.Remove(descriptor);
            }

            services.AddSingleton(Substitute.For<IDbConnectionFactory>());
            services.AddSingleton(Substitute.For<IProcTimer>());
            services.AddSingleton(MockBenchmarkService);
        });
    }
}
