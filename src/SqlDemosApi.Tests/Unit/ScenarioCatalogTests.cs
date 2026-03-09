namespace SqlDemosApi.Tests.Unit;

public class ScenarioCatalogTests
{
    private readonly ScenarioDefinition[] _catalog = ScenarioCatalog.Build(2025);

    [Fact]
    public void Build_Returns_Six_Scenarios() => 
        _catalog.Should().HaveCount(6);

    [Fact]
    public void Build_AllScenarios_HaveUniqueIds() =>
        _catalog.Select(s => s.Id).Should().OnlyHaveUniqueItems();

    [Fact]
    public void Build_AllScenarios_HaveNonEmptyNames() =>
        _catalog.Should().AllSatisfy(s => s.Name.Should().NotBeNullOrWhiteSpace());

    [Fact]
    public void Build_AllScenarios_HaveAtLeastTwoRuns() =>
        _catalog.Should().AllSatisfy(s => s.Runs.Should().HaveCountGreaterThanOrEqualTo(2));

    [Fact]
    public void Build_EachScenario_HasExactlyOneBadNonWarmupRun() =>
        _catalog.Should().AllSatisfy(s =>
            s.Runs.Count(r => r.IsBad && !r.IsWarmup).Should().Be(1));

    [Fact]
    public void Build_EachScenario_HasAtLeastOneFixedNonWarmupRun() =>
        _catalog.Should().AllSatisfy(s =>
            s.Runs.Count(r => !r.IsBad && !r.IsWarmup).Should().BeGreaterThanOrEqualTo(1));

    [Fact]
    public void Build_Scenario2_HasWarmupRun() =>
        _catalog.Single(s => s.Id == 2).Runs.Should().Contain(r => r.IsWarmup);

    [Fact]
    public void Build_Scenario3_UsesProvidedYear()
    {
        var catalog = ScenarioCatalog.Build(2024);
        var scenario3 = catalog.Single(s => s.Id == 3);
        var badRun = scenario3.Runs.First(r => r.IsBad);

        // Parameters is an anonymous object with Year property
        var yearProp = badRun.Parameters!.GetType().GetProperty("Year");
        yearProp.Should().NotBeNull();
        yearProp.GetValue(badRun.Parameters).Should().Be(2024);
    }

    [Fact]
    public void Build_Count_MatchesArrayLength() =>
        ScenarioCatalog.Count.Should().Be(_catalog.Length);
}
