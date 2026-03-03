# Generics & LINQ

---

## Generics

Generics let you write type-safe, reusable code without knowing the concrete type at compile time.

```csharp
// Without generics — not type-safe, requires casting
public class Box
{
    private object _value;
    public void Set(object val) { _value = val; }
    public object Get() { return _value; }
}

// With generics — type-safe, no casting needed
public class Box<T>
{
    private T _value;
    public void Set(T val)  { _value = val; }
    public T    Get()       { return _value; }
}

var intBox    = new Box<int>();
var stringBox = new Box<string>();
intBox.Set(42);
```

### Generic Constraints

```csharp
// T must implement IComparable<T>
public T Max<T>(T a, T b) where T : IComparable<T>
    => a.CompareTo(b) >= 0 ? a : b;

// T must be a class (reference type) with a parameterless constructor
public T CreateInstance<T>() where T : class, new()
    => new T();

// T must be a value type (struct)
public void Process<T>(T val) where T : struct { ... }

// T must implement an interface
public void Save<T>(T entity) where T : IEntity { ... }
```

### Common Generic Interfaces

`IEnumerable<T>`, `IList<T>`, `ICollection<T>`, `IDictionary<TKey, TValue>`, `IReadOnlyList<T>` — know when to use each.

---

## LINQ

LINQ (Language Integrated Query) lets you query collections using a consistent, readable syntax. Two styles exist — method syntax and query syntax — and they're equivalent.

```csharp
var numbers = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

// Method syntax (more common in practice)
var evenSquares = numbers
    .Where(n => n % 2 == 0)
    .Select(n => n * n)
    .OrderByDescending(n => n)
    .ToList();

// Query syntax (looks like SQL)
var evenSquares2 = (from n in numbers
                    where n % 2 == 0
                    select n * n)
                   .OrderByDescending(n => n)
                   .ToList();
```

### Deferred Execution — Critical to Understand

LINQ queries are **not executed when you write them**. They're executed when you enumerate the results (call `.ToList()`, `.FirstOrDefault()`, `foreach`, etc.).

```csharp
var query = numbers.Where(n => n > 5);  // ← Not executed yet — just defines the query

// Execution happens here:
var result1 = query.ToList();           // Execute now, materialize to list
var result2 = query.FirstOrDefault();  // Execute now, get first match
foreach (var n in query) { ... }       // Execute per iteration

// This means the query runs again each time you enumerate it!
// If you need the results multiple times, call .ToList() once and cache it.
```

### `IEnumerable<T>` vs `IQueryable<T>`

```csharp
// IEnumerable<T> — LINQ to Objects — executes in memory
List<Order> orders = GetAllOrders();  // All rows loaded into memory
var recent = orders.Where(o => o.Date > cutoff);  // Filtered in C#

// IQueryable<T> — LINQ to SQL / EF Core — translates to SQL
IQueryable<Order> query = _context.Orders;  // No SQL yet
var recent = query.Where(o => o.Date > cutoff);  // Adds WHERE clause to SQL
var result = recent.ToList();  // SQL executed: SELECT * FROM Orders WHERE Date > @p0
```

**The gotcha:** Adding `.AsEnumerable()` or `.ToList()` mid-query forces in-memory execution from that point. Any LINQ after that runs in C#, not SQL.

```csharp
// ❌ Loads ALL orders to memory, then filters in C#
var expensive = _context.Orders
    .ToList()                           // ← Everything loaded here
    .Where(o => o.Total > 1000);

// ✅ Filters in SQL — only matching rows returned
var expensive = _context.Orders
    .Where(o => o.Total > 1000)
    .ToList();
```

### Common LINQ Methods

```csharp
var orders = _context.Orders.AsQueryable();

// Filtering
orders.Where(o => o.Status == "Active")

// Projection
orders.Select(o => new { o.OrderID, o.TotalAmount })
orders.Select(o => new OrderDto { Id = o.OrderID, Total = o.TotalAmount })

// Sorting
orders.OrderBy(o => o.OrderDate)
orders.OrderByDescending(o => o.TotalAmount)
orders.OrderBy(o => o.CustomerID).ThenBy(o => o.OrderDate)

// Aggregation
orders.Count()
orders.Count(o => o.Status == "Active")
orders.Sum(o => o.TotalAmount)
orders.Average(o => o.TotalAmount)
orders.Max(o => o.TotalAmount)

// First / Single
orders.First()              // throws if empty
orders.FirstOrDefault()     // null if empty
orders.Single()             // throws if not exactly 1 result
orders.SingleOrDefault()    // null if empty, throws if more than 1

// Existence checks
orders.Any()                // true if any rows
orders.Any(o => o.Total > 0)
orders.All(o => o.Status != "Cancelled")

// Pagination
orders.Skip(20).Take(10)    // Page 3 of 10 per page

// Joining
var result = orders.Join(
    customers,
    o => o.CustomerID,
    c => c.CustomerID,
    (o, c) => new { o.OrderID, c.Name });

// GroupBy
var grouped = orders
    .GroupBy(o => o.CustomerID)
    .Select(g => new {
        CustomerID = g.Key,
        Count = g.Count(),
        Total = g.Sum(o => o.TotalAmount)
    });

// Flattening
var items = orders.SelectMany(o => o.OrderItems);

// Set operations
var merged   = list1.Union(list2);
var common   = list1.Intersect(list2);
var onlyLeft = list1.Except(list2);

// Distinct
orders.Select(o => o.CustomerID).Distinct();

// ToLookup (like Dictionary<TKey, IEnumerable<TValue>>)
var byCustomer = orders.ToLookup(o => o.CustomerID);
var customerOrders = byCustomer[customerId];  // O(1) lookup
```

### LINQ Performance Tips

- Call `.Where()` and filtering operators **before** `.Select()` to reduce the working set early
- Use `.Any()` instead of `.Count() > 0` (stops at first match)
- Use `.FirstOrDefault()` instead of `.Where(...).First()` (same result, slightly cleaner)
- Materialize with `.ToList()` or `.ToArray()` when you need to iterate multiple times
- For EF Core: avoid `.ToList()` before filtering — let the DB do the work
