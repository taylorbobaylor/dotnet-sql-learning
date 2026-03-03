-- ============================================================
-- 02 - Create Tables
-- ============================================================
-- Schema for the InterviewDemoDB used in all 6 performance
-- scenario exercises.
--
-- Tables:
--   Customers     - 1,000 customers, varied order volumes
--   Products      - 200 products across 5 categories
--   Orders        - ~55,000 orders (skewed: one "whale" customer)
--   OrderItems    - line items per order
--   Employees     - org chart (self-referencing hierarchy)
--   OrderAuditLog - deliberately wide table with a notes blob
-- ============================================================

USE InterviewDemoDB;
GO

-- -------------------------------------------------------
-- Customers
-- -------------------------------------------------------
CREATE TABLE dbo.Customers (
    CustomerID   INT           IDENTITY(1,1) NOT NULL,
    FirstName    NVARCHAR(50)  NOT NULL,
    LastName     NVARCHAR(50)  NOT NULL,
    Email        NVARCHAR(200) NOT NULL,
    City         NVARCHAR(100) NOT NULL,
    Country      NVARCHAR(50)  NOT NULL DEFAULT 'US',
    IsVIP        BIT           NOT NULL DEFAULT 0,
    TierCode     NVARCHAR(10)  NOT NULL DEFAULT 'STANDARD',  -- STANDARD, SILVER, GOLD, PLATINUM
    CreatedDate  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Customers PRIMARY KEY CLUSTERED (CustomerID)
);
GO

-- -------------------------------------------------------
-- Products
-- -------------------------------------------------------
CREATE TABLE dbo.Products (
    ProductID    INT           IDENTITY(1,1) NOT NULL,
    ProductName  NVARCHAR(200) NOT NULL,
    Category     NVARCHAR(50)  NOT NULL,
    UnitPrice    DECIMAL(10,2) NOT NULL,
    StockQty     INT           NOT NULL DEFAULT 0,
    IsActive     BIT           NOT NULL DEFAULT 1,
    CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (ProductID)
);
GO

-- -------------------------------------------------------
-- Orders
-- Key design note: OrderDate distribution is intentionally
-- skewed so that date-based queries are meaningful.
-- CustomerID 1 (BigCorp) has ~50,000 orders — this is the
-- parameter sniffing villain.
-- -------------------------------------------------------
CREATE TABLE dbo.Orders (
    OrderID      INT           IDENTITY(1,1) NOT NULL,
    CustomerID   INT           NOT NULL,
    OrderDate    DATETIME2     NOT NULL,
    Status       NVARCHAR(20)  NOT NULL DEFAULT 'Pending',
    TotalAmount  DECIMAL(12,2) NOT NULL DEFAULT 0.00,
    ShipCity     NVARCHAR(100) NULL,
    Notes        NVARCHAR(MAX) NULL,    -- ← intentionally included for Scenario 4 (SELECT *)
    CONSTRAINT PK_Orders PRIMARY KEY CLUSTERED (OrderID),
    CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerID) REFERENCES dbo.Customers(CustomerID)
);
GO

-- -------------------------------------------------------
-- OrderItems
-- -------------------------------------------------------
CREATE TABLE dbo.OrderItems (
    OrderItemID  INT           IDENTITY(1,1) NOT NULL,
    OrderID      INT           NOT NULL,
    ProductID    INT           NOT NULL,
    Quantity     INT           NOT NULL DEFAULT 1,
    UnitPrice    DECIMAL(10,2) NOT NULL,
    LineTotal    AS (Quantity * UnitPrice) PERSISTED,
    CONSTRAINT PK_OrderItems PRIMARY KEY CLUSTERED (OrderItemID),
    CONSTRAINT FK_OrderItems_Orders   FOREIGN KEY (OrderID)   REFERENCES dbo.Orders(OrderID),
    CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductID) REFERENCES dbo.Products(ProductID)
);
GO

-- -------------------------------------------------------
-- Employees (self-referencing hierarchy for SELF JOIN demo)
-- -------------------------------------------------------
CREATE TABLE dbo.Employees (
    EmployeeID   INT           IDENTITY(1,1) NOT NULL,
    FullName     NVARCHAR(100) NOT NULL,
    ManagerID    INT           NULL,        -- NULL = top of org (CEO)
    Department   NVARCHAR(50)  NOT NULL,
    JobTitle     NVARCHAR(100) NOT NULL,
    Salary       DECIMAL(10,2) NOT NULL,
    HireDate     DATE          NOT NULL,
    CONSTRAINT PK_Employees PRIMARY KEY CLUSTERED (EmployeeID),
    CONSTRAINT FK_Employees_Manager FOREIGN KEY (ManagerID) REFERENCES dbo.Employees(EmployeeID)
);
GO

-- -------------------------------------------------------
-- OrderAuditLog
-- Wide table with a large text column — used for Scenario 4
-- to demonstrate the cost of SELECT * on fat tables.
-- -------------------------------------------------------
CREATE TABLE dbo.OrderAuditLog (
    AuditID       INT           IDENTITY(1,1) NOT NULL,
    OrderID       INT           NOT NULL,
    ChangedAt     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    ChangedBy     NVARCHAR(100) NOT NULL,
    OldStatus     NVARCHAR(20)  NULL,
    NewStatus     NVARCHAR(20)  NULL,
    ChangeDetails NVARCHAR(MAX) NULL,  -- ← fat column (imagine serialized JSON or XML)
    CONSTRAINT PK_OrderAuditLog PRIMARY KEY CLUSTERED (AuditID)
);
GO

PRINT '✅ Tables created.';
GO
