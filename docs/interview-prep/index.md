# Interview Game Plan

You have a C#/.NET/SQL Server interview tomorrow. Here's how to spend your time tonight.

---

## Priority Order for Tonight

Given that you're strong on C# and .NET but SQL optimization is your biggest gap, prioritize in this order:

### 🔴 High Priority — SQL (Your Gap)

1. **[Execution Plans](../sql/execution-plans.md)** — Read the whole page. Practice the "interview explanation template" at the bottom. This one question — "how do you diagnose a slow stored procedure?" — is almost certain to come up.

2. **[Parameter Sniffing](../sql/parameter-sniffing.md)** — Read the page and memorize the interview answer template. This topic immediately signals senior-level SQL knowledge if you mention it unprompted.

3. **[Indexes](../sql/indexes.md)** — Focus on: clustered vs nonclustered, covering indexes (INCLUDE), and the fragmentation section. Know the DMV query for finding missing indexes.

4. **[Stored Procedure Best Practices](../sql/stored-procedures.md)** — `SET NOCOUNT ON`, `TRY/CATCH`, avoiding cursors, SARGable predicates.

5. **[Query Optimization Anti-Patterns](../sql/optimization.md)** — The non-SARGable section and the temp table vs table variable section.

### 🟡 Medium Priority — Joins & C#

6. **[Joins](../sql/joins.md)** — You probably know this but run through the anti-join pattern (LEFT JOIN + WHERE IS NULL) and FULL OUTER JOIN use cases.

7. **[Async/Await](../csharp/async-await.md)** — Focus on the mistakes section: `async void`, `.Result` deadlock, unnecessary `async`.

8. **[SOLID Principles](../csharp/solid-principles.md)** — Know a one-liner and a quick C# example for each.

### 🟢 Lower Priority — If Time Allows

9. **[Dependency Injection Lifetimes](../csharp/dependency-injection.md)** — The captive dependency problem is the juicy interview trap.

10. **[EF Core N+1](../dotnet/entity-framework.md)** — A very common gotcha question.

11. **[Calling Stored Procedures](../dotnet/calling-stored-procedures.md)** — Review the Dapper patterns since you said you use both.

---

## The Three Questions You Must Nail

These are almost certain to be asked given the job description:

### 1. "A stored procedure is running slow. Walk me through what you'd do."

**Your answer framework:**
1. Run it in SSMS with `SET STATISTICS IO ON` and the actual execution plan
2. Look at logical reads per table — which table is doing the most I/O?
3. Check the execution plan for: Table Scans, Key Lookups, and row count estimate vs actual mismatches
4. Table Scan → look for missing index or non-SARGable predicate
5. Key Lookup → add INCLUDE columns to make it covering
6. Estimate vs actual mismatch → stale statistics (`UPDATE STATISTICS`) or parameter sniffing
7. Fix the highest-cost operator, re-measure, iterate

### 2. "Explain clustered vs nonclustered indexes."

**Your answer:**
"A clustered index determines the physical sort order of data in the table — the leaf pages of the index ARE the data rows. You can only have one per table because data can only be sorted one way, and SQL Server creates it on the primary key by default. A nonclustered index is a separate structure with a pointer back to the clustered index row. You can have up to 999 per table. A covering index is a nonclustered index that includes all columns a specific query needs via INCLUDE — this eliminates the key lookup back to the base table."

### 3. "What are the different join types and when would you use each?"

**Your answer:** Walk through INNER (both match), LEFT (all left + nullable right), RIGHT (all right + nullable left — usually flip to LEFT), FULL OUTER (everything from both), and mention the anti-join pattern (LEFT JOIN + WHERE IS NULL to find records with no match). Mention you typically avoid RIGHT JOINs for code readability.

---

## Bonus Points Topics (Drop These In)

Mentioning these unprompted will impress:

- **Parameter sniffing** — "One thing I've seen cause mysterious performance issues is parameter sniffing, where..."
- **`SET NOCOUNT ON`** — mention it as a reflex best practice in stored procedures
- **SARGable predicates** — "I always make sure my WHERE clauses are SARGable — avoiding functions on the left side of comparisons"
- **`AsNoTracking()` in EF Core** — "For read-only queries I always use AsNoTracking to skip the change tracking overhead"
- **Covering indexes** — shows you understand the cost of key lookups

---

## Day-Of Checklist

- [ ] Review [Quick Reference Card](quick-reference.md) over breakfast
- [ ] Have a story ready for each: "Tell me about a time you solved a performance problem"
- [ ] Be ready to whiteboard a SQL query — practice writing one on paper
- [ ] Know how to say "I'd investigate that with X" for things you don't know perfectly
- [ ] Mention the data team context honestly: "We had a dedicated data team, so I worked with them on tuning, but I understand the mechanics well enough to diagnose and propose solutions"
