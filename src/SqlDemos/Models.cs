namespace SqlDemos;

// Order, Customer, and Employee records were removed — all Dapper queries in this
// app use QueryAsync<dynamic> for row-counting only; typed mapping is unused.

public record BenchmarkResult(
    string ScenarioName,
    string SprocName,
    long ElapsedMs,
    int RowCount,
    bool IsBad);
