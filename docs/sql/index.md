# SQL Server Overview

This section covers the SQL Server topics most likely to come up in a developer interview, with particular focus on stored procedures, indexing, and query performance — the areas where developers who rely on DBAs can have knowledge gaps.

## Topics in This Section

- **[Joins](joins.md)** — INNER, LEFT, RIGHT, FULL OUTER, CROSS, and SELF joins with real examples
- **[Stored Procedures](stored-procedures.md)** — Writing, optimizing, and best practices
- **[Indexes](indexes.md)** — Clustered, nonclustered, covering, and composite indexes
- **[Query Optimization](optimization.md)** — Practical techniques to make queries faster
- **[Execution Plans](execution-plans.md)** — Reading and acting on execution plans (VS Code MSSQL / DataGrip)
- **[Parameter Sniffing](parameter-sniffing.md)** — What it is and how to fix it

## Key SQL Server Concepts to Know Cold

**T-SQL vs ANSI SQL** — SQL Server uses Transact-SQL (T-SQL), Microsoft's extension of ANSI SQL. It adds control flow, local variables, error handling (`TRY/CATCH`), and more.

**Query lifecycle:** Parse → Bind → Optimize → Execute. The optimizer generates an execution plan, which is cached for reuse. Understanding this is key to explaining why stored procedures perform well.

**SQL Server editions commonly used in enterprise:** Standard, Enterprise, Developer (free, same features as Enterprise — great for local dev).

## Useful Shortcuts on macOS

> **Note:** Azure Data Studio was retired February 28, 2026. Use VS Code + MSSQL extension or DataGrip.

### VS Code MSSQL Extension

| Action | How |
|---|---|
| Execute query | `Cmd + Shift + E` or click ▶ in toolbar |
| Run with actual execution plan | Right-click → **Run Query with Actual Execution Plan** |
| Show estimated execution plan | Right-click → **Explain Current Statement** |
| Comment/uncomment lines | `Cmd + /` |
| Connect to server | Click the server name in the status bar at the bottom |

### DataGrip

| Action | How |
|---|---|
| Execute query / selection | `Cmd + Enter` |
| Run with actual execution plan | Right-click → **Explain Plan → Explain Analyzed** |
| Show estimated execution plan | Right-click → **Explain Plan → Explain Plan** or `Cmd + Shift + E` |
| Comment/uncomment lines | `Cmd + /` |
| Reformat SQL | `Cmd + Alt + L` |
