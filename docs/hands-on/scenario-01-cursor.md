# Scenario 1: The Cursor Catastrophe

> **Antipattern:** Processing rows one-by-one with a cursor instead of a set-based UPDATE.
> **Symptom:** SP that took seconds on test data takes 20+ minutes in production.
> **Fix:** Replace the cursor with a single set-based UPDATE.

---

## The Story

A new order management feature required recalculating `TotalAmount` on all Pending orders after a bulk price adjustment. The developer who wrote it had a background in procedural programming and reached for a cursor:

> *"Loop through each pending order, calculate the sum of its items, update the order. Simple."*

It worked fine in test with 50 orders. In production with 50,000 Pending orders it ran for 45 minutes and locked the table, taking down the order dashboard.

---

## The Bad SP

```sql
-- docker/init/05-bad-stored-procs.sql

CREATE OR ALTER PROCEDURE dbo.usp_Bad_RecalcOrderTotals
    @Status NVARCHAR(20) = 'Pending'
AS
BEGIN
    -- ❌ No SET NOCOUNT ON
    -- ❌ Cursor processing rows one at a time

    DECLARE @OrderID  INT;
    DECLARE @NewTotal DECIMAL(12,2);

    DECLARE order_cursor CURSOR FOR
        SELECT OrderID FROM dbo.Orders WHERE Status = @Status;

    OPEN order_cursor;
    FETCH NEXT FROM order_cursor INTO @OrderID;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        SELECT @NewTotal = SUM(oi.Quantity * oi.UnitPrice)
        FROM dbo.OrderItems oi
        WHERE oi.OrderID = @OrderID;

        UPDATE dbo.Orders
        SET TotalAmount = ISNULL(@NewTotal, 0)
        WHERE OrderID = @OrderID;

        FETCH NEXT FROM order_cursor INTO @OrderID;
    END;

    CLOSE order_cursor;
    DEALLOCATE order_cursor;
END;
```

---

## The Exercise

**Step 1:** Enable statistics and actual execution plan.

Paste this at the top of your query window in whichever tool you're using:

```sql
SET STATISTICS IO ON;
SET STATISTICS TIME ON;
```

Then enable the **actual execution plan** before running:

| Tool | How to enable actual execution plan |
|---|---|
| **VS Code MSSQL** | Right-click in editor → **"Run Query with Actual Execution Plan"** |
| **DataGrip** | Right-click → **Explain Plan → Explain Analyzed** |

**Step 2:** Run the bad version:

```sql
EXEC dbo.usp_Bad_RecalcOrderTotals @Status = 'Pending';
```

**Step 3:** Open the **Messages** tab (VS Code MSSQL) or **Output** panel (DataGrip). You'll see the `STATISTICS IO` output repeated hundreds of times — one entry per order — because the cursor fires individual I/O for each row.

**Step 4:** Run the fixed version:

```sql
EXEC dbo.usp_Fixed_RecalcOrderTotals @Status = 'Pending';
```

**Step 5:** Compare the Messages/Output. The fixed version shows a single pair of `STATISTICS IO` lines — one pass over each table.

---

## What You'll See in the Messages Tab

**Bad version (cursor):**
```
-- Repeated N times — once per order:
Table 'OrderItems'. Scan count 1, logical reads 4
Table 'Orders'.    Scan count 0, logical reads 3

SQL Server Execution Times: CPU time = 12 ms, elapsed = 18 ms
-- × 10,000 rows = 120,000 ms = 2 MINUTES just for CPU
```

**Fixed version (set-based):**
```
Table 'OrderItems'. Scan count 1, logical reads 1843
Table 'Orders'.    Scan count 1, logical reads  812

SQL Server Execution Times: CPU time = 340 ms, elapsed = 890 ms
-- Done in under 1 second
```

---

## The Fixed SP

```sql
CREATE OR ALTER PROCEDURE dbo.usp_Fixed_RecalcOrderTotals
    @Status NVARCHAR(20) = 'Pending'
AS
BEGIN
    SET NOCOUNT ON;  -- ✅ Always

    -- ✅ Single set-based UPDATE — one pass, one plan, can parallelize
    UPDATE o
    SET o.TotalAmount = ISNULL(item_totals.NewTotal, 0)
    FROM dbo.Orders o
    INNER JOIN (
        SELECT oi.OrderID, SUM(oi.Quantity * oi.UnitPrice) AS NewTotal
        FROM dbo.OrderItems oi
        INNER JOIN dbo.Orders ord ON oi.OrderID = ord.OrderID
        WHERE ord.Status = @Status
        GROUP BY oi.OrderID
    ) item_totals ON o.OrderID = item_totals.OrderID
    WHERE o.Status = @Status;

    SELECT @@ROWCOUNT AS RowsUpdated;
END;
```

---

## Why It's Faster

The cursor version has three problems:

1. **N individual transactions.** Each `UPDATE` is its own write to the transaction log. 10,000 orders = 10,000 log writes. Set-based = 1 log write.

2. **N individual seeks.** Each loop iteration does a fresh seek into `OrderItems` for just that OrderID. The set-based version does one efficient pass with a hash join.

3. **Can't parallelize.** Cursors execute serially. The set-based UPDATE can use all available CPU cores.

---

## When Cursors Are Actually OK

Cursors aren't always wrong. Use them when:

- Processing must happen in a specific order that genuinely can't be expressed as a set operation
- You need to call a stored procedure or execute dynamic SQL per row
- The result set is very small (< 100 rows) and the logic is complex
- You need to `PRINT` progress for long-running batch operations

But always ask yourself first: *"Can I express this as a single UPDATE/INSERT/DELETE?"* — the answer is yes more often than you'd think.

---

## Interview Answer

> "I replaced the cursor with a set-based UPDATE using a subquery to aggregate line item totals grouped by OrderID. The key insight is that everything a cursor does one row at a time, SQL Server can usually do in one set-based operation — one log write instead of N, one join instead of N individual seeks, and parallel execution instead of serial. In this case it went from minutes to under a second."
