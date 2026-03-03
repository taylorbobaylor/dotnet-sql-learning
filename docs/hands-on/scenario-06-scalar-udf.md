# Scenario 6: The Scalar UDF Killer

> **Antipattern:** A scalar user-defined function called in a WHERE clause or SELECT list.
> **Symptom:** Query executes row-by-row (serial), often 10-100× slower than equivalent inline logic.
> **Fix:** Inline the logic as a JOIN/CASE expression, or use an inline Table-Valued Function.

---

## The Story

The team needed to filter orders to only those from "Gold" tier customers. Someone created a clean, reusable function:

```sql
CREATE FUNCTION dbo.fn_GetCustomerTier(@CustomerID INT)
RETURNS NVARCHAR(10) AS BEGIN ... END
```

It looked elegant. You could use it anywhere. The problem: SQL Server calls this function **once per row of the outer query**. On 55,000+ orders, that's 55,000 individual function executions — each doing its own lookup. And because scalar UDFs in WHERE clauses force serial execution, **parallelism is completely disabled**.

The query that should scan 55,000 rows using all 8 CPU cores now uses 1 core and executes 55,000 + 55,000 individual database calls.

---

## Why Scalar UDFs Are So Destructive

Three compounding problems:

**1. Row-by-row execution:** The function is called once per input row. No batching, no set operations.

**2. Forces serial plan:** Any query with a scalar UDF in the predicate or SELECT list cannot use a parallel execution plan. SQL Server sets `Degree of Parallelism = 1` for the entire query.

**3. Black box to the optimizer:** SQL Server treats the UDF as a black box — it can't see inside to estimate cost, push predicates down, or choose a better join strategy. It assumes 1 row output and 1 "fuzzy" cost unit.

---

## The Bad Code

```sql
-- The scalar UDF (docker/init/05-bad-stored-procs.sql)
CREATE OR ALTER FUNCTION dbo.fn_GetCustomerTier(@CustomerID INT)
RETURNS NVARCHAR(10) AS
BEGIN
    DECLARE @Tier NVARCHAR(10);
    SELECT @Tier = TierCode FROM dbo.Customers WHERE CustomerID = @CustomerID;
    RETURN ISNULL(@Tier, 'STANDARD');
END;

-- The stored procedure using it
CREATE OR ALTER PROCEDURE dbo.usp_Bad_GetGoldCustomerOrders
    @MinAmount DECIMAL(12,2) = 500.00
AS
BEGIN
    SET NOCOUNT ON;

    SELECT o.OrderID, o.CustomerID, o.OrderDate, o.TotalAmount,
           dbo.fn_GetCustomerTier(o.CustomerID) AS CustomerTier  -- ❌ Called per row in SELECT
    FROM dbo.Orders o
    WHERE dbo.fn_GetCustomerTier(o.CustomerID) = 'GOLD'          -- ❌ Called per row in WHERE
      AND o.TotalAmount >= @MinAmount;
END;
```

The UDF is called **twice** per row — once in WHERE (to filter) and once in SELECT (to display). On 55,000 orders: **110,000 function calls**.

---

## The Exercise

**Step 1:** Enable statistics and actual execution plan:

```sql
SET STATISTICS IO ON;
SET STATISTICS TIME ON;
```

Enable the actual plan **before** running:

| Tool | How to enable actual execution plan |
|---|---|
| **VS Code MSSQL** | Right-click → **"Run Query with Actual Execution Plan"** |
| **DataGrip** | Right-click → **Explain Plan → Explain Analyzed** |

**Step 2:** Run the bad SP:

```sql
EXEC dbo.usp_Bad_GetGoldCustomerOrders @MinAmount = 500.00;
```

In the **Execution Plan tab**, look for:

- A `Compute Scalar` node — this is the UDF being called once per row. Hover/click it to confirm it references `fn_GetCustomerTier`.
- **No parallelism operators** — in VS Code MSSQL the parallelism (Gather Streams) operator looks like two arrows (↔). If you don't see it anywhere in the plan, the query is fully serial. DataGrip similarly shows no parallel branches.
- In the **Messages/Output tab**: check that `CPU time` is *disproportionately high* relative to `elapsed time`. Row-by-row UDF calls saturate a single core — CPU and elapsed will be close together and both large.

**Step 3:** Run the fixed SP:

```sql
EXEC dbo.usp_Fixed_GetGoldCustomerOrders @MinAmount = 500.00;
```

