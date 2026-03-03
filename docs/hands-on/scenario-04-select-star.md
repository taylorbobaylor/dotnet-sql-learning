# Scenario 4: The SELECT * Flood

> **Antipattern:** `SELECT *` pulling columns the caller never uses, including fat `NVARCHAR(MAX)` data.
> **Symptom:** Query is slow, network I/O is saturated, memory usage spikes in the app.
> **Fix:** Explicit column list — only select what you actually need.

---

## The Story

A developer built a customer order history page. They wrote:

```sql
SELECT * FROM Orders WHERE CustomerID = @ID
```

Quick to write, easy to read. On the dev machine with 20 orders it was instant.

In production, BigCorp (CustomerID=1) has 50,000 orders. Each of those orders has a `Notes` column seeded with ~800 bytes of JSON audit data. `SELECT *` pulls every single byte of every Notes value — **~40MB per query call** — for a UI that only ever shows OrderID, Date, Status, and Amount. The four columns that matter are **8 bytes total per row**.

The query also can't be covered by any nonclustered index because `NVARCHAR(MAX)` columns can't be included in indexes — so SQL Server always hits the clustered index for those rows.

---

## The Bad SP

```sql
CREATE OR ALTER PROCEDURE dbo.usp_Bad_GetCustomerOrderHistory
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *                    -- ❌ Pulls Notes (NVARCHAR MAX) for every row
    FROM dbo.Orders o
    WHERE o.CustomerID = @CustomerID
    ORDER BY o.OrderDate DESC;
END;
```

---

## The SSMS Exercise

**Step 1:** Enable statistics:

```sql
SET STATISTICS IO ON;
SET STATISTICS TIME ON;
-- Also press Ctrl+M for the actual plan
```

**Step 2:** Run the bad version for BigCorp:

```sql
EXEC dbo.usp_Bad_GetCustomerOrderHistory @CustomerID = 1;
```

In the Messages tab, watch for **lob logical reads** — this is SQL Server reading LOB (Large Object) pages for the NVARCHAR(MAX) column:

```
Table 'Orders'. Scan count 1, logical reads 1843, lob logical reads 4920
```

Those `lob logical reads` are the Notes pages. Each LOB page = 8KB. 4,920 pages = ~39MB of data read just for the Notes column.

**Step 3:** Run the fixed version:

```sql
EXEC dbo.usp_Fixed_GetCustomerOrderHistory @CustomerID = 1;
```

```
Table 'Orders'. Scan count 1, logical reads 1843, lob logical reads 0
```

LOB reads drop to **zero** because the Notes column is never accessed.

---

## The Fixed SP

```sql
CREATE OR ALTER PROCEDURE dbo.usp_Fixed_GetCustomerOrderHistory
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- ✅ Only the columns the UI actually uses
    SELECT
        o.OrderID,
        o.OrderDate,
        o.Status,
        o.TotalAmount,
        o.ShipCity
    FROM dbo.Orders o
    WHERE o.CustomerID = @CustomerID
    ORDER BY o.OrderDate DESC;
END;
```

---

## Why This Matters Beyond Performance

`SELECT *` has additional problems:

**1. Fragile to schema changes:** If someone adds or reorders a column, `SELECT *` returns a different shape — positional `DataReader` access breaks silently.

**2. Wider network payloads:** Every row is larger, burning more bandwidth between app servers and the database.

**3. More memory pressure on SQL Server:** SQL Server buffers data in the buffer pool. Pulling fat columns evicts other useful pages from cache.

**4. ORM footprint:** If you map to a model with `SELECT *`, you're instantiating the entire object graph including fields you'll never read.

---

## A Word on Projection in EF Core

The same problem occurs in Entity Framework when you forget to project:

```csharp
// ❌ Loads ALL columns including Notes into every Order object
var orders = await _context.Orders
    .Where(o => o.CustomerID == customerId)
    .ToListAsync();

// ✅ Projection to a DTO — only the 5 columns needed
var orders = await _context.Orders
    .Where(o => o.CustomerID == customerId)
    .Select(o => new OrderSummaryDto
    {
        OrderID     = o.OrderID,
        OrderDate   = o.OrderDate,
        Status      = o.Status,
        TotalAmount = o.TotalAmount,
        ShipCity    = o.ShipCity
    })
    .ToListAsync();
```

The generated SQL for the projection omits `Notes` entirely — the database never reads those LOB pages.

---

## Interview Answer

> "SELECT * is almost always a bad idea in production stored procedures. The obvious reason is network overhead — you're transmitting columns the caller never uses. But the more subtle problem is LOB columns: if a table has an NVARCHAR(MAX) or VARBINARY(MAX) column, SELECT * forces SQL Server to read the large-object pages for every single row, even if the caller throws that data away immediately. In one case we had a reporting query that was reading 40MB per call because it was dragging a JSON notes column through SELECT *. Replacing it with an explicit column list dropped the LOB reads to zero and cut query time by 80%. I always explicit-column in stored procedures — it's also much more resilient when the schema evolves."
