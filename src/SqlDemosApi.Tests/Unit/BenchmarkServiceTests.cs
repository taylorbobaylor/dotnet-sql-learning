using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute.ExceptionExtensions;

namespace SqlDemosApi.Tests.Unit;

public class BenchmarkServiceTests
{
    private readonly IDbConnectionFactory _connectionFactory = Substitute.For<IDbConnectionFactory>();
    private readonly IProcTimer _procTimer = Substitute.For<IProcTimer>();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 3, 9, 12, 0, 0, TimeSpan.Zero));
    private readonly BenchmarkService _sut;

    public BenchmarkServiceTests()
    {
        _procTimer.TimeProcAsync(
                Arg.Any<IDbConnectionFactory>(),
                Arg.Any<string>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns((100L, 50));

        _sut = new BenchmarkService(
            _connectionFactory,
            _procTimer,
            _timeProvider,
            ScenarioCatalog.Build(_timeProvider.GetUtcNow().Year - 1),
            NullLogger<BenchmarkService>.Instance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(100)]
    public async Task RunScenarioAsync_InvalidId_ThrowsArgumentOutOfRange(int id) =>
        await FluentActions.Invoking(() => _sut.RunScenarioAsync(id, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public async Task RunScenarioAsync_ValidId_ReturnsScenarioResult(int id)
    {
        var result = await _sut.RunScenarioAsync(id, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Id.Should().Be(id);
        result.Runs.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunScenarioAsync_CallsProcTimerForEachRun()
    {
        await _sut.RunScenarioAsync(1, TestContext.Current.CancellationToken);

        await _procTimer.Received(2).TimeProcAsync(
            Arg.Any<IDbConnectionFactory>(),
            Arg.Any<string>(),
            Arg.Any<object?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunScenarioAsync_SkipsWarmupRunsInResult()
    {
        var result = await _sut.RunScenarioAsync(2, TestContext.Current.CancellationToken);

        result.Runs.Should().NotContain(r => r.Label.Contains("Warmup", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RunScenarioAsync_WarmupFailure_ContinuesExecution()
    {
        var callCount = 0;
        _procTimer.TimeProcAsync(
                Arg.Any<IDbConnectionFactory>(),
                Arg.Any<string>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1) throw new InvalidOperationException("Warmup failed");
                return (100L, 50);
            });

        var result = await _sut.RunScenarioAsync(2, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Runs.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunScenarioAsync_ImprovementFactor_CalculatedCorrectly()
    {
        var callCount = 0;
        _procTimer.TimeProcAsync(
                Arg.Any<IDbConnectionFactory>(),
                Arg.Any<string>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return callCount == 1 ? (1000L, 50) : (100L, 50);
            });

        var result = await _sut.RunScenarioAsync(1, TestContext.Current.CancellationToken);

        result.ImprovementFactor.Should().Be(10.0);
    }

    [Fact]
    public async Task RunAllAsync_ReturnsAllSixScenarios() =>
        (await _sut.RunAllAsync(TestContext.Current.CancellationToken)).Scenarios.Should().HaveCount(6);

    [Fact]
    public async Task RunAllAsync_ScenariosOrderedById() =>
        (await _sut.RunAllAsync(TestContext.Current.CancellationToken)).Scenarios.Select(s => s.Id).Should().BeInAscendingOrder();

    [Fact]
    public async Task RunAllAsync_ReturnsTotalElapsedMs() =>
        (await _sut.RunAllAsync(TestContext.Current.CancellationToken)).TotalElapsedMs.Should().BeGreaterThanOrEqualTo(0);

    [Fact]
    public async Task RunAllAsync_CancellationToken_Propagated()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _procTimer.TimeProcAsync(
                Arg.Any<IDbConnectionFactory>(),
                Arg.Any<string>(),
                Arg.Any<object?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        await FluentActions.Invoking(() => _sut.RunAllAsync(cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }
}
