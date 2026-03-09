using System.Net;
using System.Text.Json;

using SqlDemosApi.Tests.Fixtures;

namespace SqlDemosApi.Tests.Integration;

public class ScenariosListEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetScenarios_ReturnsOk() =>
        (await _client.GetAsync("/scenarios", TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact]
    public async Task GetScenarios_ReturnsSixScenarios() =>
        (await GetScenariosAsync()).Should().HaveCount(6);

    [Fact]
    public async Task GetScenarios_EachHasRequiredFields() =>
        (await GetScenariosAsync()).Should().AllSatisfy(s =>
        {
            s.Id.Should().BeGreaterThan(0);
            s.Name.Should().NotBeNullOrWhiteSpace();
            s.Antipattern.Should().NotBeNullOrWhiteSpace();
            s.Fix.Should().NotBeNullOrWhiteSpace();
            s.BadProcedure.Should().NotBeNullOrWhiteSpace();
            s.FixedProcedure.Should().NotBeNullOrWhiteSpace();
        });

    [Fact]
    public async Task GetScenarios_IdsAreOneThruSix() =>
        (await GetScenariosAsync()).Select(s => s.Id).Should().BeEquivalentTo([1, 2, 3, 4, 5, 6]);

    private async Task<ScenarioInfo[]> GetScenariosAsync()
    {
        var response = await _client.GetAsync("/scenarios", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return JsonSerializer.Deserialize<ScenarioInfo[]>(json, JsonOptions)!;
    }
}
