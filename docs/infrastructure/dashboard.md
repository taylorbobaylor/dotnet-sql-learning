# Angular Dashboard

`sql-dashboard/` is the front-end for the SQL Performance Lab. It is an **Angular 21 SPA** that visualises all 6 benchmark scenarios — showing timing, row counts, and the speedup factor for every bad-vs-fixed stored procedure pair.

---

## What It Shows

| Area | Detail |
|---|---|
| **Scenario cards** | One card per scenario — run individually and see bad/fixed timing side-by-side |
| **Run All** | Fires all 6 scenarios in a single request and populates every card at once |
| **Summary table** | Sortable overview of all 6 results: bad time, fixed time, speedup factor, row count |
| **Health indicator** | Live status badge in the toolbar that hits `GET /health` on the API |

---

## Running Locally (dev server)

The dev server proxies `/health` and `/scenarios` to the API running in Kubernetes so there are no CORS issues.

```bash
# The k8s API must already be running at localhost:30080
cd sql-dashboard
npm install   # first time only
npm start     # ng serve on http://localhost:4200
```

Open **http://localhost:4200** in your browser.

!!! note "API must be up first"
    The dev server proxy forwards API calls to `http://localhost:30080`. Make sure the Kubernetes stack is running (`terraform apply`) before starting the dev server, or the health check will show "API unreachable".

The proxy is configured in `proxy.conf.json`:

```json
{
  "/scenarios": { "target": "http://localhost:30080", ... },
  "/health":    { "target": "http://localhost:30080", ... }
}
```

---

## Kubernetes Deployment

`terraform apply` builds the Docker image and deploys the dashboard alongside the API. Once up, open:

```
http://localhost:30081
```

### How it works in-cluster

```
Browser → NodePort 30081 → nginx pod
                               ├─ GET /            → serves index.html (SPA)
                               ├─ GET /health      → proxy_pass → sql-demos-api:80
                               └─ GET /scenarios/* → proxy_pass → sql-demos-api:80
```

nginx proxies all API routes to the `sql-demos-api` Kubernetes Service using in-cluster DNS (`sql-demos-api` short name resolves within the same namespace). The Angular build uses an empty `apiBaseUrl` so all requests are relative — nginx handles routing them to the right backend. No CORS headers are needed.

### Build

The Dockerfile is a two-stage build:

```
Stage 1 (node:22-alpine)   npm ci + ng build --configuration production
Stage 2 (nginx:alpine)     copies dist/sql-dashboard/browser → /usr/share/nginx/html
```

The nginx `default.conf` is **not** baked into the image — it is injected at runtime via a Kubernetes ConfigMap (see `helm/sql-dashboard/templates/configmap.yaml`). This makes it easy to change the upstream API host without rebuilding the image.

---

## Project Structure

```
sql-dashboard/
├── Dockerfile                        Multi-stage build (node build → nginx runtime)
├── proxy.conf.json                   Dev-server proxy: /health + /scenarios → localhost:30080
├── angular.json                      Build config; swaps environment files per configuration
├── src/
│   ├── index.html                    Shell; links favicon.svg + Google Fonts
│   ├── environments/
│   │   ├── environment.ts            Production: apiBaseUrl = '' (relative, nginx proxies)
│   │   └── environment.development.ts  Dev: apiBaseUrl = '' (Angular proxy handles routing)
│   └── app/
│       ├── services/
│       │   └── benchmark.service.ts  HTTP calls to /health, /scenarios, /scenarios/all
│       ├── models/
│       │   └── scenario.models.ts    TypeScript interfaces matching API JSON
│       └── components/
│           ├── dashboard/            Main page — toolbar, hero, scenario grid, summary table
│           └── scenario-card/        Individual scenario card with run button + result display

helm/sql-dashboard/
├── Chart.yaml
├── values.yaml                       image, nodePort (30081), apiServiceHost
└── templates/
    ├── _helpers.tpl
    ├── configmap.yaml                nginx default.conf — proxies /health + /scenarios to API
    ├── deployment.yaml               Mounts ConfigMap; liveness/readiness on GET /
    └── service.yaml                  NodePort 30081
```

---

## Terraform Integration

The dashboard is deployed as the last step in `terraform apply`, after the API is ready:

```
null_resource "build_dashboard_image"   docker build -t sql-dashboard:latest sql-dashboard/
        ↓ (depends_on)
helm_release "sql_demos_api"
        ↓ (depends_on)
helm_release "sql_dashboard"            deploys helm/sql-dashboard/ at NodePort 30081
```

Terraform re-triggers the Docker build automatically when `sql-dashboard/Dockerfile` or any file under `sql-dashboard/src/` changes. No manual `docker build` needed.

| Variable | Default | Description |
|---|---|---|
| `dashboard_node_port` | `30081` | NodePort exposed on Docker Desktop |

Output after `terraform apply`:

```
dashboard_url = "http://localhost:30081"
```

---

## Troubleshooting

### Dashboard loads but shows "API unreachable"

The health check failed. Confirm the API pod is running and the service is reachable:

```bash
kubectl get pods -n sql-demo
curl http://localhost:30080/health
```

If the API pod is `Running` but health fails, check API logs:

```bash
kubectl logs deployment/sql-demos-api -n sql-demo
```

### Dashboard pod stuck in `ErrImageNeverPull`

The Docker image wasn't built before Terraform deployed the chart. Force a rebuild:

```bash
terraform taint null_resource.build_dashboard_image
terraform apply
```

### Page loads but spinning / blank after teardown

There is a brief window between `terraform destroy` completing and `terraform apply` finishing where nothing is listening on port 30081. The browser will spin until the pod is ready. Wait for:

```bash
kubectl get pods -n sql-demo -w
# Wait until sql-dashboard pod shows Running + READY 1/1
```

### Dev server shows "API unreachable" (`npm start`)

The dev proxy needs the API running at `localhost:30080`. Either:

- Run `terraform apply` first to bring up the k8s stack, or
- Start the API locally: `cd src/SqlDemosApi && dotnet run` (it binds to `localhost:5000` by default — update `proxy.conf.json` target to `http://localhost:5000` if using the local dev server)
