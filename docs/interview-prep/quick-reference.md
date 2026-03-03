# Quick Reference Card

Review this in the morning before the interview. One page of everything you need to know cold.

---

## SQL Joins — At a Glance

| Join | Returns | Use When |
|---|---|---|
| `INNER JOIN` | Rows matching in BOTH tables | Only want records with a relationship |
| `LEFT JOIN` | All left rows + matching right (NULL if no match) | All left records, optional right data |
| `LEFT JOIN ... WHERE right.col IS NULL` | Left rows with NO match in right | Find orphaned / unmatched records |
| `RIGHT JOIN` | All right rows + matching left (NULL if no match) | Usually rewrite as LEFT JOIN |
| `FULL OUTER JOIN` | All rows from both (NULL where no match) | Data reconciliation / compare two sets |
| `CROSS JOIN` | Every combination (Cartesian product) | Test data, grids, matrices |
| `SELF JOIN` | Table joined to itself | Hierarchies, org charts |

---

## Diagnosing Slow Stored Procedures

```sql
-- Step 1: Measure
SET STATISTICS IO ON; SET STATISTICS TIME ON;
EXEC dbo.MySlowProc @Param = value;
SET STATISTICS IO OFF; SET STATISTICS TIME OFF;
```

**In Messages tab, look at:** logical reads per table

**In Execution Plan, look for:**

| Warning Sign | Root Cause | Fix |
|---|---|---|
| Table Scan | No usable index | Add index |
| Index Scan (large table) | Index exists but not efficient | Check predicate |
| Key Lookup | Non-covering index | Add INCLUDE columns |
| Estimates ≠ Actuals | Stale stats or parameter sniff | `UPDATE STATISTICS` / OPTION(RECOMPILE) |
| Thick arrow before filter | Non-SARGable predicate | Rewrite WHERE clause |

---

## Index Types

| Type | Key Idea |
|---|---|
| **Clustered** | Physical order of data rows. One per table. Usually the PK. |
| **Nonclustered** | Separate structure with pointer back. Up to 999. |
| **Covering** | Nonclustered + INCLUDE all query columns → no key lookup |
| **Composite** | Multiple key columns. Left-to-right column order matters. |

**Fragmentation:** < 10% = ignore, 10-30% = REORGANIZE, > 30% = REBUILD

---

## Parameter Sniffing

**What:** Plan built for first-called params, reused for all. Bad when data distribution varies widely.

**Signs:** "Fast for some users, slow for others" / estimate ≠ actual rows in plan

**Fixes:**

```sql
OPTION (RECOMPILE)          -- Best: fresh plan each time. Use for infrequent calls.
OPTION (OPTIMIZE FOR UNKNOWN) -- Cached plan using averages. Use for frequent calls.
DECLARE @local = @param     -- Local variable trick. Hides param from sniffing.
```

---

## Stored Procedure Best Practices

```sql
SET NOCOUNT ON;         -- Always. Stops "X rows affected" messages.
-- Use schema prefix:   EXEC dbo.ProcName (not just ProcName)
-- Avoid SELECT *       Always list columns explicitly
-- SARGable predicates: No functions on left side of WHERE
-- Use TRY/CATCH        For error handling with transactions
-- Avoid cursors        Set-based logic is almost always faster
```

---

## SARGable vs Non-SARGable

```sql
-- ❌ Non-SARGable (can't use index)
WHERE YEAR(OrderDate) = 2024
WHERE UPPER(Name) = 'SMITH'

-- ✅ SARGable (uses index)
WHERE OrderDate >= '2024-01-01' AND OrderDate < '2025-01-01'
WHERE Name = 'Smith'    -- with correct collation
```

---

## Async/Await — Key Points

- `await` releases the thread; it doesn't create a new one
- `async void` = only for event handlers, never otherwise
- `.Result` / `.Wait()` = deadlock risk in older ASP.NET; always async all the way down
- `Task.WhenAll()` = run multiple async tasks in parallel
- Pass `CancellationToken` through from the controller all the way to the DB call

---

## DI Lifetimes

| Lifetime | New instance | Use for |
|---|---|---|
| `Singleton` | Once per app | Config, cache, HttpClient |
| `Scoped` | Once per request | DbContext, unit of work |
| `Transient` | Every injection | Stateless utilities |

**Gotcha:** Never inject Scoped into Singleton — captive dependency bug.

---

## SOLID — One Line Each

| | Principle | Quick memory hook |
|---|---|---|
| **S** | Single Responsibility | One class, one reason to change |
| **O** | Open/Closed | Add new code; don't edit old code |
| **L** | Liskov Substitution | Subclasses must honor parent's contract |
| **I** | Interface Segregation | Many small interfaces, not one fat one |
| **D** | Dependency Inversion | Depend on interfaces, not concrete types |

---

## EF Core Gotchas

- **N+1:** Lazy-loading in a loop. Fix: `.Include()` or projection
- **Cartesian explosion:** Multiple `Include` on collections. Fix: `.AsSplitQuery()`
- **Over-fetching:** Always `.AsNoTracking()` for read-only queries
- **`Find` vs `FirstOrDefault`:** `Find` checks cache first; `FirstOrDefault` always hits DB

---

## Calling Stored Procs — Dapper Pattern

```csharp
// Query (returns rows)
var result = await connection.QueryAsync<MyType>(
    "dbo.MyProc",
    new { Param1 = val1, Param2 = val2 },
    commandType: CommandType.StoredProcedure);

// With output parameter
var p = new DynamicParameters();
p.Add("@Input",  value);
p.Add("@Output", dbType: DbType.Int32, direction: ParameterDirection.Output);
await connection.ExecuteAsync("dbo.MyProc", p, commandType: CommandType.StoredProcedure);
var output = p.Get<int>("@Output");
```
