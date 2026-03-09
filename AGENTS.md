## Repository purpose

This repo is a **.NET + SQL benchmarking lab** with an accompanying **Angular Material dashboard UI**. The back end hosts a set of SQL scenarios (different query shapes, indexing strategies, parameterization patterns, etc.), and the front end will provide a dashboard-style experience to **run scenarios, compare timings, and understand performance trade-offs**.

## High-level architecture

- **Backend**
  - **Tech**: ASP.NET Core Minimal API, targeting **.NET 10**.
  - **Projects**
    - `src/SqlDemosApi` – HTTP API surface for running benchmarks and exposing metadata/results.
    - `src/SqlDemos.Shared` – shared domain models, DTOs, and utilities.
  - **Data store**: SQL Server, with multiple demo schemas / queries to illustrate performance patterns.
  - **Containerization**: Dockerfile at repo root builds and runs `SqlDemosApi` (multi-stage build).

- **Frontend**
  - **Tech**: Angular with **Angular Material**, dashboard-style UI.
  - **App**: `sql-dashboard` workspace.
  - **Purpose**: Browse SQL scenarios, trigger benchmark runs, and visualize results over time (tables, charts, comparison views).

## Design goals and constraints

- **Use .NET 10–compatible APIs**
  - New code should assume **.NET 10** as the baseline target.
  - Prefer modern minimal-API style, `IResult` endpoints, and dependency injection patterns that work smoothly on .NET 10.

- **Benchmark clarity over "magic"**
  - Code should make performance behaviors obvious (indexes used, joins, filters, parameterization).
  - Avoid heavy abstractions that hide what SQL is actually being executed.
  - When adding new scenarios, keep the C# and SQL simple and explicit so they can be used for teaching.

- **Reproducibility**
  - Benchmarks should be easy to re-run locally and in containers.
  - If you introduce randomness, provide a way to fix seeds or data sets for consistent comparisons.

- **Safe experimentation**
  - Assume the database is **non-production** and can be mutated freely, but:
    - Do not introduce destructive migrations that break existing demo scenarios without adding replacements.
    - Prefer adding new schemas/tables or clearly versioned scenarios instead of altering existing ones incompatibly.

## Backend guidelines for agents

- **Project structure**
  - Keep new API endpoints in `SqlDemosApi` organized by scenario domain (e.g., `Scenarios`, `Indexes`, `Joins`).
  - Share cross-cutting models and utilities in `SqlDemos.Shared`, not in the API project.

- **SQL patterns**
  - Favor **parameterized queries** (ADO.NET, Dapper, or EF Core with explicit SQL) to avoid skewed benchmarks from plan cache issues.
  - When demonstrating an "anti-pattern", clearly separate it (e.g., `*_bad` endpoints or scenarios) and document intent in code comments.

- **Configuration**
  - Keep connection strings in `appsettings*.json` / environment variables; do **not** hard-code credentials.
  - For new configuration entries, follow .NET configuration binding conventions (`Section__Key` for env vars).

- **Testing / validation**
  - When feasible, add simple integration tests that:
    - Call the new scenario endpoint.
    - Validate that it returns plausible metrics (e.g., non-negative durations, row counts).

## Frontend (Angular Material dashboard) guidelines

- **Tech expectations**
  - Use **Angular + Angular Material** with a **dashboard layout** (toolbar, sidenav, responsive grid).
  - Prefer strongly typed **models and services** under `sql-dashboard/src/app` (e.g., `models`, `services`, `components` directories).

- **API integration**
  - Centralize HTTP calls in services (e.g., `BenchmarkService`) that:
    - Use Angular `HttpClient`.
    - Mirror backend contracts with TypeScript interfaces.
    - Handle base URLs and proxies via Angular environment config / `proxy.conf.json`, not hard-coded URLs.

- **UI/UX**
  - Use Angular Material components for:
    - Scenario lists (`mat-table`, `mat-list`, `mat-card`).
    - Filters and run controls (`mat-form-field`, `mat-select`, `mat-slide-toggle`, `mat-button`).
    - Result visualization (tables; charts if/when a chart library is added).
  - Keep the overall feel **dashboard-like**: concise, information-dense, with clear indication of which scenario is running and recent runs history.

- **State & navigation**
  - Favor simple, observable-based state inside feature components/services before adding global state management.
  - Use Angular routing for scenario/detail-type views instead of overloading single components.

## Tasks that are safe for agents to automate

- **Safe to do**
  - Add new benchmark scenarios (C# + SQL) as long as they are clearly named and do not silently change existing behavior.
  - Extend the Angular dashboard with new cards, filters, or views for additional scenarios/metrics.
  - Improve observability: logging, basic metrics, better error payloads from the API.
  - Add or refine Docker / local-dev scripts that **do not** modify production-like resources.

- **Be cautious**
  - Changing database schema: prefer additive changes; avoid breaking existing demo queries.
  - Upgrading dependencies: ensure changes remain compatible with **.NET 8** and the current Angular major version.
  - Changing public API contracts that the dashboard depends on; coordinate backend and frontend changes together.

## How agents should work in this repo

- **General behavior**
  - Follow existing code style in each project (naming, folder layout, nullability, etc.).
  - Keep commits and changes focused; group related backend and frontend updates logically.
  - Prefer small, composable functions and services over large monoliths.

- **When uncertain**
  - Assume **teaching and benchmarking** are primary goals: favor clarity and observability over micro-optimizations.
  - If a choice affects architecture (e.g., new shared libraries, large refactors), propose the approach in comments or documentation first rather than silently restructuring everything.

This file is the **single source of truth for AI agents** working in this repository. When in doubt, prefer **.NET 8–compatible choices, explicit SQL, and clear, dashboard-style UI** that helps users understand how different SQL approaches behave.

