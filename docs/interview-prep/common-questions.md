# Top Interview Q&A

Complete answers to the most likely questions. Read these out loud to practice.

---

## SQL Server Questions

### "How do you approach optimizing a slow stored procedure?"

> "My first step is always measurement before assumption. I'll run the procedure in SSMS with `SET STATISTICS IO ON` and the actual execution plan enabled. The logical reads metric tells me where the I/O is happening — which table is doing the most work. Then I look at the execution plan itself.
>
> The things I look for are: table scans on large tables (usually means a missing index or a non-SARGable predicate in the WHERE clause), key lookups (where SQL Server found the row via a nonclustered index but needed to go back for extra columns — I'd fix that by adding INCLUDE columns to make a covering index), and any large discrepancy between estimated and actual row counts (which points to either stale statistics or parameter sniffing).
>
> I fix the highest-cost issue first, re-run with statistics to compare logical reads before and after, then iterate. A good optimization often drops logical reads by 10x or more."

---

### "What's the difference between a clustered and nonclustered index?"

> "A clustered index defines the physical sort order of the data in the table — the data rows themselves are stored in the index leaf pages. You can only have one per table because the data can only be ordered one way. SQL Server creates a clustered index on the primary key by default.
>
> A nonclustered index is a separate data structure that contains the indexed key columns plus a pointer back to the actual data row. You can have up to 999 nonclustered indexes per table, though in practice you want to be selective to avoid write overhead.
>
> A covering index is a nonclustered index where I add INCLUDE columns — columns the query needs that aren't in the index key. This means SQL Server can satisfy the query entirely from the index, eliminating the 'key lookup' operation of going back to the base table. That's one of the most impactful optimizations I look for in execution plans."

---

### "What is parameter sniffing?"

> "Parameter sniffing is when SQL Server builds an execution plan based on the first set of parameter values a stored procedure is called with, caches that plan, and then reuses it for all subsequent calls — even if those calls have parameter values with very different data distributions.
>
> For example, if a CustomerID column has some customers with 2 orders and some with 500,000 orders, and the plan was first built for a customer with 2 orders, it'll use Nested Loops. Then when a high-volume customer calls the same procedure, they get that same Nested Loops plan — which is terrible for 500,000 rows.
>
> The telltale sign is 'runs fast for some users, slow for others.' In the execution plan, you'll see a big mismatch between estimated and actual row counts.
>
> My fix depends on the call frequency. For infrequently-called procedures, I add `OPTION (RECOMPILE)` to the problematic query — it generates a fresh optimal plan each time. For high-frequency calls where recompile CPU overhead would matter, I use `OPTION (OPTIMIZE FOR UNKNOWN)` or the local variable trick, which causes the optimizer to use average statistics for planning rather than the specific sniffed value."

---

### "Explain the different SQL joins."

> "INNER JOIN returns only rows where there's a match in both tables — it's the most common join and appropriate when you only want records that have a complete relationship.
>
> LEFT JOIN returns all rows from the left table plus any matching rows from the right. Where there's no match, you get NULL on the right side. I use this constantly for things like 'give me all customers and their most recent order, if they have one.'
>
> There's also a really useful pattern with LEFT JOIN — if I filter on WHERE right.column IS NULL, I get all left records that have NO match in the right table. That's an anti-join — great for finding orphaned records or things that haven't happened yet.
>
> RIGHT JOIN is the mirror of LEFT JOIN — all right rows plus matching left. I generally rewrite these as LEFT JOINs by just swapping the table order, for consistency and readability.
>
> FULL OUTER JOIN returns all rows from both tables, with NULLs filling in wherever there's no match. I use it for data reconciliation scenarios — comparing two datasets to see what exists on one side but not the other.
>
> Then there's CROSS JOIN (Cartesian product — every combination of both tables, good for test data or grids) and SELF JOIN (a table joined to itself, useful for hierarchical data like org charts)."

---

### "How do indexes affect performance for reads vs writes?"

> "Indexes significantly speed up reads but add overhead to writes. For reads, an index allows SQL Server to seek directly to matching rows rather than scanning the whole table — the difference can be millions of rows scanned vs a handful of logical reads.
>
> For writes — INSERTs, UPDATEs, DELETEs — each index on the table must be maintained. A table with 10 indexes on it requires 10 index page updates for every insert. So the trade-off is: more indexes = faster reads, slower writes, more storage.
>
> In practice, I think about which columns are used in WHERE clauses, JOIN conditions, and ORDER BY — those are the candidates. I'll also look at the actual execution plan's missing index suggestions, though I don't blindly create everything it suggests — sometimes they'd create overlapping or redundant indexes."

---

## C# / .NET Questions

### "Explain async/await."

> "Async/await is .NET's mechanism for non-blocking asynchronous programming. When you `await` an operation — like a database call or HTTP request — the current thread is released back to the thread pool rather than sitting blocked waiting for the result. A state machine generated by the compiler records where the method paused, and when the awaited task completes, execution resumes from that point.
>
> The important distinction is that `async/await` is not about multithreading — it's about I/O concurrency. You're not spinning up new threads; you're efficiently using existing ones. This is why ASP.NET Core can serve thousands of concurrent requests with a small thread pool.
>
> Common mistakes I've seen: calling `.Result` or `.Wait()` on an async method in some contexts can cause deadlocks. Using `async void` outside of event handlers means exceptions are unobservable. And not passing `CancellationToken` through from the HTTP request means you keep doing database work even after the client has disconnected."

---

### "What are the SOLID principles?"

> "SOLID is five object-oriented design principles. Single Responsibility says a class should do one thing — one reason to change. Open/Closed says you should be able to add new behavior by adding new code, not modifying existing tested code — usually achieved through interfaces and new implementations.
>
> Liskov Substitution says a subclass should be fully substitutable for its base class — if substituting a subclass breaks behavior, the inheritance relationship is wrong. Interface Segregation says prefer many small focused interfaces over one fat interface that forces classes to implement things they don't need.
>
> And Dependency Inversion says high-level modules shouldn't depend on low-level concrete classes — both should depend on abstractions. That's the foundation of why we use interfaces and dependency injection in .NET."

---

### "What's the difference between Scoped, Transient, and Singleton in DI?"

> "These are the three service lifetimes in .NET's dependency injection container.
>
> Singleton creates one instance for the entire application lifetime — it's shared by every request and every class. Good for things like configuration objects, in-memory caches, or `IHttpClientFactory`.
>
> Scoped creates one instance per HTTP request. Everything that runs during a single request shares the same instance. This is what EF Core's `DbContext` uses by default — you get one unit of work per request.
>
> Transient creates a new instance every single time it's resolved from the container. Good for lightweight, stateless services.
>
> The important gotcha is what I call the captive dependency problem — if you inject a Scoped service into a Singleton, that Scoped service is captured and never disposed. It lives for the app lifetime instead of the request lifetime. .NET will actually throw an exception in development mode if you do this."

---

### "What's the N+1 query problem in EF Core?"

> "The N+1 problem happens when lazy loading is enabled and you load a list of entities, then access a navigation property in a loop — each access triggers a separate SQL query. So if you load 100 orders and access `order.Customer.Name` in a foreach, you get 1 query for the orders plus 100 queries for the customers — 101 queries total instead of 1.
>
> The fix is eager loading with `.Include()`, which generates a single JOIN query. Or for read-heavy scenarios, I'd use a projection with `.Select()` to a DTO — EF Core translates that to a JOIN in SQL and you only fetch exactly the columns you need. I also always use `.AsNoTracking()` for read-only queries to skip the change tracking overhead."

---

## Behavioral / Situational

### "Tell me about a time you improved SQL performance."

Have a story ready. Structure: Situation → what was slow → how you diagnosed it → what you changed → result. Even if a DBA was involved, you can describe the diagnostic process you went through together.

---

### "How comfortable are you with SQL Server given you've had DBAs?"

> "Very comfortable with the diagnostic and optimization side — I've always wanted to understand WHY things are slow, not just hand it off. I know how to read execution plans, identify missing indexes, spot parameter sniffing, and understand whether a problem is a query issue or a statistics/index maintenance issue. Where I've relied on the DBA team is for things like server-level configuration, index maintenance scheduling, and query store analysis on production. I'm excited to take more ownership of that in this role."
