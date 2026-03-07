using System.Data.Common;

namespace SqlDemos.Shared;

/// <summary>
/// Abstracts SQL connection creation so consumers (BenchmarkService, console app) are
/// not bound to SqlConnection directly, making the data-access layer substitutable in tests.
/// </summary>
public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}
