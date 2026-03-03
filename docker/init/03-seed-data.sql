-- ============================================================
-- 03 - Seed Data
-- ============================================================
-- Inserts realistic, intentionally skewed data to make
-- performance scenarios meaningful.
--
-- Key skews:
--  • CustomerID=1 ("BigCorp Ltd") → ~50,000 orders
--    This is the parameter sniffing villain.
--  • Most other customers → 1-10 orders
--  • Dates spread over 3 years → date filter demos
--  • Product prices vary widely → aggregation demos
-- ============================================================

USE InterviewDemoDB;
GO

SET NOCOUNT ON;

-- -------------------------------------------------------
-- Employees (build hierarchy first — customers reference nothing)
-- -------------------------------------------------------
INSERT INTO dbo.Employees (FullName, ManagerID, Department, JobTitle, Salary, HireDate) VALUES
    ('Sarah Chen',       NULL, 'Executive',   'CEO',                     220000, '2015-01-15'),
    ('Marcus Webb',      1,    'Engineering', 'VP of Engineering',        175000, '2016-03-20'),
    ('Priya Patel',      1,    'Sales',       'VP of Sales',              165000, '2016-07-01'),
    ('Tom Burgess',      1,    'Operations',  'COO',                      180000, '2017-02-14'),
    ('Aisha Johnson',    2,    'Engineering', 'Engineering Manager',       130000, '2018-05-10'),
    ('Diego Torres',     2,    'Engineering', 'Senior Software Engineer',  115000, '2018-09-15'),
    ('Emma Li',          2,    'Engineering', 'Software Engineer',          95000, '2020-01-06'),
    ('Jake Nowak',       5,    'Engineering', 'Software Engineer',          92000, '2021-03-01'),
    ('Rachel Kim',       3,    'Sales',       'Sales Manager',             110000, '2019-06-15'),
    ('Carlos Mendez',    3,    'Sales',       'Account Executive',          80000, '2020-08-20'),
    ('Sofia Andersen',   3,    'Sales',       'Account Executive',          78000, '2021-01-11'),
    ('Liam O''Brien',    9,    'Sales',       'Sales Development Rep',      65000, '2022-04-04'),
    ('Nat Russo',        4,    'Operations',  'Operations Manager',        105000, '2019-11-01'),
    ('Yuki Tanaka',      13,   'Operations',  'Logistics Coordinator',      72000, '2021-09-13'),
    ('Ben Carter',       13,   'Operations',  'Analyst',                    68000, '2022-07-25');
GO

-- -------------------------------------------------------
-- Products
-- -------------------------------------------------------
INSERT INTO dbo.Products (ProductName, Category, UnitPrice, StockQty) VALUES
    ('Laptop Pro 15"',           'Electronics',  1299.99, 150),
    ('Wireless Mouse',           'Electronics',    29.99, 800),
    ('USB-C Hub 7-Port',         'Electronics',    49.99, 500),
    ('Mechanical Keyboard',      'Electronics',   149.99, 300),
    ('4K Monitor 27"',           'Electronics',   649.99,  80),
    ('Webcam HD 1080p',          'Electronics',    79.99, 400),
    ('Standing Desk',            'Furniture',     599.99,  40),
    ('Ergonomic Chair',          'Furniture',     799.99,  30),
    ('Monitor Arm',              'Furniture',     129.99, 200),
    ('Desk Lamp LED',            'Furniture',      59.99, 350),
    ('Notebook A5 Pack 3',       'Stationery',      9.99, 2000),
    ('Ballpoint Pens 12pk',      'Stationery',      7.99, 3000),
    ('Sticky Notes Variety',     'Stationery',     12.99, 1500),
    ('Whiteboard Markers 8pk',   'Stationery',     14.99, 600),
    ('File Folders 25pk',        'Stationery',     11.99, 800),
    ('Protein Powder Vanilla',   'Wellness',       49.99, 500),
    ('Blue Light Glasses',       'Wellness',       39.99, 700),
    ('Desk Plant Succulent',     'Wellness',       24.99, 400),
    ('Noise Cancelling Earbuds', 'Electronics',   199.99, 250),
    ('Cable Management Kit',     'Electronics',    19.99, 900),
    ('Office Chair Mat',         'Furniture',      89.99, 120),
    ('Whiteboard 36x24"',        'Furniture',     159.99,  60),
    ('Planner 2024',             'Stationery',     21.99, 400),
    ('Hand Sanitiser 500ml',     'Wellness',        8.99, 1200),
    ('Coffee Mug Thermal',       'Wellness',       27.99, 600);
