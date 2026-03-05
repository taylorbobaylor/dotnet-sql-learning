# Benchmark API

`src/SqlDemosApi/` is a **.NET 10 ASP.NET Core Minimal API** that exposes all 6 SQL benchmark scenarios as JSON endpoints. It is the application layer deployed to Kubernetes alongside SQL Server.

---

## Why an API instead of just the console app?

The console app is great for local dev — run it, see the table, done. But it can't be deployed to Kubernetes in any meaningful way (no HTTP, no probes, no service discovery). The API solves this:

| | Console App | API |
|---|---|---|
| Local dev | `dotnet run -- all` | `dotnet run` then hit `/scenarios/all` |
| Kubernetes | Not suitable (no HTTP) | **Deployment** + Service + liveness probes |
| Testing | Terminal output | Postman / Scalar UI / `curl` |
| Consumed by other services | No | Yes — returns structured JSON |

---

## Running locally

```bash
cd src/SqlDemosApi
dotnet run
```

The app starts on `http://localhost:5000` by default. Open the Scalar UI to explore all endpoints:

```
http://localhost:5000/scalar
```

The OpenAPI spec is at `http://localhost:5000/openapi/v1.json` and can be imported directly into Postman or Insomnia.

---

## Endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/health` | Liveness probe — returns `{ status: "healthy" }` |
| `GET` | `/scenarios` | List all 6 scenarios (metadata only, no DB calls) |
| `GET` | `/scenarios/all` | Run all 6 bad-vs-fixed pairs, return timing JSON |
| `GET` | `/scenarios/{id}` | Run a single scenario (1–6) |

### Example response — `GET /scenarios/1`

```json
{
  "id": 1,
  "name": "Cursor Catastrophe",
  "antipattern": "Row-by-row cursor loop UPDATE",
  "fix": "Set-based UPDATE",
  "runs": [
    {
      "procedure": "usp_Bad_RecalcOrderTotals",
      "label": "Cursor (row-by-row)",
      "elapsedMs": 4200,
      "rowCount": 55000,
      "isBad": true
    },
    {
      "procedure": "usp_Fixed_RecalcOrderTotals",
      "label": "Set-based UPDATE",
      "elapsedMs": 18,
      "rowCount": 55000,
      "isBad": false
    }
  ],
  "improvementFactor": 233.3,
  "ranAt": "2026-03-05T12:00:00+00:00"
}
```

---

## Postman collection

Import `postman/SqlDemosApi.postman_collection.json` to get pre-built requests for every endpoint, including saved example responses and inline descriptions of each antipattern.

The collection uses a `baseUrl` variable so you can switch between environments with one change:

| Environment | `baseUrl` |
|---|---|
| Local `dotnet run` | `http://localhost:5000` |
| Docker Desktop Kubernetes | `http://localhost:30080` |
| AWS EKS (NLB / Ingress) | `http://<your-endpoint>` |

---

## Configuration

The connection string is read from `appsettings.json` for local dev. In Kubernetes, the Helm chart injects it via the `ConnectionStrings__InterviewDemo` environment variable (sourced from a Secret), which overrides `appsettings.json` at runtime.

```json
// appsettings.json — local default
{
  "ConnectionStrings": {
    "InterviewDemo": "Server=localhost,1433;Database=InterviewDemoDB;..."
  }
}
```

In-cluster the service connects to SQL Server by Kubernetes DNS:

```
Server=sql-server.sql-demo.svc.cluster.local,1433;...
```

---

## Project structure

```
src/SqlDemosApi/
├── SqlDemosApi.csproj      SDK.Web, Dapper, SqlClient, OpenAPI, Scalar
├── Program.cs              Minimal API setup + all route handlers
├── BenchmarkService.cs     Executes all 6 scenarios, returns ScenarioResult
├── Models.cs               ProcRun, ScenarioResult, AllScenariosResult, ScenarioInfo
└── appsettings.json        Connection string (localhost default for local dev)
```

---

## Kubernetes deployment

See [Kubernetes (Docker Desktop → AWS)](../infrastructure/kubernetes.md) for the full deployment walkthrough. The short version:

```bash
# 1. Build the image
docker build -t sql-demos-api .

# 2. Apply via Terraform (deploys sql-server + sql-demos-api in one command)
cd terraform && terraform apply

# 3. Open the Scalar UI in Kubernetes
open http://localhost:30080/scalar
```

The Helm chart (`helm/sql-demos-api/`) creates a **Deployment** and a **NodePort Service**. The Deployment includes `livenessProbe` and `readinessProbe` that both hit `GET /health` — Kubernetes will route traffic to the pod only when the readiness probe succeeds, and will restart it if the liveness probe starts failing.
