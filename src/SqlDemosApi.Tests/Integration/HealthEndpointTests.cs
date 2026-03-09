using System.Text.Json;

using SqlDemosApi.Tests.Fixtures;

namespace SqlDemosApi.Tests.Integration;

public class HealthEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetHealth_ReturnsOk() =>
        (await _client.GetAsync("/health", TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

    [Fact]
    public async Task GetHealth_ReturnsHealthyStatus()
    {
        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        JsonDocument.Parse(json).RootElement
            .GetProperty("status").GetString().Should().Be("healthy");
    }

    [Fact]
    public async Task GetHealth_ReturnsTimestamp()
    {
        var response = await _client.GetAsync("/health", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        JsonDocument.Parse(json).RootElement
            .TryGetProperty("timestamp", out _).Should().BeTrue();
    }
}
