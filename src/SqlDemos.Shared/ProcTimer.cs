using System.Data;
using System.Diagnostics;
using Dapper;

namespace SqlDemos.Shared;

/// <summary>
/// Shared stored-procedure timing helper used by both the console app and the API.
/// Opens a connection, executes the procedure, and returns elapsed time + row count.
/// </summary>
public static class ProcTimer
{
    public const int DefaultCommandTimeoutSeconds = 120;

    public static async Task<(long ElapsedMs, int RowCount)> TimeProcAsync(
        IDbConnectionFactory connectionFactory,
        string procName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var stopwatch = Stopwatch.StartNew();

        var rows = (await connection.QueryAsync<dynamic>(
            new CommandDefinition(
                procName,
                parameters,
                commandType: CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken))).AsList();

        stopwatch.Stop();
        return (stopwatch.ElapsedMilliseconds, rows.Count);
    }
}
