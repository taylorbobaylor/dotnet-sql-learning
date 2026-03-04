namespace SqlDemos;

public record Order(
    int       OrderID,
    int       CustomerID,
    DateTime  OrderDate,
    string    Status,
    decimal   TotalAmount,
    string?   ShipCity,
    string?   Notes = null
);

public record Customer(
    int      CustomerID,
    string   FirstName,
    string   LastName,
    string   Email,
    string   City,
    bool     IsVIP,
    string   TierCode
);

public record Employee(
    int      EmployeeID,
    string   FullName,
    string   JobTitle,
    string   Department,
    decimal  Salary,
    string?  Manager,
    string?  ManagerTitle,
    int      YearsAtCompany
);

public record BenchmarkResult(
    string   ScenarioName,
    string   SprocName,
    long     ElapsedMs,
    int      RowCount,
    bool     IsBad
);