GO

-- -------------------------------------------------------
-- Customers
-- Customer 1 = "BigCorp Ltd" — the parameter sniffing whale
-- -------------------------------------------------------
INSERT INTO dbo.Customers (FirstName, LastName, Email, City, Country, IsVIP, TierCode) VALUES
    ('BigCorp',   'Ltd',        'orders@bigcorp.com',         'New York',     'US', 1, 'PLATINUM');

-- Generate 999 regular customers using a numbers trick
WITH nums AS (
    SELECT TOP 999 ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_columns a CROSS JOIN sys.all_columns b
)
INSERT INTO dbo.Customers (FirstName, LastName, Email, City, Country, IsVIP, TierCode)
SELECT
    'Customer' + CAST(n AS NVARCHAR(10)),
    'Lastname'  + CAST(n AS NVARCHAR(10)),
    'customer'  + CAST(n AS NVARCHAR(10)) + '@example.com',
    CASE (n % 8)
        WHEN 0 THEN 'New York'    WHEN 1 THEN 'Los Angeles'
        WHEN 2 THEN 'Chicago'     WHEN 3 THEN 'Houston'
        WHEN 4 THEN 'Phoenix'     WHEN 5 THEN 'Philadelphia'
        WHEN 6 THEN 'San Antonio' ELSE     'San Diego'
    END,
    CASE (n % 5) WHEN 0 THEN 'CA' WHEN 1 THEN 'AU' WHEN 2 THEN 'GB' ELSE 'US' END,
    CASE WHEN n % 20 = 0 THEN 1 ELSE 0 END,  -- 5% are VIP
    CASE
        WHEN n % 20 = 0 THEN 'GOLD'
        WHEN n % 10 = 0 THEN 'SILVER'
        ELSE 'STANDARD'
    END
FROM nums;
GO

-- -------------------------------------------------------
-- Orders — BigCorp (CustomerID=1) gets ~50,000 orders
-- This is what makes parameter sniffing dramatic.
-- -------------------------------------------------------

-- BigCorp orders — spread over 3 years
DECLARE @i INT = 0;
WHILE @i < 50000
BEGIN
    INSERT INTO dbo.Orders (CustomerID, OrderDate, Status, TotalAmount, ShipCity, Notes)
    VALUES (
        1,
        DATEADD(MINUTE, -(@i * 31), SYSUTCDATETIME()),  -- every 31 minutes back in time
        CASE (@i % 5)
            WHEN 0 THEN 'Pending'   WHEN 1 THEN 'Processing'
            WHEN 2 THEN 'Shipped'   WHEN 3 THEN 'Delivered'
            ELSE        'Cancelled'
        END,
        ROUND(RAND(CHECKSUM(NEWID())) * 2000 + 50, 2),
        'New York',
        NULL  -- notes deliberately NULL for most — Scenario 4 seeds some fat notes below
    );
    SET @i = @i + 1;
END;
GO

-- Regular customers — 1 to 8 orders each
INSERT INTO dbo.Orders (CustomerID, OrderDate, Status, TotalAmount, ShipCity, Notes)
SELECT
    c.CustomerID,
    DATEADD(DAY, -ABS(CHECKSUM(NEWID()) % 1095), SYSUTCDATETIME()),
    CASE (ABS(CHECKSUM(NEWID())) % 5)
        WHEN 0 THEN 'Pending'   WHEN 1 THEN 'Processing'
        WHEN 2 THEN 'Shipped'   WHEN 3 THEN 'Delivered'
        ELSE        'Cancelled'
    END,
    ROUND(ABS(CHECKSUM(NEWID()) % 1500) + 25.00, 2),
    c.City,
    NULL
