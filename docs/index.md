# .NET & SQL Interview Prep 🚀

Welcome to your personal interview study guide covering **C# .NET** and **SQL Server**. This site pairs concept explanations with a **hands-on lab** — a real Docker-hosted SQL Server with intentionally bad stored procedures you can run, diagnose, and fix yourself.

---

## 🧪 Start Here: Hands-On Lab

Spin up a local SQL Server and run the 6 real-world performance scenarios:

```bash
cd docker && docker compose up -d
bash init-db.sh           # creates DB, tables, seed data, all stored procs
cd ../src/SqlDemos
dotnet run -- all         # benchmark all 6 bad vs fixed proc pairs
```

| # | Scenario | Antipattern |
|---|---|---|
| [1](hands-on/scenario-01-cursor.md) | Cursor Catastrophe | Row-by-row UPDATE |
| [2](hands-on/scenario-02-parameter-sniffing.md) | Parameter Sniffing Ghost | Cached wrong plan |
| [3](hands-on/scenario-03-sargable.md) | Non-SARGable Date Trap | Functions on columns |
| [4](hands-on/scenario-04-select-star.md) | SELECT * Flood | Pulling NVARCHAR(MAX) |
| [5](hands-on/scenario-05-key-lookup.md) | Key Lookup Tax | Non-covering index |
| [6](hands-on/scenario-06-scalar-udf.md) | Scalar UDF Killer | Serial row-by-row UDF |

---

## What's Covered

### 🗄️ SQL Server
Topics every developer touching SQL Server should know — from basic joins through to stored procedure optimization.

| Section | Why It Matters |
|---|---|
| [Joins](sql/joins.md) | Foundational — you WILL get asked about these |
| [Stored Procedures](sql/stored-procedures.md) | Core topic for the role |
| [Indexes](sql/indexes.md) | Critical for "why is this slow?" conversations |
| [Query Optimization](sql/optimization.md) | Best practices and quick wins |
| [Execution Plans](sql/execution-plans.md) | How to diagnose a slow query like a pro |
| [Parameter Sniffing](sql/parameter-sniffing.md) | A common gotcha — shows senior-level awareness |

### ⚙️ C# Language
The language features you'll be expected to discuss fluently.

| Section | Why It Matters |
|---|---|
| [Async / Await](csharp/async-await.md) | Always asked; easy to get wrong conceptually |
| [SOLID Principles](csharp/solid-principles.md) | Architecture discussions and code review answers |
| [Dependency Injection](csharp/dependency-injection.md) | Core to .NET Core — expected knowledge |
| [Generics & LINQ](csharp/generics-linq.md) | Shows you write clean, modern C# |

### 🔌 .NET & Data Access
How you connect C# to SQL Server.

| Section | Why It Matters |
|---|---|
| [Entity Framework Core](dotnet/entity-framework.md) | Modern ORM used in most .NET shops |
| [Dapper](dotnet/dapper.md) | Lightweight ORM, great for stored proc calls |
| [Calling Stored Procedures](dotnet/calling-stored-procedures.md) | Practical patterns — EF Core + Dapper side-by-side |

### 📋 Interview Prep
Structured game plan and cheat sheets.

| Section | Why It Matters |
|---|---|
| [Game Plan](interview-prep/index.md) | How to spend your study time tonight |
| [Quick Reference Card](interview-prep/quick-reference.md) | One-page cheat sheet to review right before |
| [Top Q&A](interview-prep/common-questions.md) | Most common questions with ideal answers |

---

## How to Run This Site

```bash
pip install mkdocs-material
mkdocs serve
```

Then open [http://localhost:8000](http://localhost:8000) in your browser.

---

!!! tip "Study tip"
    Start with the [Game Plan](interview-prep/index.md) to prioritize your time, then work through SQL Server topics first — those are the areas you've flagged as needing the most brush-up.
