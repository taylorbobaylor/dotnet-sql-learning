# SQL Performance Lab — Angular Dashboard

A clean Angular 21 + Angular Material dashboard for visualising the **Bad vs Fixed**
SQL stored-procedure benchmarks from the `.NET SqlDemos` project.

## Features

- 6 scenario cards — one per SQL antipattern
- Run individual scenarios or **Run All** at once
- Animated timing-bar comparison (bad vs fixed)
- Improvement factor badge (e.g. "233× faster")
- Summary table after running all scenarios
- Configurable API base URL (live-typed in the toolbar)
- Health-check indicator with live status
- Responsive grid layout (mobile-friendly)

## Prerequisites

| Tool | Version |
|------|---------|
| Node.js | 20+ |
| Angular CLI | `npm i -g @angular/cli` |
| .NET API | Running locally on port 5000 |

## Quick Start

```bash
# 1. Install dependencies
cd sql-dashboard
npm install

# 2. Make sure the .NET API is running
cd ../src/SqlDemosApi
dotnet run            # starts on http://localhost:5000

# 3. Start the Angular dev server (proxy forwards /scenarios and /health to :5000)
cd ../../sql-dashboard
npm start             # http://localhost:4200
```

The proxy config in `proxy.conf.json` automatically forwards `/scenarios` and
`/health` calls to `http://localhost:5000`, so you don't need to worry about CORS
during development.

## API URL Config

The toolbar has an **API Base URL** field.

- **Leave it blank** → Angular dev proxy handles routing (recommended for local dev)
- **Enter a URL** (e.g. `http://localhost:30080` for Kubernetes NodePort) → all
  requests go directly to that host. Make sure CORS is enabled on the API.

## Building for Production

```bash
npm run build
# Output: dist/sql-dashboard/
```

## Project Structure

```
sql-dashboard/
├── src/
│   ├── app/
│   │   ├── components/
│   │   │   ├── dashboard/          # Main page layout
│   │   │   └── scenario-card/      # Individual benchmark card
│   │   ├── models/
│   │   │   └── scenario.models.ts  # TypeScript interfaces
│   │   └── services/
│   │       └── benchmark.service.ts # HTTP calls to the API
│   ├── index.html
│   ├── main.ts
│   └── styles.scss
├── proxy.conf.json                 # Dev-server proxy config
├── angular.json
└── package.json
```

## Theme

Uses Angular Material's built-in **azure-blue** (Material Design 3) theme as the
base, with custom steel-blue branding to match the `.NET console app's` colour
scheme.
