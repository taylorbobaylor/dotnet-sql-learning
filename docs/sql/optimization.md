# Query Optimization

This is the "what would you do if a stored procedure is running slow?" page. Know this section well — it's the most likely deep-dive interview topic for a senior developer role.

---

## The "Slow Stored Procedure" Framework

When asked how you'd approach a slow stored procedure, walk through this structured approach:

### Step 1: Measure first, assume nothing

```sql
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

EXEC dbo.GetCustomerOrders @CustomerID = 1234;

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
```

Look at the **Messages tab** in SSMS. You'll see:
- **Logical reads** — pages read from the buffer cache (most important metric)
- **Physical reads** — pages read from disk (bad — means data wasn't cached)
- **CPU time** and **elapsed time**

High logical reads on a large table = index problem or full table scan.

### Step 2: Get the execution plan

Press `Ctrl+M` in SSMS before running the query to capture the **actual execution plan**.

Look for:
- **Table Scan / Index Scan** on large tables (red flag — look for a Seek instead)
- **Key Lookup** — nonclustered index found the row but needed to go back for more columns (covering index opportunity)
- **Sort** with a large estimated subtree cost
- **Fat arrows** — thick lines between operators mean large row counts (often unexpected)
- **Yellow warning triangles** — estimate vs actual row count mismatch (statistics problem)
- **Parallelism** issues — query using too many or too few threads

### Step 3: Check for missing indexes

SSMS will show green "Missing Index" hints in the execution plan. Use them as guidance (not gospel).

Also check the DMV query on the [Indexes page](indexes.md#finding-missing-indexes).

### Step 4: Fix the most impactful issue first

Common fixes, roughly ordered by impact:

| Problem | Fix |
|---|---|
| Table scan on large table | Add appropriate index |
| Key lookup | Make index covering with INCLUDE |
| Non-SARGable predicate | Rewrite the WHERE clause |
| Outdated statistics | `UPDATE STATISTICS TableName` |
| Parameter sniffing | See [Parameter Sniffing](parameter-sniffing.md) |
| Excessive logical reads | Review query logic, joins |
| Cursor / row-by-row logic | Rewrite as set-based |
| `SELECT *` | Specify only needed columns |

---

## Common Anti-Patterns and Fixes

### Anti-Pattern 1: Functions on Columns in WHERE

```sql
-- ❌ Kills index usage — SQL Server must evaluate the function for every row
WHERE CONVERT(DATE, CreatedAt) = '2024-06-01'
WHERE LEN(PhoneNumber) > 10
WHERE UPPER(Email) = 'USER@EXAMPLE.COM'

-- ✅ Rewrite to keep the column bare on one side
WHERE CreatedAt >= '2024-06-01' AND CreatedAt < '2024-06-02'
WHERE PhoneNumber LIKE '___________%'  -- 10+ chars
WHERE Email = 'user@example.com'       -- use case-insensitive collation
```

### Anti-Pattern 2: Implicit Type Conversions

```sql
-- ❌ CustomerCode is NVARCHAR, but we pass a plain string literal — implicit cast
WHERE CustomerCode = 'ABC123'      -- might be fine

-- ❌ But this is a silent type conversion disaster:
-- OrderID is INT, but someone stored it as VARCHAR and filters with INT
WHERE VarcharOrderID = 12345        -- SQL converts every row's VarcharOrderID to INT

-- ✅ Always match data types in predicates
WHERE VarcharOrderID = '12345'
```

### Anti-Pattern 3: OR in WHERE Clauses

```sql
-- ❌ Can prevent index usage or cause inefficient scans
WHERE CustomerID = @ID OR Email = @Email

-- ✅ Rewrite with UNION (each branch can use its own index)
SELECT * FROM Customers WHERE CustomerID = @ID
UNION
SELECT * FROM Customers WHERE Email = @Email AND CustomerID <> @ID
```

### Anti-Pattern 4: NOT IN with NULLs

```sql
-- ❌ If Subquery returns any NULLs, NOT IN returns 0 rows — silent bug!
WHERE CustomerID NOT IN (SELECT CustomerID FROM BlockedCustomers)

-- ✅ Use NOT EXISTS instead — handles NULLs correctly
WHERE NOT EXISTS (
    SELECT 1 FROM BlockedCustomers bc WHERE bc.CustomerID = c.CustomerID
)
```

### Anti-Pattern 5: Wildcard Leading LIKE

```sql
-- ❌ Leading wildcard — full index scan, can't seek
WHERE ProductName LIKE '%widget%'

-- ✅ Trailing wildcard only — can use index seek
WHERE ProductName LIKE 'widget%'

-- For full-text search, use Full-Text indexes with CONTAINS/FREETEXT
WHERE CONTAINS(ProductName, 'widget')
```

### Anti-Pattern 6: SELECT *

```sql
-- ❌ Retrieves unused columns, can trigger key lookups, wastes memory
SELECT * FROM Orders WHERE CustomerID = @ID

-- ✅ Explicit columns — faster, clearer, less fragile
SELECT OrderID, OrderDate, TotalAmount FROM Orders WHERE CustomerID = @ID
```

---

## Statistics

SQL Server's query optimizer relies on **statistics** to estimate how many rows a query will return. Stale statistics lead to bad execution plan choices.

```sql
-- View statistics for a table
SELECT * FROM sys.stats WHERE object_id = OBJECT_ID('Orders');

-- View details of a specific statistic
DBCC SHOW_STATISTICS('Orders', 'IX_Orders_CustomerID');

-- Update statistics on a table (reads a sample of data)
UPDATE STATISTICS Orders;

-- Update statistics with a full scan (more accurate, more I/O)
UPDATE STATISTICS Orders WITH FULLSCAN;

-- Update all statistics in the database
EXEC sp_updatestats;
```

**When to update statistics:**
- After a large data load (bulk import, ETL)
- When queries start returning bad plans unexpectedly
- Auto-update statistics is enabled by default but may lag on large tables

---

## Temporary Tables vs Table Variables

| Feature | Temp Table (`#temp`) | Table Variable (`@table`) |
|---|---|---|
| Scope | Session (visible to called SPs) | Batch/procedure |
| Statistics | Yes (updated) | No (estimated 1 row) |
| Transaction log | Yes | Minimal |
| Indexes | Yes (clustered + nonclustered) | Limited (only constraint-based) |
| Best for | Large datasets, joins | Small datasets, < ~100 rows |
| Recompile | May trigger recompile | No |

```sql
-- Temp table — better for large datasets
CREATE TABLE #TempOrders (
    OrderID    INT,
    TotalAmount DECIMAL(10,2),
    INDEX IX_Temp_OrderID (OrderID)  -- can add indexes
);

-- Table variable — better for small lookups
DECLARE @StatusCodes TABLE (
    Code   NVARCHAR(20),
    Label  NVARCHAR(100)
);
```

!!! tip "Common interview answer"
    "For large intermediate result sets I'd use a temp table because SQL Server maintains statistics on it, which gives the optimizer accurate row estimates. Table variables always estimate 1 row, which leads to bad plans on larger datasets."

---

## Query Hints (Use Sparingly)

Query hints override the optimizer's decisions. Use only as a last resort after exhausting other options.

```sql
-- Force an index
SELECT * FROM Orders WITH (INDEX(IX_Orders_CustomerID))
WHERE CustomerID = @ID;

-- Force a join type
SELECT * FROM Orders o
INNER LOOP JOIN Customers c ON o.CustomerID = c.CustomerID;  -- Nested loops

-- Force recompile (see Parameter Sniffing page)
EXEC dbo.GetCustomerOrders @ID = 1 WITH RECOMPILE;

-- Recompile at query level
SELECT * FROM Orders WHERE CustomerID = @ID OPTION (RECOMPILE);

-- Optimize for unknown
SELECT * FROM Orders WHERE CustomerID = @ID OPTION (OPTIMIZE FOR (@ID UNKNOWN));
```

---

## Key DMVs for Performance Troubleshooting

```sql
-- Top 10 most expensive queries currently in the plan cache
SELECT TOP 10
    qs.total_elapsed_time / qs.execution_count AS avg_elapsed_time,
    qs.total_logical_reads / qs.execution_count AS avg_logical_reads,
    qs.execution_count,
    SUBSTRING(st.text, (qs.statement_start_offset/2)+1,
        ((CASE qs.statement_end_offset WHEN -1 THEN DATALENGTH(st.text)
          ELSE qs.statement_end_offset END - qs.statement_start_offset)/2)+1) AS query_text
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
ORDER BY avg_logical_reads DESC;

-- Currently running queries
SELECT
    r.session_id,
    r.status,
    r.command,
    r.cpu_time,
    r.total_elapsed_time,
    r.logical_reads,
    st.text AS query_text
FROM sys.dm_exec_requests r
CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) st
WHERE r.session_id <> @@SPID;
```
