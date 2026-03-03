-- ============================================================
-- 05 - BAD Stored Procedures (The Villains)
-- ============================================================
-- These are intentionally terrible stored procedures.
-- Each one has a real-world antipattern that causes
-- measurable performance problems on the seed data.
--
-- DO NOT USE THESE IN PRODUCTION. That's the point.
--
-- Run each scenario in SSMS with:
--   SET STATISTICS IO ON;
--   SET STATISTICS TIME ON;
-- ...then look at the execution plan.
--
-- Scenarios:
--   1. Cursor Catastrophe     — usp_Bad_RecalcOrderTotals
--   2. Parameter Sniffing     — usp_Bad_GetOrdersByCustomer
--   3. Non-SARGable Predicate — usp_Bad_GetOrdersByMonth
--   4. SELECT * Flood         — usp_Bad_GetCustomerOrderHistory
--   5. Missing Covering Index — usp_Bad_GetPendingOrders
--   6. Scalar UDF Killer      — usp_Bad_GetGoldCustomerOrders
--                             — (+ the scalar UDF itself)
-- ============================================================

USE InterviewDemoDB;
GO

-- ============================================================
-- SCENARIO 1: The Cursor Catastrophe
-- ============================================================
-- Story: The previous dev needed to recalculate TotalAmount
-- on all Pending orders after a pricing correction.
-- They knew cursors from their old Oracle days, so...
--
-- This SP loops through every Pending order ONE AT A TIME
-- and issues an UPDATE per row. On 50,000 BigCorp orders,
-- this takes forever. A single set-based UPDATE does it
-- in milliseconds.
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_Bad_RecalcOrderTotals
    @Status NVARCHAR(20) = 'Pending'
AS
BEGIN
    -- ANTIPATTERN: No SET NOCOUNT ON
    -- ANTIPATTERN: Cursor for row-by-row processing

    DECLARE @OrderID    INT;
    DECLARE @NewTotal   DECIMAL(12,2);

    DECLARE order_cursor CURSOR FOR
        SELECT OrderID
        FROM dbo.Orders
        WHERE Status = @Status;

    OPEN order_cursor;
    FETCH NEXT FROM order_cursor INTO @OrderID;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Calculate new total for this single order
        SELECT @NewTotal = SUM(oi.Quantity * oi.UnitPrice)
        FROM dbo.OrderItems oi
        WHERE oi.OrderID = @OrderID;

        -- Update just this one order
        UPDATE dbo.Orders
        SET TotalAmount = ISNULL(@NewTotal, 0)
        WHERE OrderID = @OrderID;

        FETCH NEXT FROM order_cursor INTO @OrderID;
    END;

    CLOSE order_cursor;
    DEALLOCATE order_cursor;
END;
GO


-- ============================================================
-- SCENARIO 2: The Parameter Sniffing Ghost
-- ============================================================
-- Story: Works perfectly for normal customers.
-- BigCorp (CustomerID=1, ~50,000 orders) calls this SP
-- and it grinds to a halt. Or worse — it was first called
-- with BigCorp's ID, so the plan is optimized for 50,000
-- rows, and normal customers (2 rows) now use an
-- inefficient Hash Join for their tiny result.
--
-- Run it for a regular customer first (e.g., CustomerID=50),
-- then for CustomerID=1. Watch the plan change (or not).
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_Bad_GetOrdersByCustomer
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- ANTIPATTERN: No plan hint — plan cached from first call
    -- wins forever, regardless of data distribution.

    SELECT
        o.OrderID,
        o.OrderDate,
        o.Status,
        o.TotalAmount,
        o.ShipCity,
        o.Notes  -- ← also pulling the fat Notes column (Scenario 4 bonus)
    FROM dbo.Orders o
    WHERE o.CustomerID = @CustomerID
    ORDER BY o.OrderDate DESC;
END;
GO


-- ============================================================
-- SCENARIO 3: The Non-SARGable Date Trap
-- ============================================================
-- Story: "I need orders for a specific month. Easy!"
-- The dev wraps OrderDate in YEAR() and MONTH() functions.
-- SQL Server can no longer use the IX_Orders_OrderDate index.
-- Instead of an Index Seek + 1,000 rows, you get a full
-- Index Scan of 55,000+ rows.
--
-- Run SET STATISTICS IO ON and count the logical reads.
-- Then run the fixed version and compare.
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_Bad_GetOrdersByMonth
    @Year  INT,
    @Month INT
