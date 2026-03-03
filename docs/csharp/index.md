# C# Language Overview

With 8-9 years of experience, you likely know most of this intuitively. This section focuses on the **conceptual explanations** you'll need to articulate clearly in an interview, not just use in code.

## Topics in This Section

- **[Async / Await](async-await.md)** — The threading model, common mistakes, and best practices
- **[SOLID Principles](solid-principles.md)** — Each principle with a real C# example
- **[Dependency Injection](dependency-injection.md)** — DI in .NET Core, lifetimes, and common patterns
- **[Generics & LINQ](generics-linq.md)** — Type safety, deferred execution, and common operators

## Key C# Concepts to Know Cold

**Value types vs reference types:** Value types (int, struct, bool, decimal) live on the stack (or inline in an object). Reference types (class, string, arrays) live on the heap with a pointer on the stack. Important for understanding equality, mutation, and GC behavior.

**`string` vs `StringBuilder`:** `string` is immutable — every concatenation creates a new string object. Use `StringBuilder` for building strings in loops.

**`IEnumerable` vs `IQueryable`:** `IEnumerable` executes in memory (LINQ to Objects). `IQueryable` translates to SQL (LINQ to SQL, EF Core) — the expression tree is sent to the database.

**`==` vs `.Equals()`:** For value types, both compare by value. For reference types, `==` compares references by default unless overridden (e.g., `string` overrides it). `.Equals()` can be overridden to compare by value.

**`null` vs `default`:** For reference types, `default` is `null`. For value types, `default` is 0/false/etc. In generics, `default(T)` is the correct way to get the zero value.

## C# Version Features Worth Knowing

| Version | Notable Features |
|---|---|
| C# 6 | String interpolation, null-conditional `?.`, nameof |
| C# 7 | Tuples, pattern matching, `out` variables |
| C# 8 | Nullable reference types, async streams, switch expressions |
| C# 9 | Records, init-only properties, top-level statements |
| C# 10 | Global usings, file-scoped namespaces, record structs |
| C# 11 | Required members, raw string literals |
| C# 12 | Primary constructors, collection expressions |
