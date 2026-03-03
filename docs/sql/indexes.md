# SQL Server Indexes

Indexes are the single most impactful tool for query performance. If an interviewer asks "a stored procedure is running slow, what do you do?" — missing indexes is almost always the first thing to check.

---

## What Is an Index?

An index is a separate data structure that SQL Server maintains alongside a table to allow fast data lookup without scanning every row. Think of it like a book's index — instead of reading every page to find a topic, you go to the index, find the page number, and jump straight there.

**The trade-off:** Indexes speed up reads but slow down writes (INSERT/UPDATE/DELETE must maintain the index). More indexes = faster reads, slower writes, more storage.

---

## Clustered Index

A clustered index **determines the physical sort order of data in the table**. The data rows themselves are stored in the index leaf pages — it IS the table.

- **Only one per table** (the data can only be sorted one way)
- Usually created on the **primary key** (SQL Server does this automatically by default)
- If no clustered index exists, the table is a **heap** (unsorted, can be slow for large range scans)

```sql
-- Creating a clustered index on OrderDate (instead of the default PK)
CREATE CLUSTERED INDEX IX_Orders_OrderDate
ON Orders (OrderDate);

-- SQL Server auto-creates a clustered index when you define a PRIMARY KEY:
CREATE TABLE Orders (
    OrderID   INT PRIMARY KEY,  -- ← Automatically creates clustered index on OrderID
    ...
);
```

**When to put the clustered index somewhere other than the PK:**
- On a date column if most queries filter or range-scan by date
- Tables that are primarily read by date ranges (logs, audit tables, time-series data)

---

## Nonclustered Index

A nonclustered index is a **separate structure** that contains the indexed column(s) plus a pointer (the row locator) back to the actual data row.

- You can have **up to 999 nonclustered indexes** per table (in practice, keep it reasonable — 5-10 is typical)
- The leaf pages contain the index key + a pointer to the clustered index key (or heap RID)
- Looking up data via a nonclustered index may require a **key lookup** (bookmark lookup) to retrieve non-indexed columns from the base table

```sql
-- Basic nonclustered index
CREATE NONCLUSTERED INDEX IX_Orders_CustomerID
ON Orders (CustomerID);

-- Composite nonclustered index
CREATE NONCLUSTERED INDEX IX_Orders_Customer_Date
ON Orders (CustomerID, OrderDate);
```

---

## Covering Index (INCLUDE columns)

A covering index is a nonclustered index that contains **all the columns a specific query needs**, eliminating the need for a key lookup back to the base table.

```sql
-- Query we want to cover:
SELECT OrderID, OrderDate, TotalAmount
FROM Orders
WHERE CustomerID = @CustomerID;

-- Index that "covers" this query — CustomerID is the key, rest are included
CREATE NONCLUSTERED INDEX IX_Orders_CustomerID_Covering
ON Orders (CustomerID)
INCLUDE (OrderDate, TotalAmount, OrderID);
```

The `INCLUDE` columns are stored only at the leaf level — they're not part of the sort key, so they don't affect index ordering but do satisfy column lookups.

**Why covering indexes matter:** A key lookup (going from the nonclustered index back to the clustered index to fetch extra columns) is expensive — especially at scale. Covering the query eliminates this extra I/O.

!!! tip "Interview answer"
    "I'd check the execution plan for **Key Lookup** operators — these indicate the query needs columns not in the index. Adding those columns via INCLUDE turns it into a covering index and eliminates the key lookup."

---

## Composite Index

A composite (multi-column) index has **multiple key columns**. The order of columns matters significantly.

```sql
CREATE NONCLUSTERED INDEX IX_Orders_Customer_Status_Date
ON Orders (CustomerID, Status, OrderDate);
```

**Column order rules:**

1. Put the **most selective, equality-filtered** column first
2. Put **range-filtered** columns last
3. The index supports queries that filter on the **leading columns** (left-to-right)

```sql
-- ✅ This query fully uses IX_Orders_Customer_Status_Date
WHERE CustomerID = 5 AND Status = 'Pending' AND OrderDate > '2024-01-01'

-- ✅ This also uses it (leading columns)
WHERE CustomerID = 5 AND Status = 'Pending'

-- ✅ This uses it too
WHERE CustomerID = 5

-- ❌ This does NOT use the index efficiently — CustomerID is missing
WHERE Status = 'Pending' AND OrderDate > '2024-01-01'
```

---

## Index Fragmentation

Over time, as rows are inserted, updated, and deleted, index pages become fragmented — data is out of physical order, pages have gaps (internal fragmentation) or the logical order doesn't match physical order (external fragmentation).

**How to check fragmentation:**

