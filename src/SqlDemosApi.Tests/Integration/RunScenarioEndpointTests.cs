using System.Net;
using System.Text.Json;

using SqlDemosApi.Tests.Fixtures;

namespace SqlDemosApi.Tests.Integration;

public class RunScenarioEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _client;

    public RunScenarioEndpointTests(TestWebApplicationFactory factory)
    {
        factory.MockBenchmarkService
            .RunScenarioAsync(Arg.Is<int>(id => id >= 1 && id <= 6), Arg.Any<CancellationToken>())
            .Returns(callInfo => CreateFakeResult(callInfo.ArgAt<int>(0)));

        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RunScenario_ValidId_ReturnsOk() =>
        (await _client.GetAsync("/scenarios/1", TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact]
    public async Task RunScenario_InvalidId_Zero_ReturnsBadRequest() =>
        (await _client.GetAsync("/scenarios/0", TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

    [Fact]
    public async Task RunScenario_InvalidId_Seven_ReturnsBadRequest() =>
        (await _client.GetAsync("/scenarios/7", TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

    [Fact]
    public async Task RunScenario_InvalidId_Negative_ReturnsBadRequest() =>
        (await _client.GetAsync("/scenarios/-1", TestContext.Current.CancellationToken))
            .StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.NotFound);

    [Fact]
    public async Task RunScenario_ReturnsExpectedScenarioResult()
    {
        var response = await _client.GetAsync("/scenarios/1", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var result = JsonSerializer.Deserialize<ScenarioResult>(json, JsonOptions);

        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.Name.Should().NotBeNullOrWhiteSpace();
    }

    private static ScenarioResult CreateFakeResult(int id) =>
        new(
            id,
            $"Scenario {id}",
            "Bad pattern",
            "Fix description",
            [new ProcRun("dbo.usp_Bad", "Bad", 500, 100, true),
             new ProcRun("dbo.usp_Fixed", "Fixed", 50, 100, false)],
            10.0,
            DateTimeOffset.UtcNow);
}