AS
BEGIN
    SET NOCOUNT ON;

    -- ANTIPATTERN: Functions wrapped around the column in WHERE
    -- SQL Server cannot use IX_Orders_OrderDate for a Seek.
    -- It must evaluate YEAR() and MONTH() for every single row.

    SELECT
        o.OrderID,
        o.CustomerID,
        o.OrderDate,
        o.Status,
        o.TotalAmount
    FROM dbo.Orders o
    WHERE YEAR(o.OrderDate)  = @Year     -- ← Index-killer
      AND MONTH(o.OrderDate) = @Month;   -- ← Index-killer
END;
GO


-- ============================================================
-- SCENARIO 4: The SELECT * Flood
-- ============================================================
-- Story: A developer wrote a "customer order history" report.
-- They used SELECT * because it was quick. Now every call
-- drags the fat Notes column (NVARCHAR(MAX), up to ~5KB per
-- row) across the wire — even though the UI never shows it.
-- On BigCorp's 50,000 orders, that's ~250MB per query.
--
-- Watch the IO stats and compare to the fixed version.
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_Bad_GetCustomerOrderHistory
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- ANTIPATTERN: SELECT * pulls Notes (NVARCHAR(MAX)) unnecessarily.
    -- The UI only shows OrderID, OrderDate, Status, TotalAmount.

    SELECT *                       -- ← The crime
    FROM dbo.Orders o
    WHERE o.CustomerID = @CustomerID
    ORDER BY o.OrderDate DESC;
END;
GO


-- ============================================================
-- SCENARIO 5: The Key Lookup Tax
-- ============================================================
-- Story: "I indexed Status so orders dashboard loads fast."
-- IX_Orders_Status only has the Status column.
-- Every lookup must do a Key Lookup back to the clustered
-- index to fetch OrderID, CustomerID, OrderDate, TotalAmount.
-- With 10,000 Pending orders, that's 10,000 extra page reads.
--
-- Look for "Key Lookup" in the execution plan.
-- The fix adds INCLUDE columns — eliminating the lookups.
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_Bad_GetPendingOrders
    @Status NVARCHAR(20) = 'Pending'
AS
BEGIN
    SET NOCOUNT ON;

    -- IX_Orders_Status exists but only covers the Status key.
    -- SQL Server must do a Key Lookup for every other column.

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
-- SCENARIO 6: The Scalar UDF Killer
-- ============================================================
-- First, the scalar UDF that kills performance.
-- This looks innocent — just "look up a customer's tier."
-- But SQL Server calls this function ROW BY ROW for every
-- customer, and it DISABLES PARALLELISM on the whole query.
--
-- Story: "We have complex tier logic, let's put it in a
-- reusable function." Reasonable intention, terrible result.
-- ============================================================

CREATE OR ALTER FUNCTION dbo.fn_GetCustomerTier
    (@CustomerID INT)
RETURNS NVARCHAR(10)
AS
BEGIN
    -- ANTIPATTERN: Scalar UDF with a data access
    -- SQL Server calls this once per row — forces serial execution
    DECLARE @Tier NVARCHAR(10);

    SELECT @Tier = TierCode
    FROM dbo.Customers
    WHERE CustomerID = @CustomerID;

    RETURN ISNULL(@Tier, 'STANDARD');
END;
GO


CREATE OR ALTER PROCEDURE dbo.usp_Bad_GetGoldCustomerOrders
    @MinAmount DECIMAL(12,2) = 500.00
AS
BEGIN
    SET NOCOUNT ON;

    -- ANTIPATTERN: Scalar UDF in WHERE clause
    -- Evaluated row-by-row, kills parallelism, causes table scan.
    -- On 55,000+ orders this is catastrophic.

    SELECT
        o.OrderID,
        o.CustomerID,
        o.OrderDate,
        o.TotalAmount,
        dbo.fn_GetCustomerTier(o.CustomerID) AS CustomerTier   -- ← Also bad in SELECT
    FROM dbo.Orders o
    WHERE dbo.fn_GetCustomerTier(o.CustomerID) = 'GOLD'        -- ← The real killer
      AND o.TotalAmount >= @MinAmount;
END;
GO

PRINT '✅ Bad stored procedures created (all 6 antipattern scenarios).';
PRINT '   Run these with SET STATISTICS IO ON and actual execution plans to see the damage!';
GO