```sql
SELECT
    OBJECT_NAME(ips.object_id) AS TableName,
    i.name                     AS IndexName,
    ips.avg_fragmentation_in_percent,
    ips.page_count
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
INNER JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
WHERE ips.avg_fragmentation_in_percent > 10
ORDER BY ips.avg_fragmentation_in_percent DESC;
```

**Fragmentation thresholds (general guidelines):**

| Fragmentation | Action |
|---|---|
| < 10% | Leave it alone |
| 10% – 30% | `REORGANIZE` (online, low impact) |
| > 30% | `REBUILD` (faster, can be online in Enterprise) |

```sql
-- Reorganize (online operation, low impact)
ALTER INDEX IX_Orders_CustomerID ON Orders REORGANIZE;

-- Rebuild (more thorough, can lock table in Standard edition)
ALTER INDEX IX_Orders_CustomerID ON Orders REBUILD;

-- Rebuild all indexes on a table
ALTER INDEX ALL ON Orders REBUILD;
```

---

## Finding Missing Indexes

SQL Server tracks queries that would have benefited from an index and stores this in DMVs:

```sql
SELECT TOP 10
    mid.statement                           AS TableName,
    migs.avg_total_user_cost * migs.avg_user_impact * (migs.user_seeks + migs.user_scans) AS ImprovementMeasure,
    'CREATE INDEX [IX_' + REPLACE(REPLACE(mid.statement, '[', ''), ']', '') + '_'
        + REPLACE(REPLACE(REPLACE(ISNULL(mid.equality_columns, ''), '[', ''), ']', ''), ', ', '_')
        + CASE WHEN mid.inequality_columns IS NOT NULL THEN '_' + REPLACE(REPLACE(REPLACE(mid.inequality_columns, '[', ''), ']', ''), ', ', '_') ELSE '' END
        + '] ON ' + mid.statement
        + ' (' + ISNULL(mid.equality_columns, '')
        + CASE WHEN mid.equality_columns IS NOT NULL AND mid.inequality_columns IS NOT NULL THEN ', ' ELSE '' END
        + ISNULL(mid.inequality_columns, '') + ')'
        + ISNULL(' INCLUDE (' + mid.included_columns + ')', '') AS CreateIndexStatement
FROM sys.dm_db_missing_index_group_stats migs
INNER JOIN sys.dm_db_missing_index_groups mig ON migs.group_id = mig.index_group_id
INNER JOIN sys.dm_db_missing_index_details mid ON mig.index_handle = mid.index_handle
ORDER BY ImprovementMeasure DESC;
```

!!! warning "Use missing index suggestions as a guide, not gospel"
    SQL Server sometimes suggests redundant or overlapping indexes. Always review suggestions and consider the write overhead before creating new indexes.

---

## Unused Indexes

Indexes that are never used still need to be maintained on every write:

```sql
SELECT
    OBJECT_NAME(i.object_id) AS TableName,
    i.name                   AS IndexName,
    ius.user_seeks,
    ius.user_scans,
    ius.user_lookups,
    ius.user_updates
FROM sys.indexes i
LEFT JOIN sys.dm_db_index_usage_stats ius
    ON i.object_id = ius.object_id AND i.index_id = ius.index_id AND ius.database_id = DB_ID()
WHERE OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
  AND i.index_id > 1  -- skip clustered index
  AND (ius.user_seeks IS NULL OR ius.user_seeks = 0)
  AND (ius.user_scans IS NULL OR ius.user_scans = 0)
ORDER BY ius.user_updates DESC;
```

Note: DMV stats reset on SQL Server restart — only meaningful after uptime period.

---

## Index Types Summary

| Type | Key Idea | Per Table Limit |
|---|---|---|
| Clustered | IS the table's physical order | 1 |
| Nonclustered | Separate structure, pointer back to data | 999 |
| Covering | Nonclustered + INCLUDE all needed columns | Part of nonclustered limit |
| Composite | Multiple key columns, order matters | Part of nonclustered limit |
| Unique | Enforces uniqueness, can be clustered or not | — |
| Filtered | Partial index with a WHERE clause | — |
| Columnstore | Column-oriented, great for analytics/aggregations | — |
| Full-Text | For text search (CONTAINS, FREETEXT) | — |

---

## Interview Quick-Fire Answers

**Q: Difference between clustered and nonclustered index?**
Clustered determines physical data order (one per table, IS the table). Nonclustered is a separate structure with a pointer back to the data (up to 999 per table).

**Q: What is a covering index?**
A nonclustered index where all columns needed by a query are either key columns or INCLUDE columns — so SQL Server can satisfy the query entirely from the index without going back to the base table (eliminates key lookups).

**Q: How do you check if an index is fragmented?**
Query `sys.dm_db_index_physical_stats`. Reorganize at 10-30% fragmentation, rebuild above 30%.

**Q: What's a heap?**
A table with no clustered index. Data is stored with no physical ordering. Can cause performance problems for range queries and large scans.
