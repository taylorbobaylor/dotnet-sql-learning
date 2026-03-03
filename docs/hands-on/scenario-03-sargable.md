# Scenario 3: The Non-SARGable Date Trap

> **Antipattern:** Wrapping a column in a function in the WHERE clause (`YEAR(OrderDate) = 2024`).
> **Symptom:** An index exists, but the execution plan shows a full Index Scan.
> **Fix:** Rewrite the predicate so the column is bare — use a range instead.

---

## The Story

The monthly reports dashboard had a filter for "orders in a given month." The developer wrote what felt natural:

```sql
WHERE YEAR(OrderDate) = @Year AND MONTH(OrderDate) = @Month
```

An index on `OrderDate` exists. The query should be fast. But the execution plan shows a full **Index Scan** of 55,000+ rows. Every single order is read and evaluated.

The index is useless because SQL Server can't pre-evaluate `YEAR()` — it doesn't know which index pages to skip.

---

## Understanding SARGability

**SARG** = Search ARGument. A predicate is SARGable if SQL Server can use it to **seek** into an index.

The rule is simple: **the column must appear alone on one side of the comparison.**

```sql
-- ❌ Non-SARGable — column is inside a function
WHERE YEAR(OrderDate)     = 2024
WHERE CONVERT(DATE, OrderDate) = '2024-06-01'
WHERE UPPER(LastName)     = 'SMITH'
WHERE OrderID + 0         = @ID        -- arithmetic on the column

-- ✅ SARGable — column is bare
WHERE OrderDate >= '2024-01-01' AND OrderDate < '2025-01-01'
WHERE OrderDate >= '2024-06-01' AND OrderDate < '2024-06-02'
WHERE LastName  = 'Smith'              -- use correct collation
WHERE OrderID   = @ID
```

---

## The Bad SP

```sql
CREATE OR ALTER PROCEDURE dbo.usp_Bad_GetOrdersByMonth
    @Year  INT,
    @Month INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT o.OrderID, o.CustomerID, o.OrderDate, o.Status, o.TotalAmount
    FROM dbo.Orders o
    WHERE YEAR(o.OrderDate)  = @Year    -- ❌ Index-killer
      AND MONTH(o.OrderDate) = @Month;  -- ❌ Index-killer
END;
```

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

**Step 2:** Run the bad version:

```sql
EXEC dbo.usp_Bad_GetOrdersByMonth @Year = 2024, @Month = 6;
```

In the **Messages/Output tab**, note the logical reads:
```
Table 'Orders'. Scan count 1, logical reads [something large]
```

In the **Execution Plan tab**, look for **Index Scan** on Orders — SQL Server is reading every single row and applying the `YEAR()`/`MONTH()` functions per-row.

**Step 3:** Run the fixed version:

```sql
EXEC dbo.usp_Fixed_GetOrdersByMonth @Year = 2024, @Month = 6;
```

The Messages/Output now shows dramatically fewer logical reads. The execution plan shows **Index Seek** — jumping directly to the matching date range. Hover the seek node to confirm the **Seek Predicate** shows a range (`OrderDate >= '2024-06-01' AND OrderDate < '2024-07-01'`) rather than a function call.

---

## Comparing the Plans

**Bad execution plan:**

```
Index Scan [IX_Orders_OrderDate]   ← Reading ALL rows, applying function per-row
  ↳ Predicate: YEAR(OrderDate) = 2024 AND MONTH(OrderDate) = 6
  ↳ Estimated Rows: 55,000   Actual Rows: 4,600  (SQL doesn't know what YEAR() returns)
```

**Fixed execution plan:**

```
Index Seek [IX_Orders_OrderDate]   ← Jump directly to the date range
  ↳ Seek Predicate: OrderDate >= '2024-06-01' AND OrderDate < '2024-07-01'
  ↳ Estimated Rows: 4,600    Actual Rows: 4,600  (statistics match)
```

---

## The Fixed SP

```sql
CREATE OR ALTER PROCEDURE dbo.usp_Fixed_GetOrdersByMonth
    @Year  INT,
    @Month INT
AS
BEGIN
    SET NOCOUNT ON;

    -- ✅ Calculate boundaries in T-SQL — keep the column bare in the WHERE
    DECLARE @StartDate DATETIME2 = DATEFROMPARTS(@Year, @Month, 1);
    DECLARE @EndDate   DATETIME2 = DATEADD(MONTH, 1, @StartDate);

    SELECT o.OrderID, o.CustomerID, o.OrderDate, o.Status, o.TotalAmount
    FROM dbo.Orders o
    WHERE o.OrderDate >= @StartDate   -- ✅ Column is bare — SARGable
      AND o.OrderDate <  @EndDate;    -- ✅ Exclusive upper bound (no end-of-day edge cases)
END;
```

Note the `<` vs `<=` on the end date. Using `< first-of-next-month` handles every time of day on the last day of the month perfectly — no `23:59:59.997` edge case bugs.

---

## More Non-SARGable Examples You'll Encounter

```sql
-- ❌ Implicit conversion (varchar column filtered with integer)
WHERE StringID = 12345                 -- SQL casts every row, kills index

-- ❌ ISNULL wrapping the column
WHERE ISNULL(Status, 'Unknown') = 'Unknown'
-- ✅ Fix:
WHERE Status IS NULL OR Status = 'Unknown'

-- ❌ CAST on the column
WHERE CAST(OrderID AS NVARCHAR) = @StringParam
-- ✅ Fix: cast the parameter, not the column
WHERE OrderID = CAST(@StringParam AS INT)

-- ❌ Negation of a function
WHERE CHARINDEX('error', LogMessage) > 0   -- full scan
-- ✅ Fix: Use Full-Text index with CONTAINS if this is a core query
WHERE CONTAINS(LogMessage, 'error')
```

---

## Interview Answer

> "SARGable stands for Search ARGument Able — it means the predicate can be used to seek into an index rather than scanning. The key rule is that the column must appear alone on one side of the comparison. Wrapping a column in a function like `YEAR(OrderDate)` or `UPPER(Name)` makes the predicate non-SARGable because SQL Server has to evaluate the function for every single row. The fix is to keep the column bare and move the logic to the other side — calculate the date boundaries in variables and use a range predicate. It goes from an Index Scan of 55,000 rows to an Index Seek of 4,600, which you can see immediately as a 10× drop in logical reads."
