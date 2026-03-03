# .NET & Data Access Overview

Since you have experience with Entity Framework and Dapper, this section focuses on the specifics that come up in interviews — plus the patterns for calling stored procedures in different ways.

## Topics in This Section

- **[Entity Framework Core](entity-framework.md)** — Key concepts, common gotchas, performance
- **[Dapper](dapper.md)** — Lightweight ORM, when to use it, and common patterns
- **[Calling Stored Procedures](calling-stored-procedures.md)** — EF Core vs Dapper, patterns, parameters

## EF Core vs Dapper — Quick Comparison

| Aspect | Entity Framework Core | Dapper |
|---|---|---|
| Type | Full ORM | Micro-ORM |
| LINQ support | Yes — full | No — raw SQL strings |
| Code generation | Migrations, scaffolding | None |
| Performance | Good with care, some overhead | Very fast — near ADO.NET |
| Change tracking | Yes — automatic | No |
| Complex queries | LINQ handles it | You write the SQL |
| Stored procedures | Supported (some friction) | Excellent — first-class |
| Learning curve | High | Low |
| Best for | CRUD-heavy apps with complex object graphs | Reporting, stored procs, performance-critical queries |

**Real-world answer for interviews:** "I use EF Core for standard CRUD and domain model persistence. For complex reports, stored procedure calls, or anything performance-sensitive, I'll use Dapper or fall through to raw SQL via EF Core's `FromSqlRaw`."
