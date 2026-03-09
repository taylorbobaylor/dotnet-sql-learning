namespace SqlDemosApi.Tests.Unit;

public class ModelTests
{
    [Fact]
    public void ProcRun_Constructor_SetsAllProperties()
    {
        var run = new ProcRun("dbo.usp_Test", "Test Label", 150, 42, true);

        run.Procedure.Should().Be("dbo.usp_Test");
        run.Label.Should().Be("Test Label");
        run.ElapsedMs.Should().Be(150);
        run.RowCount.Should().Be(42);
        run.IsBad.Should().BeTrue();
    }

    [Fact]
    public void ScenarioResult_Constructor_SetsAllProperties()
    {
        var runs = new List<ProcRun> { new("proc", "label", 100, 10, false) };
        var ranAt = DateTimeOffset.UtcNow;
        var result = new ScenarioResult(1, "Test", "Bad pattern", "Fix it", runs, 5.0, ranAt);

        result.Id.Should().Be(1);
        result.Name.Should().Be("Test");
        result.Antipattern.Should().Be("Bad pattern");
        result.Fix.Should().Be("Fix it");
        result.Runs.Should().HaveCount(1);
        result.ImprovementFactor.Should().Be(5.0);
        result.RanAt.Should().Be(ranAt);
    }

    [Fact]
    public void AllScenariosResult_Constructor_SetsAllProperties()
    {
        var scenarios = new List<ScenarioResult>();
        var ranAt = DateTimeOffset.UtcNow;
        var result = new AllScenariosResult(1234, scenarios, ranAt);

        result.TotalElapsedMs.Should().Be(1234);
        result.Scenarios.Should().BeEmpty();
        result.RanAt.Should().Be(ranAt);
    }

    [Fact]
    public void ScenarioInfo_Constructor_SetsAllProperties()
    {
        var info = new ScenarioInfo(1, "Name", "Pattern", "Fix", "bad_proc", "fixed_proc");

        info.Id.Should().Be(1);
        info.Name.Should().Be("Name");
        info.Antipattern.Should().Be("Pattern");
        info.Fix.Should().Be("Fix");
        info.BadProcedure.Should().Be("bad_proc");
        info.FixedProcedure.Should().Be("fixed_proc");
    }
}
