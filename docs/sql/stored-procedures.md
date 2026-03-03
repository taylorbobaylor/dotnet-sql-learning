# Stored Procedures

Stored procedures are precompiled SQL programs stored in the database. As a developer who calls SPs from C#, you're expected to understand not just *how to call them* but *how they work internally* and *how to make them fast*.

---

## What Is a Stored Procedure?

A stored procedure is a named, precompiled collection of T-SQL statements stored in the database. On first execution, SQL Server compiles it and caches the **execution plan**. Subsequent executions reuse the cached plan — no recompilation overhead.

```sql
CREATE PROCEDURE dbo.GetCustomerOrders
    @CustomerID INT,
    @StartDate  DATE = NULL,
    @EndDate    DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        o.OrderID,
        o.OrderDate,
        o.TotalAmount,
        c.Name AS CustomerName
    FROM Orders o
    INNER JOIN Customers c ON o.CustomerID = c.CustomerID
    WHERE o.CustomerID = @CustomerID
      AND (@StartDate IS NULL OR o.OrderDate >= @StartDate)
      AND (@EndDate   IS NULL OR o.OrderDate <= @EndDate)
    ORDER BY o.OrderDate DESC;
END;
GO
```

---

## Key Stored Procedure Best Practices

### 1. Always use `SET NOCOUNT ON`

```sql
CREATE PROCEDURE dbo.UpdateOrderStatus
    @OrderID INT,
    @Status  NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;  -- ← Always put this first

    UPDATE Orders
    SET Status = @Status
    WHERE OrderID = @OrderID;
END;
```

**Why:** Without `SET NOCOUNT ON`, SQL Server sends a "X rows affected" message back to the client after every statement. This adds network overhead and can confuse some ORM libraries (notably older versions of ADO.NET).

### 2. Use schema prefix — always `dbo.ProcedureName`

```sql
-- ❌ Bad — SQL Server searches schemas, extra overhead, potential security issues
EXEC GetCustomerOrders @CustomerID = 1;

-- ✅ Good
EXEC dbo.GetCustomerOrders @CustomerID = 1;
```

### 3. Avoid `SELECT *`

```sql
-- ❌ Bad — retrieves unnecessary columns, breaks if table schema changes
SELECT * FROM Orders WHERE CustomerID = @CustomerID;

-- ✅ Good — explicit, fast, resistant to schema changes
SELECT OrderID, OrderDate, TotalAmount FROM Orders WHERE CustomerID = @CustomerID;
```

### 4. Use `TRY/CATCH` for error handling

```sql
CREATE PROCEDURE dbo.TransferFunds
    @FromAccount INT,
    @ToAccount   INT,
    @Amount      DECIMAL(10,2)
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

            UPDATE Accounts SET Balance = Balance - @Amount WHERE AccountID = @FromAccount;
            UPDATE Accounts SET Balance = Balance + @Amount WHERE AccountID = @ToAccount;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;

        -- Re-throw the error to the caller
        THROW;
        -- Or use: RAISERROR(ERROR_MESSAGE(), ERROR_SEVERITY(), ERROR_STATE());
    END CATCH
END;
```

### 5. Avoid non-SARGable predicates

**SARGable** = Search ARGument ABLE — predicates that can use an index.

```sql
-- ❌ Non-SARGable — wrapping a column in a function kills index usage
WHERE YEAR(OrderDate) = 2024
WHERE UPPER(LastName) = 'SMITH'
WHERE OrderID + 1 = @ID

-- ✅ SARGable — index can be used
WHERE OrderDate >= '2024-01-01' AND OrderDate < '2025-01-01'
WHERE LastName = 'Smith'   -- or use a case-insensitive collation
WHERE OrderID = @ID - 1
```

### 6. Avoid cursors — use set-based logic

```sql
-- ❌ Bad — row-by-row processing, very slow
DECLARE order_cursor CURSOR FOR SELECT OrderID FROM Orders WHERE Status = 'Pending';
OPEN order_cursor;
FETCH NEXT FROM order_cursor INTO @OrderID;
WHILE @@FETCH_STATUS = 0
BEGIN
    UPDATE Orders SET Status = 'Processing' WHERE OrderID = @OrderID;
    FETCH NEXT FROM order_cursor INTO @OrderID;
END;
CLOSE order_cursor; DEALLOCATE order_cursor;

-- ✅ Good — single set-based operation
UPDATE Orders SET Status = 'Processing' WHERE Status = 'Pending';
```

If you genuinely need row-by-row logic, consider a `WHILE` loop with a table variable, or process in C# instead.

---

## Stored Procedure vs Function

| Feature | Stored Procedure | Function (Scalar/TVF) |
|---|---|---|
| Returns value? | No (uses OUTPUT params or result sets) | Yes — scalar or table |
| Used in SELECT? | No | Yes |
| Can modify data? | Yes (INSERT/UPDATE/DELETE) | Generally no (scalar) |
| Can use transactions? | Yes | No |
| Can call from WHERE/JOIN? | No | Yes (use with caution) |
| Error handling (TRY/CATCH)? | Yes | Limited |

!!! warning "Scalar functions in WHERE clauses"
    Calling a scalar UDF in a `WHERE` clause (`WHERE dbo.GetTax(Price) > 10`) is evaluated row-by-row and disables parallelism. This is a **major performance killer**. Use inline Table-Valued Functions (iTVFs) or rewrite the logic inline.

---

## Output Parameters

```sql
CREATE PROCEDURE dbo.GetOrderCount
    @CustomerID  INT,
    @OrderCount  INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @OrderCount = COUNT(*) FROM Orders WHERE CustomerID = @CustomerID;
END;
GO

-- Calling it:
DECLARE @Count INT;
EXEC dbo.GetOrderCount @CustomerID = 1, @OrderCount = @Count OUTPUT;
SELECT @Count AS TotalOrders;
```

---

## Return Codes

Stored procedures return an integer return code (0 = success by convention):

```sql
CREATE PROCEDURE dbo.DeleteOrder
    @OrderID INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM Orders WHERE OrderID = @OrderID)
    BEGIN
        RETURN -1;  -- Not found
    END

    DELETE FROM Orders WHERE OrderID = @OrderID;
    RETURN 0;  -- Success
END;
```

---

## Modifying Stored Procedures

```sql
-- Modify an existing SP (preserves permissions)
ALTER PROCEDURE dbo.GetCustomerOrders
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;
    -- new body here
END;

-- Drop and recreate (loses permissions)
DROP PROCEDURE IF EXISTS dbo.GetCustomerOrders;

-- Idiomatic modern pattern
CREATE OR ALTER PROCEDURE dbo.GetCustomerOrders  -- SQL Server 2016+
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;
    -- body here
END;
```

---

## Common Interview Questions on Stored Procedures

**Q: Why are stored procedures faster than ad-hoc SQL?**
The execution plan is compiled once and cached. Ad-hoc queries may require parsing, binding, and optimization on every execution (though parameterized ad-hoc queries also get cached).

**Q: Can a stored procedure call another stored procedure?**
Yes — this is called a *nested stored procedure*. SQL Server supports up to 32 levels of nesting. Good for breaking down complex logic.

**Q: What's the difference between `RAISERROR` and `THROW`?**
`THROW` (SQL Server 2012+) is simpler and re-throws the original error with the original error number. `RAISERROR` is the older approach and requires you to specify severity and state. Prefer `THROW` in modern code.

**Q: What happens if you don't use `BEGIN TRANSACTION` but one of the statements in your SP fails?**
Each individual statement is auto-committed. Preceding statements that succeeded are NOT rolled back. Use explicit transactions when multiple operations must succeed or fail together.
