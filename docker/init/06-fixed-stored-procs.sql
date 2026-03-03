-- ============================================================
-- 06 - FIXED Stored Procedures (The Heroes)
-- ============================================================
-- The corrected versions of every bad SP from script 05.
-- Each one has comments explaining WHAT changed and WHY.
--
-- Run each pair (bad then fixed) with:
--   SET STATISTICS IO ON;
--   SET STATISTICS TIME ON;
-- ...and compare logical reads and elapsed time.
-- ============================================================

USE InterviewDemoDB;
GO

-- ============================================================
-- FIX 1: Set-Based Recalculation (replaces the Cursor)
-- ============================================================
-- WHY IT'S FASTER:
--   Single UPDATE with a subquery — one pass through OrderItems,
--   one pass through Orders. SQL Server handles the join once,
--   not N times. The query can also use parallel execution.
--
-- EXPECTED IMPROVEMENT:
--   Bad:  ~50,000 individual updates = ~50,000 × (seek + update + log)
--   Good: 1 set-based update = 1 scan + 1 update + 1 log write
--   Speed difference: 100x–1000x faster on large datasets.
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_Fixed_RecalcOrderTotals
    @Status NVARCHAR(20) = 'Pending'
AS
BEGIN
    SET NOCOUNT ON;  -- ✅ Always

    -- ✅ Set-based: recalculate ALL qualifying orders in one statement
    UPDATE o
    SET o.TotalAmount = ISNULL(item_totals.NewTotal, 0)
    FROM dbo.Orders o
    INNER JOIN (
        SELECT
            oi.OrderID,
            SUM(oi.Quantity * oi.UnitPrice) AS NewTotal
        FROM dbo.OrderItems oi
        INNER JOIN dbo.Orders ord ON oi.OrderID = ord.OrderID
        WHERE ord.Status = @Status
        GROUP BY oi.OrderID
    ) item_totals ON o.OrderID = item_totals.OrderID
    WHERE o.Status = @Status;

    -- Return how many rows were updated (useful for callers)
    SELECT @@ROWCOUNT AS RowsUpdated;
END;
GO


-- ============================================================
-- FIX 2: Parameter Sniffing — OPTION(RECOMPILE) approach
-- ============================================================
-- WHY IT WORKS:
--   OPTION(RECOMPILE) at the query level forces SQL Server to
--   generate a fresh execution plan on every call using the
--   ACTUAL parameter values. No cached bad plan.
--
--   We also stop pulling the Notes column (NVARCHAR MAX) —
--   that's the Scenario 4 fix baked in for free.
--
-- TRADE-OFF:
--   This SP is called infrequently enough that recompile CPU
--   overhead is acceptable. For a high-frequency SP, use
--   usp_Fixed_GetOrdersByCustomer_HighFreq below instead.
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_Fixed_GetOrdersByCustomer
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        o.OrderID,
        o.OrderDate,
        o.Status,
        o.TotalAmount,
        o.ShipCity
        -- ✅ Notes intentionally removed — callers never used it
    FROM dbo.Orders o
    WHERE o.CustomerID = @CustomerID
    ORDER BY o.OrderDate DESC
    OPTION (RECOMPILE);  -- ✅ Fresh plan every time, based on actual @CustomerID value
END;
GO


-- ============================================================
-- FIX 2b: Parameter Sniffing — OPTIMIZE FOR UNKNOWN
-- (Use this for high-frequency calls where RECOMPILE is costly)
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_Fixed_GetOrdersByCustomer_HighFreq
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        o.OrderID,
        o.OrderDate,
        o.Status,
        o.TotalAmount,
        o.ShipCity
    FROM dbo.Orders o
    WHERE o.CustomerID = @CustomerID
    ORDER BY o.OrderDate DESC
    OPTION (OPTIMIZE FOR (@CustomerID UNKNOWN));
    -- ✅ Plan is cached once, based on AVERAGE statistics.
    -- Not perfect for extremes (BigCorp vs tiny customers),
    -- but a reasonable middle ground for frequent calls.
END;
GO


-- ============================================================
-- FIX 2c: Local Variable Trick (alternative, no query hint syntax)
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_Fixed_GetOrdersByCustomer_LocalVar
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- ✅ SQL Server cannot sniff a local variable's value.
    -- Plan is built using average statistics — same effect as OPTIMIZE FOR UNKNOWN.
    DECLARE @LocalID INT = @CustomerID;

    SELECT
        o.OrderID,
        o.OrderDate,
        o.Status,
        o.TotalAmount,
        o.ShipCity
    FROM dbo.Orders o
    WHERE o.CustomerID = @LocalID
    ORDER BY o.OrderDate DESC;
