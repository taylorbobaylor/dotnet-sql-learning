# Execution Plans

Reading execution plans is the most powerful skill for diagnosing slow queries. Knowing how to talk about them confidently separates developers who can tune SQL from those who can't.

---

## What Is an Execution Plan?

An execution plan is SQL Server's "recipe" for how it will retrieve data. The query optimizer evaluates many possible plans and picks the one it estimates to be cheapest (in terms of I/O and CPU). The plan is then cached and reused.

There are two types:
- **Estimated execution plan** (`Ctrl+L`) — generated without running the query, uses statistics
- **Actual execution plan** (`Ctrl+M`, then run) — captured during real execution, shows actual vs estimated row counts

Always use the **actual execution plan** when diagnosing a slow query — the difference between estimated and actual row counts often reveals the root cause.

---

## How to Read an Execution Plan

Plans are read **right to left, top to bottom** in SSMS. Data flows left to the final output operator.

Each operator (node) shows:
- **Cost %** — percentage of total query cost attributed to this operator
- **Estimated rows** vs **Actual rows** — big discrepancy = stale statistics or parameter sniffing
- **Arrow thickness** — proportional to row count between operators

**Common operators:**

| Operator | Meaning |
|---|---|
| **Index Seek** | ✅ Used an index to find specific rows — fast |
| **Index Scan** | ⚠️ Scanned the entire index — may be OK for small tables |
| **Table Scan** | ❌ No usable index, read every row in a heap table |
| **Key Lookup** | ⚠️ Had to go back to clustered index for extra columns — covering index opportunity |
| **Nested Loops** | Join strategy — good for small outer sets with index seeks on inner |
| **Hash Match** | Join strategy — good for large, unsorted datasets (high memory) |
| **Merge Join** | Join strategy — efficient when both inputs are sorted on join key |
| **Sort** | Expensive if not on an indexed column |
| **Spool** | Temporary storage — often indicates optimizer is working around a problem |
| **Parallelism** | Query using multiple threads |

---

## Red Flags in Execution Plans

### 🔴 Table Scan on a Large Table

```
Table Scan (Cost: 87%)
```

A table scan reads every single row. On a large table, this is almost always a sign of a missing index or a non-SARGable predicate.

**Fix:** Add an appropriate index, or fix the WHERE clause to use an existing index.

### 🟡 Key Lookup

```
Index Seek → Key Lookup (Cost: 45%)
```

SQL Server found the row in a nonclustered index, but needed to go back to the clustered index to fetch extra columns not in the index.

**Fix:** Add the needed columns to the index as `INCLUDE` columns to make it a covering index.

### 🟡 Estimated vs Actual Row Count Mismatch

```
Estimated rows: 1  |  Actual rows: 450,000
```

The optimizer thought there'd be 1 row but got 450,000. This leads to a bad plan choice (e.g., nested loops instead of hash join). Usually caused by:
- Stale statistics → run `UPDATE STATISTICS`
- Parameter sniffing → see [Parameter Sniffing](parameter-sniffing.md)
- Multi-statement TVFs (always estimate 1 row in older SQL Server versions)

### 🟡 Sort Operator with High Cost

```
Sort (Cost: 62%)
```

Sorting is expensive and can't be parallelized the same way. If you frequently need sorted results, consider adding an index with the appropriate sort order.

### 🟡 Thick Arrows in Unexpected Places

Fat arrows = many rows. If you see a fat arrow going into a filter/join early in the plan, that means SQL Server is retrieving more rows than necessary before filtering. This often means the filter predicate isn't being pushed down through the plan.

---

## Using `SET STATISTICS IO`

This is your go-to tool in SSMS for measuring I/O impact.

```sql
SET STATISTICS IO ON;
SET STATISTICS TIME ON;

EXEC dbo.GetCustomerOrders @CustomerID = 1234;

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
```

**Sample Messages output:**

```
Table 'Orders'. Scan count 1, logical reads 3842, physical reads 0,
read-ahead reads 0, lob logical reads 0.

SQL Server Execution Times:
   CPU time = 234 ms,  elapsed time = 412 ms.
```

**What to look at:**
- **Logical reads** — pages read from the buffer cache. 1 page = 8KB. High logical reads = lots of data being processed
- **Physical reads** — pages read from disk (cache miss). High = data not in memory
- **Scan count** > 1 on the same table in a loop = possible missing index or cursor issue

**Benchmark improvements:** Before and after optimization, compare logical reads. A good optimization often drops logical reads by 10x or more.

---

## Execution Plan Cache

Plans are cached in the **plan cache** (part of the buffer pool). You can query it:

```sql
-- Find cached plan for a specific stored procedure
SELECT
    qs.execution_count,
    qs.total_logical_reads,
    qs.total_elapsed_time,
    qp.query_plan
FROM sys.dm_exec_procedure_stats ps
CROSS APPLY sys.dm_exec_query_plan(ps.plan_handle) qp
CROSS APPLY sys.dm_exec_sql_text(ps.sql_handle) st
WHERE OBJECT_NAME(ps.object_id) = 'GetCustomerOrders';
```

**Clearing the plan cache (dev/test only, never production):**

```sql
-- Clear entire plan cache — dev/test ONLY, major performance impact on prod
DBCC FREEPROCCACHE;

-- Clear cache for a specific plan
DBCC FREEPROCCACHE (plan_handle);
```

---

## Workflow: Diagnosing a Slow Stored Procedure

```
1. Enable actual execution plan (Ctrl+M) + SET STATISTICS IO ON
2. Run the stored procedure
3. Check Messages tab:
   - High logical reads on which table?
4. Check execution plan:
   - Any Table Scans? → missing index
   - Any Key Lookups? → add INCLUDE columns
   - Estimated vs Actual row mismatch? → statistics/parameter sniffing
   - Thick arrow before a filter? → predicate not pushed down, non-SARGable
5. Address the highest-cost operator first
6. Re-run and compare logical reads before/after
7. Repeat until satisfactory
```

---

## Interview Explanation Template

> "When I get a slow stored procedure, first thing I do is run it in SSMS with `SET STATISTICS IO ON` and the actual execution plan enabled. I look at logical reads per table — that tells me where the I/O is happening. Then I look at the execution plan for table scans, key lookups, and any large discrepancies between estimated and actual row counts. Table scans usually mean a missing or unused index. Key lookups mean the index doesn't cover all the columns the query needs, so I'd add an INCLUDE clause. Row count mismatches usually point to stale statistics or parameter sniffing. I fix the highest-cost issue first, re-measure, and iterate."
