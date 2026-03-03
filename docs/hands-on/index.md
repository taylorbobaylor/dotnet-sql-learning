# Hands-On Scenarios

This section pairs every concept from the study guide with a real, runnable exercise using a local SQL Server instance. You'll see actual timing numbers, real execution plans, and the satisfaction of watching logical reads drop from thousands to dozens.

---

## Prerequisites

**Step 1 — Start SQL Server via Docker:**

```bash
cd docker
cp .env.example .env          # optional: customize the SA password
docker compose up -d
```

Wait ~20 seconds for SQL Server to initialize, then run the init scripts:

```bash
bash init-db.sh               # Linux/Mac
# Windows: run each docker/init/*.sql in SSMS in order
```

**Step 2 — Verify it's working (SSMS or Azure Data Studio):**

```
Server:   localhost,1433
Login:    sa
Password: InterviewDemo@2024
```

**Step 3 — Run the C# demo app:**

```bash
cd src/SqlDemos
dotnet run              # interactive menu
dotnet run -- all       # run all 6 scenarios and print timing table
dotnet run -- 2         # run scenario 2 only
```

---

## The Database: `InterviewDemoDB`

| Table | Rows (approx.) | Purpose |
|---|---|---|
| `Customers` | 1,000 | 1 whale (50k orders), 999 normal |
| `Products` | 25 | Used in OrderItems |
| `Orders` | ~55,000 | Skewed data — key to parameter sniffing |
| `OrderItems` | ~120,000 | Line items per order |
| `Employees` | 15 | Org chart for SELF JOIN demo |
| `OrderAuditLog` | ~14,000 | Wide table for SELECT * demo |

**The whale:** CustomerID=1 ("BigCorp Ltd") has ~50,000 orders. Every other customer has 1-8. This skew is what makes Scenario 2 (Parameter Sniffing) interesting.

---

## The 6 Scenarios

| # | Name | Antipattern | Typical Speedup |
|---|---|---|---|
| [1](scenario-01-cursor.md) | Cursor Catastrophe | Row-by-row UPDATE in a loop | 100–1000× |
| [2](scenario-02-parameter-sniffing.md) | Parameter Sniffing Ghost | Cached plan wrong for data distribution | 10–50× |
| [3](scenario-03-sargable.md) | Non-SARGable Date Trap | Functions on columns in WHERE | 10–20× |
| [4](scenario-04-select-star.md) | SELECT * Flood | Pulling fat NVARCHAR(MAX) columns | 5–30× |
| [5](scenario-05-key-lookup.md) | Key Lookup Tax | Non-covering index | 5–15× |
| [6](scenario-06-scalar-udf.md) | Scalar UDF Killer | Scalar function in WHERE clause | 20–100× |

---

## How to Use SSMS to Measure

Every scenario has an SSMS exercise block. Before running, enable:

```sql
-- In SSMS: paste at the top of your query window
SET STATISTICS IO ON;
SET STATISTICS TIME ON;
```

Then press `Ctrl+M` to enable the **actual execution plan** before running.

After running, check:
- **Messages tab** → logical reads per table
- **Execution Plan tab** → look for the red flags listed in each scenario

!!! tip "Good habit"
    Always run the bad version first, write down the logical reads, then run the fixed version. The contrast is what makes it stick.