END;
GO


-- ============================================================
-- FIX 3: SARGable Date Filter
-- ============================================================
-- WHY IT'S FASTER:
--   We replace YEAR(OrderDate)=@Y AND MONTH(OrderDate)=@M
--   with a range predicate: OrderDate >= @Start AND < @End
--   The column is now "bare" on the left side — SQL Server
--   can seek directly into IX_Orders_OrderDate.
--
-- EXPECTED IMPROVEMENT:
--   Bad:  Index Scan of 55,000+ rows → logical reads ~800+
--   Good: Index Seek of ~4,600 rows  → logical reads ~50
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_Fixed_GetOrdersByMonth
    @Year  INT,
    @Month INT
AS
BEGIN
    SET NOCOUNT ON;

    -- ✅ Calculate range boundaries in code — NOT on the column itself
    DECLARE @StartDate DATETIME2 = DATEFROMPARTS(@Year, @Month, 1);
    DECLARE @EndDate   DATETIME2 = DATEADD(MONTH, 1, @StartDate);

    SELECT
        o.OrderID,
        o.CustomerID,
        o.OrderDate,
        o.Status,
        o.TotalAmount
    FROM dbo.Orders o
    WHERE o.OrderDate >= @StartDate   -- ✅ SARGable — column is bare
      AND o.OrderDate <  @EndDate;    -- ✅ SARGable — exclusive upper bound avoids end-of-day edge cases
END;
GO


-- ============================================================
-- FIX 4: Explicit Column List (no SELECT *)
-- ============================================================
-- WHY IT'S FASTER:
--   Notes is NVARCHAR(MAX). For BigCorp's 50,000 orders,
--   each row with a Notes value is ~800 bytes.
--   SELECT * pulls all those bytes even if the caller never
--   uses them. Explicit SELECT skips that entirely.
--
-- BONUS: Explicit columns are also more resilient to schema
--   changes — adding a new column won't silently break callers.
--
-- EXPECTED IMPROVEMENT:
--   IO with SELECT *: logical reads include LOB pages (large objects)
--   IO with explicit: LOB reads drop to 0 (Notes not accessed at all)
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_Fixed_GetCustomerOrderHistory
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- ✅ Only the 5 columns the UI actually displays
    SELECT
        o.OrderID,
        o.OrderDate,
        o.Status,
        o.TotalAmount,
        o.ShipCity
    FROM dbo.Orders o
    WHERE o.CustomerID = @CustomerID
    ORDER BY o.OrderDate DESC;
END;
GO


-- ============================================================
-- FIX 5: Covering Index (add INCLUDE columns)
-- ============================================================
-- The fix is partly in SQL (drop and recreate the index)
-- and the SP itself doesn't change — the index does the work.
--
-- Step A: Drop the narrow index from script 04
-- Step B: Recreate it as a covering index with INCLUDE
-- Step C: Run usp_Bad_GetPendingOrders vs usp_Fixed_GetPendingOrders
--         and watch Key Lookup disappear from the plan.
-- ============================================================

-- ✅ STEP A: Drop the old narrow index
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Orders_Status' AND object_id = OBJECT_ID('dbo.Orders'))
    DROP INDEX IX_Orders_Status ON dbo.Orders;
GO

-- ✅ STEP B: Recreate as covering index
-- Now includes all columns the SP needs — no Key Lookup required.
CREATE NONCLUSTERED INDEX IX_Orders_Status_Covering
    ON dbo.Orders (Status)
    INCLUDE (OrderID, CustomerID, OrderDate, TotalAmount);
    -- Notes is intentionally NOT included — it's fat and callers shouldn't need it
GO

-- The SP is identical — the index does all the work
CREATE OR ALTER PROCEDURE dbo.usp_Fixed_GetPendingOrders
    @Status NVARCHAR(20) = 'Pending'
AS
BEGIN
    SET NOCOUNT ON;

    -- ✅ Same query — but IX_Orders_Status_Covering now covers it fully.
    -- No Key Lookup will appear in the execution plan.
    SELECT
        o.OrderID,
        o.CustomerID,
        o.OrderDate,
        o.TotalAmount
    FROM dbo.Orders o
    WHERE o.Status = @Status
    ORDER BY o.OrderDate DESC;
END;
GO