The fixed plan will show:
- A direct **Hash Match** or **Nested Loops** JOIN to the Customers table — one set-based operation instead of N function calls
- Potential **parallelism operators** (Parallelism / Gather Streams nodes) if the data volume justifies it
- Much lower CPU time and elapsed time in Messages/Output

---

## The Fixed SP

```sql
CREATE OR ALTER PROCEDURE dbo.usp_Fixed_GetGoldCustomerOrders
    @MinAmount DECIMAL(12,2) = 500.00
AS
BEGIN
    SET NOCOUNT ON;

    -- ✅ JOIN on the table directly — SARGable, parallel-friendly
    SELECT
        o.OrderID,
        o.CustomerID,
        o.OrderDate,
        o.TotalAmount,
        c.TierCode AS CustomerTier
    FROM dbo.Orders o
    INNER JOIN dbo.Customers c ON o.CustomerID = c.CustomerID
    WHERE c.TierCode = 'GOLD'         -- ✅ Direct column filter
      AND o.TotalAmount >= @MinAmount;
END;
```

SQL Server can now use statistics on `c.TierCode`, seek into any index that covers it, and parallelize the join.

---

## When You Need Reusable Logic: Inline TVF

If the tier logic genuinely has multiple steps and needs to be reused across many queries, use an **Inline Table-Valued Function** (iTVF) instead of a scalar UDF:

```sql
-- ✅ Inline TVF — SQL Server can see inside, inline like a view
CREATE OR ALTER FUNCTION dbo.fn_GetCustomerTierInline(@CustomerID INT)
RETURNS TABLE AS
RETURN (
    SELECT TierCode
    FROM dbo.Customers
    WHERE CustomerID = @CustomerID
);

-- Used as a JOIN — not row-by-row
SELECT o.OrderID, t.TierCode
FROM dbo.Orders o
CROSS APPLY dbo.fn_GetCustomerTierInline(o.CustomerID) t
WHERE t.TierCode = 'GOLD';
```

`CROSS APPLY` with an iTVF is treated by the optimizer like a correlated subquery that it can inline, push predicates through, and parallelize. It's the safe way to have reusable logic.

---

## Detecting Scalar UDF Problems in Production

```sql
-- Find queries with scalar UDFs in the plan cache that are burning CPU
SELECT TOP 10
    SUBSTRING(st.text, (qs.statement_start_offset/2)+1, 300) AS query_text,
    qs.total_worker_time  / qs.execution_count AS avg_cpu_microseconds,
    qs.total_elapsed_time / qs.execution_count AS avg_elapsed_microseconds,
    qs.execution_count
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
CROSS APPLY sys.dm_exec_query_plan(qs.plan_handle) qp
WHERE CAST(qp.query_plan AS NVARCHAR(MAX)) LIKE '%UDF%'
ORDER BY avg_cpu_microseconds DESC;
```

---

## SQL Server 2019+ Native Scalar UDF Inlining

SQL Server 2019 introduced **Scalar UDF Inlining** — certain simple UDFs are automatically inlined by the optimizer, regaining parallelism and removing row-by-row overhead.

```sql
-- Check if a UDF was inlined
SELECT name, is_inlineable
FROM sys.sql_modules sm
JOIN sys.objects o ON sm.object_id = o.object_id
WHERE o.type = 'FN' AND o.name = 'fn_GetCustomerTier';
```

If `is_inlineable = 1`, SQL Server can inline it. But it only works for simple UDFs — any `BEGIN`/`END` block with multiple statements, `TRY/CATCH`, or side effects disqualifies it.

!!! tip "Don't rely on automatic inlining"
    Write explicit JOINs or iTVFs. Inlining is a best-effort safety net, not a permission to write scalar UDFs carelessly.

---

## Interview Answer

> "Scalar UDFs in WHERE clauses are one of the most common hidden performance killers I've seen. The problem is two-fold: first, SQL Server calls the function once per row — it can't batch or vectorize the operation. Second, and more damaging, any query with a scalar UDF in a predicate or SELECT list forces a serial execution plan — no parallelism at all. On a table with 55,000 rows using 8 cores, that's an 8× floor on slowdown before you even factor in the row-by-row calls. The fix is to replace the scalar UDF with a direct JOIN on the underlying data. The optimizer can then see the statistics, use indexes, and parallelize the query. If the logic genuinely needs to be reusable, I'd use an inline Table-Valued Function with CROSS APPLY instead — the optimizer can see inside iTVFs and treat them like correlated views."
