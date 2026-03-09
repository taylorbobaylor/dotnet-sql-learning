namespace SqlDemos.Shared;

/// <summary>
/// Abstracts stored-procedure timing so consumers can be unit-tested
/// without hitting a real database.
/// </summary>
public interface IProcTimer
{
    Task<(long ElapsedMs, int RowCount)> TimeProcAsync(
        IDbConnectionFactory connectionFactory,
        string procName,
        object? parameters = null,
        CancellationToken cancellationToken = default);
}
