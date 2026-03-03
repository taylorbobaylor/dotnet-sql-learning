-- ============================================================
-- 04 - Baseline Indexes
-- ============================================================
-- These are the GOOD indexes the database should have.
-- In the scenario exercises you'll drop specific ones
-- to see what breaks, then recreate them to fix it.
--
-- Each index is commented with which scenario uses it.
-- ============================================================

USE InterviewDemoDB;
GO

-- -------------------------------------------------------
-- Customers
-- -------------------------------------------------------

-- Scenario 5 / general: Look up customer by email
CREATE NONCLUSTERED INDEX IX_Customers_Email
    ON dbo.Customers (Email)
    INCLUDE (FirstName, LastName, IsVIP, TierCode);
GO

-- Scenario 3: Filter by City — intentionally NOT covering (for the exercise)
CREATE NONCLUSTERED INDEX IX_Customers_City
    ON dbo.Customers (City);
GO

-- -------------------------------------------------------
-- Orders  (most scenarios touch this table)
-- -------------------------------------------------------

-- General FK index — always needed
CREATE NONCLUSTERED INDEX IX_Orders_CustomerID
    ON dbo.Orders (CustomerID)
    INCLUDE (OrderDate, Status, TotalAmount);
GO

-- Scenario 3: Date filter index — key to SARGable lesson
CREATE NONCLUSTERED INDEX IX_Orders_OrderDate
    ON dbo.Orders (OrderDate)
    INCLUDE (CustomerID, Status, TotalAmount);
GO

-- Scenario 5: Intentionally NARROW (no INCLUDE) — creates key lookup
-- We'll upgrade this in the scenario exercise
CREATE NONCLUSTERED INDEX IX_Orders_Status
    ON dbo.Orders (Status);
    -- NOTE: Deliberately missing INCLUDE columns — this is the bug for Scenario 5
GO

-- -------------------------------------------------------
-- OrderItems
-- -------------------------------------------------------
CREATE NONCLUSTERED INDEX IX_OrderItems_OrderID
    ON dbo.OrderItems (OrderID)
    INCLUDE (ProductID, Quantity, UnitPrice, LineTotal);
GO

CREATE NONCLUSTERED INDEX IX_OrderItems_ProductID
    ON dbo.OrderItems (ProductID)
    INCLUDE (OrderID, Quantity, LineTotal);
GO

-- -------------------------------------------------------
-- OrderAuditLog
-- -------------------------------------------------------
CREATE NONCLUSTERED INDEX IX_OrderAuditLog_OrderID
    ON dbo.OrderAuditLog (OrderID)
    INCLUDE (ChangedAt, ChangedBy, OldStatus, NewStatus);
    -- NOTE: ChangeDetails (the fat NVARCHAR(MAX)) is intentionally NOT included
GO

-- -------------------------------------------------------
-- Employees
-- -------------------------------------------------------
CREATE NONCLUSTERED INDEX IX_Employees_ManagerID
    ON dbo.Employees (ManagerID)
    INCLUDE (FullName, Department, JobTitle);
GO

-- -------------------------------------------------------
-- Update statistics after bulk insert
-- -------------------------------------------------------
UPDATE STATISTICS dbo.Customers WITH FULLSCAN;
UPDATE STATISTICS dbo.Products  WITH FULLSCAN;
UPDATE STATISTICS dbo.Orders    WITH FULLSCAN;
UPDATE STATISTICS dbo.OrderItems WITH FULLSCAN;
UPDATE STATISTICS dbo.Employees WITH FULLSCAN;
GO

PRINT '✅ Baseline indexes created and statistics updated.';
GO
