# Entity Framework Core

EF Core is Microsoft's official ORM for .NET. You almost certainly use it daily — this page focuses on the interview-relevant concepts and gotchas.

---

## Core Concepts

### DbContext

The `DbContext` is the unit of work and the entry point for EF Core. It manages entity tracking, database connections, and migrations.

```csharp
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order>    Orders    { get; set; }
    public DbSet<Customer> Customers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Fluent API configuration
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.OrderID);
            entity.Property(o => o.TotalAmount).HasPrecision(10, 2);
            entity.HasOne(o => o.Customer)
                  .WithMany(c => c.Orders)
                  .HasForeignKey(o => o.CustomerID);
        });
    }
}
```

### Change Tracking

EF Core tracks entities you load and detects changes when you call `SaveChanges()`.

```csharp
var order = await _context.Orders.FindAsync(id);  // Tracked
order.Status = "Shipped";                          // Change tracked
await _context.SaveChangesAsync();                 // Issues UPDATE automatically
```

**Disable tracking when you only need to read data:**

```csharp
// ✅ Much faster for read-only queries — no tracking overhead
var orders = await _context.Orders
    .AsNoTracking()
    .Where(o => o.Status == "Active")
    .ToListAsync();
```

---

## N+1 Query Problem (Critical Interview Topic)

This is one of the most commonly asked EF Core gotchas.

```csharp
// ❌ N+1 — 1 query for orders, then N queries for each customer
var orders = await _context.Orders.ToListAsync();
foreach (var order in orders)
{
    Console.WriteLine(order.Customer.Name);  // Lazy-loads customer for each order!
}
// Results in: 1 + N SQL queries. For 1000 orders = 1001 queries!

// ✅ Fix 1: Eager loading with Include
var orders = await _context.Orders
    .Include(o => o.Customer)                // Single JOIN query
    .ToListAsync();

// ✅ Fix 2: Explicit loading (when you need selective loading)
var orders = await _context.Orders.ToListAsync();
await _context.Entry(orders[0]).Reference(o => o.Customer).LoadAsync();

// ✅ Fix 3: Projection — only fetch what you need
var result = await _context.Orders
    .Select(o => new OrderSummary
    {
        OrderID      = o.OrderID,
        CustomerName = o.Customer.Name,  // EF Core translates to JOIN in SQL
        Total        = o.TotalAmount
    })
    .ToListAsync();
```

**How to detect N+1:** Use `AddDbContextOptions(o => o.LogTo(Console.WriteLine))` in development to see all SQL being generated.

---

## Migrations

```bash
# Add a migration
dotnet ef migrations add AddOrderStatusColumn

# Apply pending migrations to the database
dotnet ef database update

# Rollback to a specific migration
dotnet ef database update PreviousMigrationName

# Generate SQL script for a migration (for production deployment)
dotnet ef migrations script FromMigration ToMigration
```

### Applying Migrations at Startup

```csharp
// Program.cs
using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await db.Database.MigrateAsync();  // Applies pending migrations on startup
```

---

## Common Performance Issues in EF Core

### Cartesian Explosion

Using `Include` with multiple collections can cause a Cartesian product:

```csharp
// ❌ Cartesian explosion — SQL JOINs produce Orders × Items × Payments rows
var orders = await _context.Orders
    .Include(o => o.OrderItems)    // 10 items
    .Include(o => o.Payments)      // 5 payments
    .ToListAsync();
// Gets 50 rows (10 × 5) from SQL, EF Core deduplicates in memory

// ✅ Fix: Use AsSplitQuery — separate SQL queries, no Cartesian product
var orders = await _context.Orders
    .Include(o => o.OrderItems)
    .Include(o => o.Payments)
    .AsSplitQuery()                // 3 separate queries instead of 1 JOIN
    .ToListAsync();
```

### Over-fetching (Select *)

```csharp
// ❌ Loads all columns including large text/binary fields you don't need
var orders = await _context.Orders.ToListAsync();

// ✅ Project to a DTO — only needed columns fetched from DB
var orders = await _context.Orders
    .Select(o => new OrderListDto
    {
        Id   = o.OrderID,
        Date = o.OrderDate,
        Name = o.Customer.Name
    })
    .ToListAsync();
```

---

## EF Core with Transactions

```csharp
// Using EF Core's transaction API
await using var transaction = await _context.Database.BeginTransactionAsync();
try
{
    var order = new Order { ... };
    _context.Orders.Add(order);
    await _context.SaveChangesAsync();

    // Do more work...
    await _context.SaveChangesAsync();

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

---

## Compiled Queries

For hot paths called thousands of times per second, pre-compile the LINQ query to avoid expression tree parsing overhead:

```csharp
private static readonly Func<AppDbContext, int, Task<Order?>> GetOrderByIdQuery =
    EF.CompileAsyncQuery((AppDbContext ctx, int id) =>
        ctx.Orders.FirstOrDefault(o => o.OrderID == id));

// Use it
var order = await GetOrderByIdQuery(_context, orderId);
```

---

## Interview Q&A

**Q: What's the difference between `Find` and `FirstOrDefault` in EF Core?**
`Find` checks the local change tracker first before going to the database. `FirstOrDefault` always goes to the database. Use `Find` when you might have already loaded the entity in the same request scope.

**Q: How does EF Core know what SQL to generate?**
LINQ expressions are translated to expression trees, which EF Core's query provider translates to SQL at runtime. This is why operations that can't be translated (like custom C# methods) throw a runtime error.

**Q: What is a migration and how does it differ from `EnsureCreated`?**
A migration is a versioned, incremental schema change script. `EnsureCreated` creates the whole schema from scratch in one shot — it can't be used alongside migrations and is only suitable for test/demo databases.

**Q: When would you choose raw SQL over LINQ in EF Core?**
For complex queries that don't translate well to LINQ (CTEs, window functions, complex pivots), or stored procedure calls, use `FromSqlRaw` / `ExecuteSqlRaw` / Dapper.
