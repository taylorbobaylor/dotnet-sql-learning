# Parameter Sniffing

Parameter sniffing is one of those "gotcha" topics that separates developers with real SQL Server experience from those without. Knowing this topic well will impress interviewers.

---

## What Is Parameter Sniffing?

When SQL Server first executes a stored procedure, it **"sniffs" the parameter values** and builds an execution plan optimized for those specific values. That plan is then **cached and reused** for all future executions — even if the next call uses wildly different parameters that would benefit from a completely different plan.

**Example scenario:**

```sql
CREATE PROCEDURE dbo.GetOrdersByCustomer
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM Orders WHERE CustomerID = @CustomerID;
END;
```

Imagine the data distribution:
- Customer 1: 2 rows
- Customer 999: 500,000 rows

If the SP is first called with `@CustomerID = 1`, SQL Server builds a plan using **Nested Loops** (efficient for 2 rows). That plan is cached. Now Customer 999 calls the same SP — SQL Server reuses the cached Nested Loops plan, which is terrible for 500,000 rows. The SP grinds to a halt.

The reverse is also true — first call is Customer 999 (500K rows → Hash Join plan), then Customer 1 uses that same heavyweight plan for their 2 rows.

---

## How to Identify Parameter Sniffing

**Signs in production:**
- SP runs fast for some users, extremely slow for others
- SP runs fast when you copy/paste its logic into a new query window (ad-hoc SQL gets a fresh plan)
- Restarting the SQL Server (clears plan cache) temporarily fixes the problem
- "It was fast yesterday and slow today" — something triggered a recompile with bad params

**Confirm via execution plan:**

```sql
-- Run with actual execution plan + statistics
SET STATISTICS IO ON;
EXEC dbo.GetOrdersByCustomer @CustomerID = 999;
```

Look for:
- Row count mismatch: Estimated 2 rows, Actual 500,000 rows
- Nested Loops join where Hash Match would be appropriate (or vice versa)

**Check the plan cache:**

```sql
SELECT
    qs.execution_count,
    qs.total_logical_reads / qs.execution_count AS avg_logical_reads,
    qp.query_plan,
    -- This shows the sniffed parameter values the plan was compiled with:
    TRY_CAST(qp.query_plan AS XML).value(
        '(//ParameterList/ColumnReference[@Column="@CustomerID"]/@ParameterCompiledValue)[1]',
        'NVARCHAR(100)') AS sniffed_value
FROM sys.dm_exec_procedure_stats ps
CROSS APPLY sys.dm_exec_query_plan(ps.plan_handle) qp
WHERE OBJECT_NAME(ps.object_id) = 'GetOrdersByCustomer';
```

---

## Solutions

### Option 1: `OPTION (RECOMPILE)` — Per Query

Forces recompilation for that specific query on every execution. The optimizer uses the actual current parameter values.

```sql
CREATE PROCEDURE dbo.GetOrdersByCustomer
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM Orders
    WHERE CustomerID = @CustomerID
    OPTION (RECOMPILE);  -- ← recompiles this query every time
END;
```

**Pros:** Optimal plan every time
**Cons:** CPU overhead from constant recompilation. Bad for high-frequency calls.
**Best for:** Queries called infrequently OR when data distribution is wildly variable

### Option 2: `WITH RECOMPILE` — Whole Procedure

Recompiles the entire stored procedure every time it runs.

```sql
CREATE PROCEDURE dbo.GetOrdersByCustomer
    @CustomerID INT
WITH RECOMPILE   -- ← procedure-level
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM Orders WHERE CustomerID = @CustomerID;
END;
```

**Pros:** Simple to implement
**Cons:** Procedure-level means even simple statements recompile
**Best for:** Legacy code quick-fix; prefer query-level `OPTION (RECOMPILE)`

### Option 3: `OPTION (OPTIMIZE FOR UNKNOWN)`

Tells the optimizer to ignore the sniffed parameter value and instead use average statistics.

```sql
CREATE PROCEDURE dbo.GetOrdersByCustomer
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM Orders
    WHERE CustomerID = @CustomerID
    OPTION (OPTIMIZE FOR (@CustomerID UNKNOWN));
END;
```

**Pros:** Plan is cached (no recompile overhead), plan is based on average, not an outlier
**Cons:** Plan may not be optimal for either extreme
**Best for:** Moderate variability where no single value is wildly different; high-frequency calls

### Option 4: `OPTION (OPTIMIZE FOR (value))`

Build the plan optimized for a specific value you know is "representative."

```sql
OPTION (OPTIMIZE FOR (@CustomerID = 100))
```

**Best for:** When you know a specific value produces a good plan for the majority of cases

### Option 5: Local Variable Trick

Assign the parameter to a local variable. SQL Server can't sniff local variables — it uses average statistics instead.

```sql
CREATE PROCEDURE dbo.GetOrdersByCustomer
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @LocalCustomerID INT = @CustomerID;  -- ← local variable

    SELECT * FROM Orders WHERE CustomerID = @LocalCustomerID;
END;
```

**Pros:** Simple, no hints needed
**Cons:** Same as OPTIMIZE FOR UNKNOWN — average statistics. Plan is cached.
**Best for:** Moderate variability, high-frequency calls

### Option 6: Plan Guides (Advanced)

Force a specific execution plan for a query without changing the code. Used when you can't modify the stored procedure (third-party code, etc.).

```sql
EXEC sp_create_plan_guide
    @name = N'PG_GetOrdersByCustomer',
    @stmt = N'SELECT * FROM Orders WHERE CustomerID = @CustomerID',
    @type = N'OBJECT',
    @module_or_batch = N'dbo.GetOrdersByCustomer',
    @hints = N'OPTION (OPTIMIZE FOR (@CustomerID UNKNOWN))';
```

---

## Decision Guide

```
Is the SP called very frequently? (100s+ times/sec)
├── YES → OPTIMIZE FOR UNKNOWN or local variable trick
└── NO → OPTION (RECOMPILE) is fine

Is data distribution highly variable (some values = 1 row, some = millions)?
├── YES → OPTION (RECOMPILE) per query
└── NO → OPTIMIZE FOR UNKNOWN

Can you modify the stored procedure code?
├── YES → Use query hints or local variable trick
└── NO → Plan Guide
```

---

## Interview Answer Template

> "Parameter sniffing happens when SQL Server builds an execution plan based on the first set of parameter values passed to a stored procedure, then reuses that plan for all future calls — even when the data distribution for those parameters is totally different. It usually shows up as 'fast for some users, slow for others' or 'was fast yesterday.'
>
> My go-to diagnostic is to check the actual execution plan and compare estimated vs actual row counts. A big mismatch — like 1 estimated vs 500,000 actual — is the telltale sign.
>
> For the fix, it depends on call frequency. For infrequent queries with highly variable data, I'd add `OPTION (RECOMPILE)` to the problematic query. For high-frequency calls where recompile overhead would be a problem, I'd use `OPTION (OPTIMIZE FOR UNKNOWN)` or the local variable trick so the plan is based on average statistics and still gets cached."
