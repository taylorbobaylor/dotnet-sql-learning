using System.Net;
using System.Text.Json;

using SqlDemosApi.Tests.Fixtures;

namespace SqlDemosApi.Tests.Integration;

public class RunAllScenariosEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _client;

    public RunAllScenariosEndpointTests(TestWebApplicationFactory factory)
    {
        factory.MockBenchmarkService.RunAllAsync(Arg.Any<CancellationToken>())
            .Returns(new AllScenariosResult(
                1500,
                Enumerable.Range(1, 6).Select(id => new ScenarioResult(
                    id,
                    $"Scenario {id}",
                    "Bad pattern",
                    "Fix",
                    [new ProcRun("dbo.usp_Bad", "Bad", 500, 100, true),
                     new ProcRun("dbo.usp_Fixed", "Fixed", 50, 100, false)],
                    10.0,
                    DateTimeOffset.UtcNow)).ToList(),
                DateTimeOffset.UtcNow));

        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RunAllScenarios_ReturnsOk() =>
        (await _client.GetAsync("/scenarios/all", TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact]
    public async Task RunAllScenarios_ReturnsAllSixScenarios()
    {
        var response = await _client.GetAsync("/scenarios/all", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<AllScenariosResult>(json, JsonOptions);

        result.Should().NotBeNull();
        result.Scenarios.Should().HaveCount(6);
    }
}
