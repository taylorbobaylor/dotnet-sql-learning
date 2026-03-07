using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace SqlDemos.Shared;

/// <summary>
/// Production implementation of <see cref="IDbConnectionFactory"/> backed by SQL Server.
/// Substitute a test-double factory in unit tests to avoid a live database.
/// </summary>
public sealed class SqlConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public DbConnection CreateConnection() => new SqlConnection(connectionString);
}
