# SQL Joins

Joins are one of the most fundamental SQL concepts and almost always come up in interviews. You should be able to explain each type clearly, draw a Venn diagram in your head, and give a real-world use case for each.

---

## Setup: Sample Tables

Throughout this page, we'll use these two tables as examples:

```sql
-- Customers table
CREATE TABLE Customers (
    CustomerID   INT PRIMARY KEY,
    Name         NVARCHAR(100),
    Email        NVARCHAR(200)
);

-- Orders table
CREATE TABLE Orders (
    OrderID      INT PRIMARY KEY,
    CustomerID   INT,           -- FK to Customers
    OrderDate    DATE,
    TotalAmount  DECIMAL(10,2)
);
```

**Sample data:**

| CustomerID | Name    |
|-----------|---------|
| 1         | Alice   |
| 2         | Bob     |
| 3         | Charlie |
| 4         | Diana   |

| OrderID | CustomerID | TotalAmount |
|---------|-----------|-------------|
| 101     | 1         | 50.00       |
| 102     | 1         | 30.00       |
| 103     | 2         | 75.00       |
| 104     | 5         | 20.00  *(orphan — no matching customer)* |

---

## INNER JOIN

Returns **only rows where there is a match in both tables**. Non-matching rows from either table are excluded.

```sql
SELECT
    c.CustomerID,
    c.Name,
    o.OrderID,
    o.TotalAmount
FROM Customers c
INNER JOIN Orders o ON c.CustomerID = o.CustomerID;
```

**Result:**

| CustomerID | Name  | OrderID | TotalAmount |
|-----------|-------|---------|-------------|
| 1         | Alice | 101     | 50.00       |
| 1         | Alice | 102     | 30.00       |
| 2         | Bob   | 103     | 75.00       |

Alice returns twice (two orders). Charlie and Diana have no orders — they're excluded. Order 104 has no matching customer — it's excluded too.

**When to use INNER JOIN:**
- When you only want records that exist in both tables
- "Give me all customers who have placed at least one order"
- Most common join type in everyday queries

---

## LEFT JOIN (LEFT OUTER JOIN)

Returns **all rows from the left table**, plus matching rows from the right table. Where there's no match on the right, `NULL` is returned for right-table columns.

```sql
SELECT
    c.CustomerID,
    c.Name,
    o.OrderID,
    o.TotalAmount
FROM Customers c
LEFT JOIN Orders o ON c.CustomerID = o.CustomerID;
```

**Result:**

| CustomerID | Name    | OrderID | TotalAmount |
|-----------|---------|---------|-------------|
| 1         | Alice   | 101     | 50.00       |
| 1         | Alice   | 102     | 30.00       |
| 2         | Bob     | 103     | 75.00       |
| 3         | Charlie | NULL    | NULL        |
| 4         | Diana   | NULL    | NULL        |

Charlie and Diana appear with NULLs because they haven't placed orders.

**When to use LEFT JOIN:**
- When you want all records from the left (primary) table regardless of whether they have related data
- "Give me all customers and their orders (if any)"
- Reporting where you want to show zero/null for missing related records
- The **most commonly used outer join** — prefer left joins over right joins for readability

### Finding records with NO match (anti-join pattern)

```sql
-- Find customers who have NEVER placed an order
SELECT c.CustomerID, c.Name
FROM Customers c
LEFT JOIN Orders o ON c.CustomerID = o.CustomerID
WHERE o.OrderID IS NULL;
```

This is a classic interview pattern — use a LEFT JOIN then filter on NULL in the right table.

---

## RIGHT JOIN (RIGHT OUTER JOIN)

Returns **all rows from the right table**, plus matching rows from the left table. Where there's no match on the left, `NULL` is returned for left-table columns.

```sql
SELECT
    c.CustomerID,
    c.Name,
    o.OrderID,
    o.TotalAmount
FROM Customers c
RIGHT JOIN Orders o ON c.CustomerID = o.CustomerID;
```

**Result:**

| CustomerID | Name  | OrderID | TotalAmount |
|-----------|-------|---------|-------------|
| 1         | Alice | 101     | 50.00       |
| 1         | Alice | 102     | 30.00       |
| 2         | Bob   | 103     | 75.00       |
| NULL      | NULL  | 104     | 20.00       |

