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

Wait ~20 seconds for SQL Server to initialize, then run the init scripts.

By default the helper will execute the first five files and leave the “fixed” stored
procedures until after you've worked through the labs. Run with `--all` if you
want everything applied in one shot.

```bash
bash init-db.sh               # Linux/Mac
# or `bash init-db.sh --all` to include the fixed procedures
# Windows: run each docker/init/*.sql in SSMS in order
```

**Step 2 — Verify it's working (VS Code MSSQL extension or DataGrip):**

```
Server:   localhost,1433
Login:    sa
Password: InterviewDemo@2026
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

## How to Measure on macOS

Every scenario has a query exercise block. The SQL commands themselves (`SET STATISTICS IO ON`, `EXEC ...`) are identical across both tools. What differs is *how you enable the execution plan*. Pick the one you have:

> **Note:** Azure Data Studio was retired on February 28, 2026. Use one of the two tools below instead.

---

### Option A — VS Code with the MSSQL Extension

1. Install **SQL Server (mssql)** by Microsoft (`ms-mssql.mssql`) from the Extensions panel if you haven't already.
2. Open a `.sql` file, connect to `localhost,1433` when prompted (or use the status-bar connection picker at the bottom).
3. Paste the statistics preamble at the top of your query:
   ```sql
   SET STATISTICS IO ON;
   SET STATISTICS TIME ON;
   ```
4. To get the **actual execution plan**, right-click anywhere in the editor → **"Run Query with Actual Execution Plan"** (or click the dropdown arrow next to the Run button ▶ and select that option).
5. The results pane shows three tabs:
   - **Results** — the row data
   - **Messages** — `STATISTICS IO` / `STATISTICS TIME` output (logical reads, CPU time, elapsed time)
   - **Execution Plan** — graphical plan (click any operator node to see estimated vs actual rows, cost %, output column list)

> **Tip:** The VS Code MSSQL extension uses the same `.sqlplan` XML format as SSMS, so all operators (Index Seek, Key Lookup, Hash Match, etc.) use identical names.

---

### Option B — JetBrains DataGrip

1. Connect to `localhost,1433` with the SQL Server driver (Login: `sa`, Password: `InterviewDemo@2026`, Database: `InterviewDemoDB`).
2. Open a query console for that data source.
3. Paste the statistics preamble at the top:
   ```sql
   SET STATISTICS IO ON;
   SET STATISTICS TIME ON;
   ```
4. To get the **actual execution plan**, right-click in the editor → **Explain Plan → Explain Analyzed**. This runs the query and captures live row counts — equivalent to SSMS's actual execution plan.
   - For a quick *estimated* plan without running the query: right-click → **Explain Plan → Explain Plan** (or `Cmd+Shift+E`).
5. Results appear in separate panels at the bottom:
   - **Output** — the `STATISTICS IO` / `STATISTICS TIME` text (logical reads, CPU, elapsed)
   - **Plan** — DataGrip's graphical plan tree. Click any node to expand its full properties (actual rows, estimated rows, cost, output columns).

> **Tip:** DataGrip's **Explain Analyzed** view highlights the costliest nodes in **orange/red**. Start your investigation there.

---

### The SQL Commands (Same in All Tools)

```sql
-- Paste at the top of every exercise query window:
SET STATISTICS IO ON;
SET STATISTICS TIME ON;
```

After running, look for this pattern in the Messages/Output tab:

```
Table 'Orders'. Scan count 1, logical reads 3842, lob logical reads 0, ...
SQL Server Execution Times: CPU time = 234 ms, elapsed time = 412 ms.
```

**Logical reads** is your primary metric. 1 logical read = 1 × 8 KB page read from the buffer cache. Dropping logical reads from thousands to dozens is the goal for most of these scenarios.

!!! tip "Good habit"
    Always run the bad version first, write down the logical reads, then run the fixed version. The contrast is what makes it stick.
