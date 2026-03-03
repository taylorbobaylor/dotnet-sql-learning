# Dapper

Dapper is a micro-ORM that extends `IDbConnection`. It maps SQL query results to C# objects with almost no overhead. It's the right choice when you need raw SQL performance, complex stored procedure calls, or queries EF Core doesn't handle well.

---

## Setup

```bash
dotnet add package Dapper
dotnet add package Microsoft.Data.SqlClient
```

```csharp
// Register the connection (typically use IDbConnectionFactory pattern)
builder.Services.AddTransient<IDbConnection>(_ =>
    new SqlConnection(configuration.GetConnectionString("DefaultConnection")));
```

---

## Basic Query Patterns

```csharp
using Dapper;
using Microsoft.Data.SqlClient;

public class OrderRepository
{
    private readonly string _connectionString;

    public OrderRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection");
    }

    // Query multiple rows
    public async Task<IEnumerable<Order>> GetOrdersByCustomerAsync(int customerId)
    {
        const string sql = @"
            SELECT OrderID, CustomerID, OrderDate, TotalAmount, Status
            FROM Orders
            WHERE CustomerID = @CustomerID";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QueryAsync<Order>(sql, new { CustomerID = customerId });
    }

    // Query single row
    public async Task<Order?> GetOrderByIdAsync(int orderId)
    {
        const string sql = "SELECT * FROM Orders WHERE OrderID = @OrderID";

        using var connection = new SqlConnection(_connectionString);
        return await connection.QuerySingleOrDefaultAsync<Order>(sql, new { OrderID = orderId });
    }

    // Execute (INSERT / UPDATE / DELETE)
    public async Task<int> UpdateOrderStatusAsync(int orderId, string status)
    {
        const string sql = "UPDATE Orders SET Status = @Status WHERE OrderID = @OrderID";

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteAsync(sql, new { OrderID = orderId, Status = status });
    }

    // Query scalar value
    public async Task<int> GetOrderCountAsync(int customerId)
    {
        const string sql = "SELECT COUNT(*) FROM Orders WHERE CustomerID = @CustomerID";

        using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<int>(sql, new { CustomerID = customerId });
    }
}
```

---

## Dapper Methods Reference

| Method | Use For |
|---|---|
| `QueryAsync<T>` | SELECT returning multiple rows |
| `QuerySingleAsync<T>` | SELECT returning exactly 1 row (throws if 0 or >1) |
| `QuerySingleOrDefaultAsync<T>` | SELECT returning 0 or 1 row |
| `QueryFirstAsync<T>` | SELECT first row (throws if empty) |
| `QueryFirstOrDefaultAsync<T>` | SELECT first row or null |
| `ExecuteAsync` | INSERT / UPDATE / DELETE — returns rows affected |
| `ExecuteScalarAsync<T>` | SELECT single value (COUNT, MAX, etc.) |
| `QueryMultipleAsync` | Multiple result sets in one round-trip |

---

## Multi-Mapping (JOIN results to multiple objects)

```csharp
const string sql = @"
    SELECT o.OrderID, o.OrderDate, o.TotalAmount,
           c.CustomerID, c.Name, c.Email
    FROM Orders o
    INNER JOIN Customers c ON o.CustomerID = c.CustomerID
    WHERE o.CustomerID = @CustomerID";

using var connection = new SqlConnection(_connectionString);
var orders = await connection.QueryAsync<Order, Customer, Order>(
    sql,
    (order, customer) =>
    {
        order.Customer = customer;  // Attach the customer to the order
        return order;
    },
    new { CustomerID = customerId },
    splitOn: "CustomerID"  // Column where Dapper splits the result into two objects
);
```

---

## Multiple Result Sets

```csharp
const string sql = @"
    SELECT * FROM Orders WHERE CustomerID = @ID;
    SELECT COUNT(*) FROM Orders WHERE CustomerID = @ID;";

using var connection = new SqlConnection(_connectionString);
using var multi = await connection.QueryMultipleAsync(sql, new { ID = customerId });

var orders = await multi.ReadAsync<Order>();
var count  = await multi.ReadSingleAsync<int>();
```

---

## Dapper with Transactions

```csharp
using var connection = new SqlConnection(_connectionString);
await connection.OpenAsync();

using var transaction = connection.BeginTransaction();
try
{
    await connection.ExecuteAsync(
        "INSERT INTO Orders (CustomerID, OrderDate) VALUES (@CustID, @Date)",
        new { CustID = customerId, Date = DateTime.UtcNow },
        transaction: transaction);

    await connection.ExecuteAsync(
        "UPDATE Customers SET LastOrderDate = @Date WHERE CustomerID = @CustID",
        new { CustID = customerId, Date = DateTime.UtcNow },
        transaction: transaction);

    transaction.Commit();
}
catch
{
    transaction.Rollback();
    throw;
}
```

---

## Dynamic Parameters

Use `DynamicParameters` for output parameters or when you need to build parameter lists dynamically:

```csharp
var parameters = new DynamicParameters();
parameters.Add("@CustomerID",  customerId);
parameters.Add("@OrderCount",  dbType: DbType.Int32, direction: ParameterDirection.Output);

using var connection = new SqlConnection(_connectionString);
await connection.ExecuteAsync("dbo.GetOrderCount", parameters, commandType: CommandType.StoredProcedure);

var count = parameters.Get<int>("@OrderCount");
```

---

## Dapper vs EF Core Trade-offs

**Choose Dapper when:**
- Calling stored procedures is the primary data access pattern
- You need maximum query performance and control
- Working with complex reports or read-heavy data access
- The team is comfortable writing SQL
- You need to map multiple result sets cleanly

**Choose EF Core when:**
- You want LINQ for queries and don't want to write SQL
- You need migrations and schema management
- Your app is CRUD-heavy with simple queries
- Change tracking and the unit-of-work pattern adds value

**Use both:** Many real applications use EF Core for standard CRUD and Dapper (or `FromSqlRaw`) for complex reads and stored procedure calls.
