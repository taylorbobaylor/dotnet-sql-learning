# Calling Stored Procedures from C#

Since the role involves stored procedures, being able to demonstrate multiple ways to call them — and knowing when to use each — is a key differentiator.

---

## Method 1: Dapper (Recommended for Stored Procs)

Dapper has first-class support for stored procedures. It's the cleanest approach.

### Simple SP Call — Map to POCO

```csharp
public async Task<IEnumerable<Order>> GetCustomerOrdersAsync(int customerId, DateTime? startDate = null)
{
    using var connection = new SqlConnection(_connectionString);

    return await connection.QueryAsync<Order>(
        "dbo.GetCustomerOrders",
        new { CustomerID = customerId, StartDate = startDate },
        commandType: CommandType.StoredProcedure  // ← This is the key flag
    );
}
```

### SP with Output Parameter

```csharp
public async Task<(IEnumerable<Order> Orders, int TotalCount)> GetPagedOrdersAsync(
    int customerId, int page, int pageSize)
{
    var p = new DynamicParameters();
    p.Add("@CustomerID", customerId);
    p.Add("@Page",       page);
    p.Add("@PageSize",   pageSize);
    p.Add("@TotalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

    using var connection = new SqlConnection(_connectionString);
    var orders = await connection.QueryAsync<Order>(
        "dbo.GetPagedOrders", p, commandType: CommandType.StoredProcedure);

    var totalCount = p.Get<int>("@TotalCount");
    return (orders, totalCount);
}
```

### SP with Multiple Result Sets

```csharp
public async Task<OrderDetails> GetOrderDetailsAsync(int orderId)
{
    using var connection = new SqlConnection(_connectionString);
    using var multi = await connection.QueryMultipleAsync(
        "dbo.GetOrderDetails",
        new { OrderID = orderId },
        commandType: CommandType.StoredProcedure);

    var order   = await multi.ReadSingleAsync<Order>();
    var items   = await multi.ReadAsync<OrderItem>();
    var history = await multi.ReadAsync<OrderHistory>();

    order.Items   = items.ToList();
    order.History = history.ToList();
    return order;
}
```

### SP that returns a scalar / return code

```csharp
public async Task<int> CreateOrderAsync(Order order)
{
    var p = new DynamicParameters();
    p.Add("@CustomerID",  order.CustomerID);
    p.Add("@TotalAmount", order.TotalAmount);
    p.Add("@NewOrderID",  dbType: DbType.Int32, direction: ParameterDirection.Output);

    using var connection = new SqlConnection(_connectionString);
    await connection.ExecuteAsync(
        "dbo.CreateOrder", p, commandType: CommandType.StoredProcedure);

    return p.Get<int>("@NewOrderID");
}
```

---

## Method 2: EF Core — `FromSqlRaw` / `FromSqlInterpolated`

EF Core can call stored procedures that return entities matching your `DbSet<T>` type.

### Simple SP Returning Entities

```csharp
// FromSqlRaw — use parameterized queries, never string-concatenate values
var orders = await _context.Orders
    .FromSqlRaw("EXEC dbo.GetCustomerOrders @CustomerID = {0}", customerId)
    .AsNoTracking()
    .ToListAsync();

// FromSqlInterpolated — cleaner syntax, auto-parameterized
var orders = await _context.Orders
    .FromSqlInterpolated($"EXEC dbo.GetCustomerOrders @CustomerID = {customerId}")
    .AsNoTracking()
    .ToListAsync();

// You can also chain LINQ after the SP call (limited — no WHERE on SP results in EF Core)
var activeOrders = await _context.Orders
    .FromSqlInterpolated($"EXEC dbo.GetCustomerOrders {customerId}")
    .Where(o => o.Status == "Active")  // This becomes a subquery in some versions
    .ToListAsync();
```

!!! warning "EF Core SP Limitation"
    `FromSqlRaw` can only return entity types that map to a `DbSet<T>`. For custom result shapes (DTOs, anonymous types), either use Dapper or use `_context.Database.SqlQuery<T>` (EF Core 7+).

### EF Core 7+ — `SqlQuery<T>` for Arbitrary Types

```csharp
// EF Core 7+ — can map to any type, not just DbSet<T>
var summaries = await _context.Database
    .SqlQuery<OrderSummary>($"EXEC dbo.GetOrderSummaries {customerId}")
    .ToListAsync();
```

### Execute Non-Query (INSERT / UPDATE / DELETE SPs)

```csharp
// Returns rows affected
int rowsAffected = await _context.Database
    .ExecuteSqlInterpolatedAsync($"EXEC dbo.UpdateOrderStatus {orderId}, {'Shipped'}");

// Or with named parameters
int rowsAffected = await _context.Database
    .ExecuteSqlRawAsync(
        "EXEC dbo.UpdateOrderStatus @OrderID = @p0, @Status = @p1",
        orderId, "Shipped");
```

---

## Method 3: Raw ADO.NET (Know It Exists)

You may not use this often, but interviewers appreciate knowing the foundational layer.

```csharp
public async Task<List<Order>> GetOrdersAdoAsync(int customerId)
{
    var orders = new List<Order>();

    using var connection = new SqlConnection(_connectionString);
    using var command    = new SqlCommand("dbo.GetCustomerOrders", connection);

    command.CommandType = CommandType.StoredProcedure;
    command.Parameters.AddWithValue("@CustomerID", customerId);

    await connection.OpenAsync();

    using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        orders.Add(new Order
        {
            OrderID    = reader.GetInt32(reader.GetOrdinal("OrderID")),
            OrderDate  = reader.GetDateTime(reader.GetOrdinal("OrderDate")),
            TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount"))
        });
    }

    return orders;
}
```

---

## Comparison Summary

| Approach | Best For | Complexity |
|---|---|---|
| **Dapper** | All stored proc patterns, multiple result sets, output params | Low |
| **EF Core `FromSqlInterpolated`** | SP returning entities matching an existing `DbSet<T>` | Medium |
| **EF Core `SqlQuery<T>` (v7+)** | SP returning any type | Medium |
| **EF Core `ExecuteSqlRaw`** | Non-query SPs (INSERT/UPDATE/DELETE) | Low |
| **ADO.NET** | Full control, no dependencies, legacy systems | High |

---

## Connection Management Best Practices

```csharp
// ✅ Always use 'using' — connection is returned to pool, not truly closed
using var connection = new SqlConnection(_connectionString);
// connection is returned to the pool at end of 'using' block

// ✅ In DI — inject IDbConnectionFactory for testability
public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync();
}

public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;
    public SqlConnectionFactory(IConfiguration config)
        => _connectionString = config.GetConnectionString("DefaultConnection");

    public async Task<IDbConnection> CreateConnectionAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}

// Registration
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
```

SQL Server uses **connection pooling** by default — `using` returns the connection to the pool rather than closing the actual TCP connection. Never hold connections open longer than needed.