-- ============================================================
-- FIX 6: Inline the Tier Logic (eliminates Scalar UDF)
-- ============================================================
-- WHY IT'S FASTER:
--   1. No function call overhead per row
--   2. Query can use parallelism again
--   3. SQL Server can use statistics on TierCode column
--   4. INNER JOIN on Customers uses IX_Customers index
--
-- ALTERNATIVE: Inline Table-Valued Function (iTVF)
--   For complex multi-step logic that must be reusable,
--   an iTVF is much faster than a scalar UDF.
--   See usp_Fixed_GetGoldCustomerOrders_iTVF below.
--
-- EXPECTED IMPROVEMENT:
--   Bad:  Serial execution, fn_ called once per row (~55K calls)
--   Good: Parallel JOIN, statistics used, 10-100x faster
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_Fixed_GetGoldCustomerOrders
    @MinAmount DECIMAL(12,2) = 500.00
AS
BEGIN
    SET NOCOUNT ON;

    -- ✅ JOIN to Customers and filter directly on TierCode column
    -- SQL Server uses statistics on the column, can parallelize,
    -- can use indexes — much better than the scalar UDF approach.
    SELECT
        o.OrderID,
        o.CustomerID,
        o.OrderDate,
        o.TotalAmount,
        c.TierCode AS CustomerTier
    FROM dbo.Orders o
    INNER JOIN dbo.Customers c ON o.CustomerID = c.CustomerID
    WHERE c.TierCode = 'GOLD'          -- ✅ Direct column filter — SARGable
      AND o.TotalAmount >= @MinAmount;
END;
GO


-- ============================================================
-- BONUS: Inline Table-Valued Function alternative for complex tier logic
-- ============================================================
-- Use this pattern when the logic IS genuinely complex and
-- needs to be reused, but you still can't afford scalar UDF overhead.

CREATE OR ALTER FUNCTION dbo.fn_GetCustomerTierInline
    (@CustomerID INT)
RETURNS TABLE
AS
RETURN (
    -- ✅ Inline TVF — SQL Server inlines this like a view.
    -- No row-by-row execution. Can be JOINed and planned optimally.
    SELECT TierCode
    FROM dbo.Customers
    WHERE CustomerID = @CustomerID
);
GO


-- ============================================================
-- REFERENCE: The SELF JOIN — org chart query
-- (Not a bug fix, but a useful demo for the joins section)
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_GetOrgChart
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.EmployeeID,
        e.FullName       AS Employee,
        e.JobTitle,
        e.Department,
        e.Salary,
        m.FullName       AS Manager,
        m.JobTitle       AS ManagerTitle,
        DATEDIFF(YEAR, e.HireDate, GETDATE()) AS YearsAtCompany
    FROM dbo.Employees e
    LEFT JOIN dbo.Employees m ON e.ManagerID = m.EmployeeID  -- Self join!
    ORDER BY m.FullName, e.FullName;
END;
GO


-- ============================================================
-- REFERENCE: LEFT JOIN anti-join — customers with no orders
-- (Demo for the joins section)
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_GetCustomersWithNoOrders
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        c.CustomerID,
        c.FirstName,
        c.LastName,
        c.Email,
        c.TierCode,
        c.CreatedDate
    FROM dbo.Customers c
    LEFT JOIN dbo.Orders o ON c.CustomerID = o.CustomerID
    WHERE o.OrderID IS NULL   -- ✅ Anti-join: customers with NO orders
    ORDER BY c.CreatedDate DESC;
END;
GO


-- ============================================================
-- REFERENCE: FULL OUTER JOIN — order reconciliation
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_ReconcileOrders
    @StartDate DATE,
    @EndDate   DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Find orders with no items (data quality issue)
    -- AND items with no matching order (orphaned data)
    SELECT
        o.OrderID,
        o.CustomerID,
        o.TotalAmount AS OrderTotal,
        oi.OrderID    AS ItemOrderID,
        SUM(oi.LineTotal) AS ItemsTotal,
        CASE
            WHEN o.OrderID IS NULL  THEN 'Orphaned Items — No Order'
            WHEN oi.OrderID IS NULL THEN 'Order With No Items'
            ELSE                         'OK'
        END AS ReconciliationStatus
    FROM dbo.Orders o
    FULL OUTER JOIN dbo.OrderItems oi ON o.OrderID = oi.OrderID
    WHERE (o.OrderDate >= @StartDate AND o.OrderDate < DATEADD(DAY, 1, @EndDate))
       OR o.OrderID IS NULL
    GROUP BY o.OrderID, o.CustomerID, o.TotalAmount, oi.OrderID
    HAVING o.OrderID IS NULL OR oi.OrderID IS NULL;  -- Only show problem rows
END;
GO

PRINT '✅ Fixed stored procedures created.';
PRINT '   Compare each usp_Bad_* vs usp_Fixed_* pair with SET STATISTICS IO ON.';
GO