FROM dbo.Customers c
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6)) AS n(num)
WHERE c.CustomerID > 1
  AND ABS(CHECKSUM(NEWID())) % 6 < n.num;  -- randomize how many orders each gets
GO

-- -------------------------------------------------------
-- Update Order TotalAmount based on actual OrderItems
-- (we'll add items next, then recalc)
-- -------------------------------------------------------

-- OrderItems — assign 1-4 random products to each order
INSERT INTO dbo.OrderItems (OrderID, ProductID, Quantity, UnitPrice)
SELECT
    o.OrderID,
    p.ProductID,
    ABS(CHECKSUM(NEWID()) % 5) + 1 AS Quantity,
    p.UnitPrice
FROM dbo.Orders o
CROSS APPLY (
    SELECT TOP (ABS(CHECKSUM(NEWID()) % 4) + 1)
        ProductID, UnitPrice
    FROM dbo.Products
    ORDER BY NEWID()
) p
WHERE o.OrderID % 3 <> 2;  -- skip some orders to leave TotalAmount as-is for variety
GO

-- Recalc TotalAmount from line items where items exist
UPDATE o
SET    o.TotalAmount = i.ItemTotal
FROM   dbo.Orders o
INNER JOIN (
    SELECT OrderID, SUM(LineTotal) AS ItemTotal
    FROM dbo.OrderItems
    GROUP BY OrderID
) i ON o.OrderID = i.OrderID;
GO

-- -------------------------------------------------------
-- Add fat Notes to some BigCorp orders — used in Scenario 4
-- to show the cost of pulling NVARCHAR(MAX) unnecessarily.
-- -------------------------------------------------------
UPDATE dbo.Orders
SET Notes = REPLICATE(
    N'{"event":"order_updated","timestamp":"2024-01-01T12:00:00Z","user":"system","details":"Automatic status update triggered by fulfilment engine. Previous state captured for audit purposes. Integration payload logged for replay in case of downstream failure. MessageID: ' + CAST(OrderID AS NVARCHAR) + N'"}',
    5  -- repeat it 5x to make each row ~800 bytes
)
WHERE CustomerID = 1 AND OrderID % 3 = 0;  -- seed about 1/3 of BigCorp orders
GO

-- -------------------------------------------------------
-- OrderAuditLog — populate for some orders
-- -------------------------------------------------------
INSERT INTO dbo.OrderAuditLog (OrderID, ChangedBy, OldStatus, NewStatus, ChangeDetails)
SELECT
    o.OrderID,
    'System',
    'Pending',
    o.Status,
    '{"automated":true,"reason":"status_transition","orderId":' + CAST(o.OrderID AS NVARCHAR) + '}'
FROM dbo.Orders o
WHERE o.Status <> 'Pending'
  AND o.OrderID % 4 = 0;  -- audit every 4th changed order
GO

PRINT '✅ Seed data inserted.';
PRINT '   Customers:     ' + CAST((SELECT COUNT(*) FROM dbo.Customers)  AS NVARCHAR);
PRINT '   Products:      ' + CAST((SELECT COUNT(*) FROM dbo.Products)   AS NVARCHAR);
PRINT '   Orders:        ' + CAST((SELECT COUNT(*) FROM dbo.Orders)     AS NVARCHAR);
PRINT '   OrderItems:    ' + CAST((SELECT COUNT(*) FROM dbo.OrderItems) AS NVARCHAR);
PRINT '   Employees:     ' + CAST((SELECT COUNT(*) FROM dbo.Employees)  AS NVARCHAR);
PRINT '   BigCorp orders:' + CAST((SELECT COUNT(*) FROM dbo.Orders WHERE CustomerID = 1) AS NVARCHAR);
GO