Order 104 appears with NULLs because its CustomerID (5) doesn't exist in Customers.

**When to use RIGHT JOIN:**
- When you want all records from the right table regardless of left-table matches
- "Give me all orders and their customer info (if the customer still exists)"
- In practice: **you can almost always rewrite a RIGHT JOIN as a LEFT JOIN** by swapping table order. Many teams ban RIGHT JOINs for readability consistency.

!!! note "Pro tip for interviews"
    Mention that you typically convert RIGHT JOINs to LEFT JOINs by just swapping the table order. Interviewers appreciate knowing you think about code readability.

---

## FULL OUTER JOIN

Returns **all rows from both tables**. Where there's no match, `NULL` fills in the gaps from the other table.

```sql
SELECT
    c.CustomerID,
    c.Name,
    o.OrderID,
    o.TotalAmount
FROM Customers c
FULL OUTER JOIN Orders o ON c.CustomerID = o.CustomerID;
```

**Result:**

| CustomerID | Name    | OrderID | TotalAmount |
|-----------|---------|---------|-------------|
| 1         | Alice   | 101     | 50.00       |
| 1         | Alice   | 102     | 30.00       |
| 2         | Bob     | 103     | 75.00       |
| 3         | Charlie | NULL    | NULL        |
| 4         | Diana   | NULL    | NULL        |
| NULL      | NULL    | 104     | 20.00       |

**When to use FULL OUTER JOIN:**
- Data reconciliation — "show me everything from both tables, highlight the gaps"
- Comparing two datasets to find records missing from either side
- Syncing or auditing data between two systems
- Relatively rare in day-to-day application queries

---

## CROSS JOIN

Returns the **Cartesian product** — every row in the left table matched with every row in the right table. No `ON` condition needed (or useful).

```sql
SELECT c.Name, p.ProductName
FROM Customers c
CROSS JOIN Products p;
```

If Customers has 4 rows and Products has 10 rows, you get 40 rows.

**When to use CROSS JOIN:**
- Generating test data combinations
- Building a calendar grid (cross join Months × Days)
- Pricing matrices (e.g., all size/color combinations for a product)
- Rarely used in production application code

---

## SELF JOIN

A table joined to **itself**. Uses table aliases to distinguish the two "sides".

```sql
-- Employees with their managers (both in the same table)
SELECT
    e.EmployeeID,
    e.Name         AS Employee,
    m.Name         AS Manager
FROM Employees e
LEFT JOIN Employees m ON e.ManagerID = m.EmployeeID;
```

**When to use SELF JOIN:**
- Hierarchical data in a single table (org charts, category trees)
- Comparing rows within the same table
- Finding duplicate records

---

## Visual Summary (Venn Diagram Mental Model)

```
INNER JOIN:      Only the overlapping center
LEFT JOIN:       All of left circle + overlap
RIGHT JOIN:      All of right circle + overlap
FULL OUTER JOIN: Both circles entirely
```

---

## Quick Decision Guide

| Scenario | Join Type |
|---|---|
| Only want records with matches on both sides | INNER JOIN |
| Want all from left, optionally matched to right | LEFT JOIN |
| Find records in left table with NO match in right | LEFT JOIN + WHERE right.col IS NULL |
| Want all from right, optionally matched to left | RIGHT JOIN (or flip to LEFT JOIN) |
| Want everything from both tables | FULL OUTER JOIN |
| Comparing two datasets for gaps | FULL OUTER JOIN |
| Every combination of two tables | CROSS JOIN |
| Hierarchical data in one table | SELF JOIN |

---

## Performance Notes on Joins

- **Always join on indexed columns** — joining on non-indexed columns causes table scans
- **INNER JOIN is generally fastest** because it reduces row count early
- **Avoid joining on functions** — `ON YEAR(o.OrderDate) = 2024` will not use an index; use `ON o.OrderDate >= '2024-01-01' AND o.OrderDate < '2025-01-01'` instead
- **Join order matters less than you think** — the query optimizer usually reorders joins, but bad join conditions still cause problems
- Check the execution plan for **Hash Match** vs **Nested Loops** vs **Merge Join** operators to understand how SQL Server is executing your join
