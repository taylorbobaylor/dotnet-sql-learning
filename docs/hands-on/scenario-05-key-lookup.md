# Scenario 5: The Key Lookup Tax

> **Antipattern:** A nonclustered index only covers the search key, not the columns the query returns.
> **Symptom:** Execution plan shows a **Key Lookup** operator next to every Index Seek row.
> **Fix:** Add `INCLUDE` columns to create a **covering index**.

---

## The Story

The orders dashboard needed to load all Pending orders quickly. A developer added an index on the `Status` column:

```sql
CREATE NONCLUSTERED INDEX IX_Orders_Status ON dbo.Orders (Status);
```

It helped — the query now uses the index. But the execution plan still shows a **Key Lookup** on every matching row. For 10,000 Pending orders, that's 10,000 individual page lookups back to the clustered index to fetch `OrderID`, `CustomerID`, `OrderDate`, and `TotalAmount`.

The index tells SQL Server *which* rows match, but not *what's in them*. For every match, SQL Server has to follow a pointer back to the actual row data. At scale, this adds up to thousands of extra I/O operations.

---

## Understanding Key Lookups

```
Query: SELECT OrderID, CustomerID, OrderDate, TotalAmount
       FROM Orders WHERE Status = 'Pending'

Nonclustered index IX_Orders_Status contains:
  Status [key]    →    Clustered index pointer (OrderID)

For each matching Status row:
  1. Index Seek into IX_Orders_Status  ← FAST
  2. Key Lookup via pointer → clustered index  ← EXPENSIVE, × N rows
     (to get OrderID, CustomerID, OrderDate, TotalAmount)
```

The **Key Lookup** is the "tax" you pay for having a narrow index. 10,000 Pending orders = 10,000 Key Lookups.

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

**Step 2:** Run the bad SP (narrow index, causes Key Lookups):

```sql
EXEC dbo.usp_Bad_GetPendingOrders @Status = 'Pending';
```

In the **Execution Plan tab**, look for two connected operators:
```
Index Seek [IX_Orders_Status]  →  Key Lookup [dbo.Orders] (Clustered)
```

The Key Lookup node typically shows **60–80% of total query cost** — it dominates the plan. Hover/click the Key Lookup node to inspect its properties:

```
Output List: [Orders].CustomerID, [Orders].OrderDate, [Orders].TotalAmount
```

Those "Output List" columns are the ones the index doesn't have — they're being fetched one-by-one from the clustered index. Each of those fetches is a separate random I/O for every matching row.

**Step 3:** Run the fixed SP (covering index applied in script 06):

```sql
EXEC dbo.usp_Fixed_GetPendingOrders @Status = 'Pending';
```

The execution plan now shows only:
```
Index Seek [IX_Orders_Status_Covering]
```

No Key Lookup operator. SQL Server satisfied the entire query from the covering index. The logical reads in Messages/Output will drop significantly.

---

## Manually Dropping and Recreating the Index

Want to see the difference yourself? Drop the covering index and go back to the narrow one:

```sql
-- Step back to the narrow index
DROP INDEX IF EXISTS IX_Orders_Status_Covering ON dbo.Orders;

CREATE NONCLUSTERED INDEX IX_Orders_Status
    ON dbo.Orders (Status);  -- Narrow — no INCLUDE

-- Run the bad SP, see Key Lookup
EXEC dbo.usp_Bad_GetPendingOrders @Status = 'Pending';

-- Now fix it — add INCLUDE columns
DROP INDEX IX_Orders_Status ON dbo.Orders;

CREATE NONCLUSTERED INDEX IX_Orders_Status_Covering
    ON dbo.Orders (Status)
    INCLUDE (OrderID, CustomerID, OrderDate, TotalAmount);

-- Run again — Key Lookup is gone
EXEC dbo.usp_Fixed_GetPendingOrders @Status = 'Pending';
```

---

## INCLUDE vs Key Columns — When to Use Each

```sql
CREATE NONCLUSTERED INDEX IX_Example
    ON dbo.Orders (Status, CustomerID)   -- Key columns: part of sort order, can be seeked
    INCLUDE (OrderDate, TotalAmount);    -- Include columns: stored at leaf level only
```

**Put in the key columns:**
- Columns in WHERE clause predicates (equality filters first, then range)
- Columns in JOIN conditions
- Columns in ORDER BY (if you want to avoid a sort operation)

**Put in INCLUDE:**
- All other columns the query SELECTs
- Fat columns (NVARCHAR, XML, JSON) — they'd bloat the index tree if in the key
- `NVARCHAR(MAX)` and `VARBINARY(MAX)` CANNOT be in key columns at all, but CAN be in INCLUDE

---

## How to Find Key Lookups in Your Database

```sql
-- Find queries with key lookups in the plan cache
SELECT TOP 20
    SUBSTRING(st.text, (qs.statement_start_offset/2)+1, 200) AS query_snippet,
    qs.total_logical_reads / qs.execution_count AS avg_logical_reads,
    qs.execution_count
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
WHERE CAST(qp.query_plan AS NVARCHAR(MAX)) LIKE '%Key Lookup%'
FROM sys.dm_exec_query_stats qs
CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
CROSS APPLY sys.dm_exec_query_plan(qs.plan_handle) qp
WHERE CAST(qp.query_plan AS NVARCHAR(MAX)) LIKE '%KeyLookup%'
ORDER BY avg_logical_reads DESC;
```

---

## Interview Answer

> "A key lookup happens when a nonclustered index gets SQL Server to the right rows, but doesn't contain all the columns the query needs — so SQL Server has to make a second trip back to the clustered index for each row to get those columns. For a small number of rows it's fine, but at scale — 10,000 Pending orders — you're paying 10,000 individual page lookups. The fix is a covering index: add the missing columns as INCLUDE columns. They're stored at the leaf level of the index, so the query can be satisfied entirely from the index without any key lookups. I look for the Key Lookup operator in the execution plan — it's often the highest-cost node and the fix is usually a simple ALTER INDEX to add INCLUDE columns."
