# Kubernetes with Terraform + Helm

The `terraform/` directory deploys both SQL Server and the benchmark API to Kubernetes using three HashiCorp providers working together:

- **[Terraform Kubernetes provider](https://registry.terraform.io/providers/hashicorp/kubernetes/latest)** — creates the namespace
- **[Terraform Helm provider](https://registry.terraform.io/providers/hashicorp/helm/latest)** — deploys both Helm charts via `helm_release` resources
- **[Terraform null provider](https://registry.terraform.io/providers/hashicorp/null/latest)** — runs `docker build` locally before the API chart deploys

The instructions below target **Docker Desktop** as the quickest local path, with inline `# AWS DIFFERENCE:` comments throughout the `.tf` and Helm files explaining what changes for **AWS EKS**.

---

## How It Works

```
terraform apply
    ├─ kubernetes provider  →  creates namespace "sql-demo"
    │
    ├─ null_resource "build_api_image"
    │       └─ local-exec: docker build -t sql-demos-api:latest .
    │          (only re-runs when Dockerfile or src/SqlDemosApi/ changes)
    │
    └─ helm provider
            ├─ helm_release "sql_server"
            │       ├─ chart: helm/sql-server/
            │       ├─ set: sqlServer.saPassword
            │       ├─ set: sqlServer.nodePort   → 31433
            │       └─ set: storage.size
            │
            └─ helm_release "sql_demos_api"   (depends_on: sql_server + build_api_image)
                    ├─ chart: helm/sql-demos-api/
                    ├─ set: connectionString    (in-cluster DNS to sql-server)
                    └─ set: service.nodePort    → 30080
```

**Terraform is the single entry point.** One `terraform apply` builds the Docker image (if needed), creates the namespace, deploys both Helm charts, and wires up all Kubernetes resources — Secrets, PVC, Deployments, and Services. You never run `helm install` or `docker build` manually.

---

## Prerequisites

- [Terraform](https://developer.hashicorp.com/terraform/install) >= 1.5
  ```bash
  brew install terraform
  ```
- [Helm](https://helm.sh/docs/intro/install/) >= 3 *(optional for standalone use; required by Terraform internally)*
  ```bash
  brew install helm
  ```
- Docker Desktop with Kubernetes enabled
  *(Settings → Kubernetes → Enable Kubernetes)*
- `kubectl` context set to Docker Desktop:
  ```bash
  kubectl config use-context docker-desktop
  ```

---

## Deploy

```bash
cd terraform

# 1. Copy the example vars file and set your password
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars — at minimum set sa_password

# 2. Initialise Terraform (downloads kubernetes + helm + null providers)
terraform init

# 3. Preview what will be created
terraform plan

# 4. Apply — builds image, deploys sql-server + sql-demos-api
terraform apply
```

`terraform apply` does three things in order: builds the `sql-demos-api` Docker image (via `null_resource`), deploys the `sql-server` Helm chart (Secret, PVC, Deployment, Service), then deploys the `sql-demos-api` Helm chart once SQL Server is ready. SQL Server takes ~30 seconds to initialise after its pod starts.

---

## Verify

```bash
# Watch both pods come up
kubectl get pods -n sql-demo -w

# Confirm the service NodePorts
kubectl get svc -n sql-demo

# Check what Helm deployed
helm list -n sql-demo
```

Once both pods show `Running` and `READY 1/1`:

**SQL Server** — connect from VS Code MSSQL or DataGrip:
```
Server:   localhost,31433
Login:    sa
Password: <your sa_password>
```

**API** — open the Scalar UI in your browser:
```
http://localhost:30080/scalar
```

Or hit it directly:
```bash
curl http://localhost:30080/health
curl http://localhost:30080/scenarios/1
```

!!! note "Port difference vs Docker Compose"
    The Kubernetes NodePorts are `31433` (SQL Server) and `30080` (API) — not the
    default ports. Run `init-db.sh` with the overridden server address to seed the database:
    ```bash
    SERVER="localhost,31433" bash docker/init-db.sh
    ```

---

## Troubleshooting

### `terraform apply` times out with "context deadline exceeded"

Helm waits for all pods to reach `Ready` before marking a release as deployed (default timeout: 5 minutes). If a pod never becomes ready, Terraform fails with `context deadline exceeded`. Open a second terminal *while apply is running* and check the pod status:

```bash
kubectl get pods -n sql-demo
```

The `STATUS` column points you straight at the cause:

| Status | Cause | Fix |
|---|---|---|
| `ErrImageNeverPull` | Image not built locally; `imagePullPolicy: Never` | Run `terraform apply` — the `null_resource` builds it automatically |
| `ImagePullBackOff` | Tried to pull from registry but failed | Check image name / registry credentials |
| `CrashLoopBackOff` | Container starts but immediately crashes | Check logs (see below) |
| `Pending` | Pod can't be scheduled | Check node resources (`kubectl describe node`) |
| `Running` but not Ready | App started but health probe failing | Check logs; probe hits `GET /health` on port 8080 |

### Read the full pod story

```bash
# Full diagnostics — scroll to the Events section at the bottom
kubectl describe pod <pod-name> -n sql-demo

# App stdout/stderr
kubectl logs <pod-name> -n sql-demo

# Logs from the previous crash (CrashLoopBackOff)
kubectl logs <pod-name> -n sql-demo --previous
```

The Events section in `kubectl describe` is the most useful first stop — it narrates exactly what Kubernetes tried and where it failed.

### Quick diagnostic cheatsheet

```bash
# Everything in the namespace at once
kubectl get all -n sql-demo

# Watch pods live (auto-refreshes)
kubectl get pods -n sql-demo -w

# Force Terraform to rebuild the image on the next apply
# (deletes the null_resource from state so its triggers re-evaluate)
terraform taint null_resource.build_api_image
terraform apply
```

### Why the `null_resource` avoids this problem

The `null_resource.build_api_image` resource in `main.tf` runs `docker build -t sql-demos-api:latest .` as part of `terraform apply`, *before* the API Helm chart deploys. Its `triggers` block hashes the `Dockerfile` and every file in `src/SqlDemosApi/` — if nothing has changed since the last apply, the build is skipped. If anything changed, it rebuilds automatically. This means you should never see `ErrImageNeverPull` from a fresh checkout.

---

## Tear Down

```bash
terraform destroy
```

This removes the Helm release (and all chart resources) and the namespace. Terraform tracks everything it created, so nothing is left orphaned.

---

## Project Structure

```
terraform/
├── main.tf                  # providers + null_resource (docker build) + helm_release resources
├── variables.tf             # namespace, sa_password, storage_size, node_port, api_node_port
├── outputs.tf               # api_url, api_scalar_ui, connection_string, helm status
└── terraform.tfvars.example # copy to terraform.tfvars (gitignored)

helm/
├── sql-server/
│   ├── Chart.yaml               # chart name, version, appVersion
│   ├── values.yaml              # default values (Terraform overrides via `set`)
│   └── templates/
│       ├── _helpers.tpl          # shared label helpers
│       ├── secret.yaml           # SA password Kubernetes Secret
│       ├── pvc.yaml              # PersistentVolumeClaim for SQL data directory
│       ├── deployment.yaml       # SQL Server Deployment (1 replica)
│       └── service.yaml          # NodePort service (port 31433)
└── sql-demos-api/
    ├── Chart.yaml
    ├── values.yaml              # image, containerPort, service.nodePort, connectionString
    └── templates/
        ├── _helpers.tpl
        ├── secret.yaml           # connection string Kubernetes Secret
        ├── deployment.yaml       # API Deployment with liveness/readiness probes on /health
        └── service.yaml          # NodePort service (port 30080)

Dockerfile                   # multi-stage build for sql-demos-api (built by null_resource)
src/SqlDemosApi/             # ASP.NET Core Minimal API source
```

---

## Customising Values

Terraform passes values into the Helm chart via `set` blocks in `main.tf`. You can also supply your own `values.yaml` overrides via Terraform's `values` argument on `helm_release`, or (for one-off testing) run Helm directly:

```bash
# Test the chart rendering without deploying
helm template sql-server helm/sql-server \
  --set sqlServer.saPassword=MyPassword123 \
  --namespace sql-demo

# Install or upgrade the chart directly (bypasses Terraform state)
helm upgrade --install sql-server helm/sql-server \
  --namespace sql-demo \
  --create-namespace \
  --set sqlServer.saPassword=MyPassword123
```

!!! warning
    Running `helm upgrade` directly will cause Terraform state drift — subsequent `terraform plan` will show the release as out-of-date. Stick to `terraform apply` for managed deployments.

---

## AWS EKS Differences

Every AWS-specific change is annotated with `# AWS DIFFERENCE:` comments inline in the `.tf` and Helm files. Here is the summary:

### Providers

On Docker Desktop, both providers read your local kubeconfig. On EKS, Terraform fetches the cluster endpoint and auth token at plan time:

```hcl
data "aws_eks_cluster"      "cluster" { name = var.eks_cluster_name }
data "aws_eks_cluster_auth" "cluster" { name = var.eks_cluster_name }

provider "kubernetes" {
  host                   = data.aws_eks_cluster.cluster.endpoint
  cluster_ca_certificate = base64decode(data.aws_eks_cluster.cluster.certificate_authority[0].data)
  token                  = data.aws_eks_cluster_auth.cluster.token
}

provider "helm" {
  kubernetes {
    host                   = data.aws_eks_cluster.cluster.endpoint
    cluster_ca_certificate = base64decode(data.aws_eks_cluster.cluster.certificate_authority[0].data)
    token                  = data.aws_eks_cluster_auth.cluster.token
  }
}
```

### Image

`azure-sql-edge` is ARM64-only (Apple Silicon). EKS nodes are typically x86_64. Override via a Terraform `set` block:

```hcl
set {
  name  = "sqlServer.image"
  value = "mcr.microsoft.com/mssql/server:2022-latest"
}
```

If you run **Graviton (ARM64) EKS nodes**, `azure-sql-edge` works as-is.

### Managed Alternative — Amazon RDS

For any real workload, **RDS for SQL Server** is worth considering over running SQL Server in a pod. Add the `aws` provider and use the `aws_db_instance` Terraform resource instead of the Helm release — you get automated backups, Multi-AZ failover, and storage autoscaling without managing any of it yourself.

### Storage

Override `storageClassName` via a Terraform `set` block and enable the EBS CSI driver:

```hcl
set {
  name  = "storage.storageClassName"
  value = "gp3"
}
```

```bash
aws eks create-addon --cluster-name <your-cluster> --addon-name aws-ebs-csi-driver
```

### Secrets

Don't store passwords in `terraform.tfvars` committed to source control. Use **AWS Secrets Manager** and the **External Secrets Operator** to sync secrets into the cluster at deploy time, keeping plaintext out of Terraform state entirely.

### Service / Networking

Override `serviceType` to provision an AWS NLB. Keep it **internal** — never expose SQL Server to the internet:

```hcl
set {
  name  = "sqlServer.serviceType"
  value = "LoadBalancer"
}
```

Then add NLB annotations to `helm/sql-server/templates/service.yaml`:

```yaml
annotations:
  service.beta.kubernetes.io/aws-load-balancer-type:   nlb
  service.beta.kubernetes.io/aws-load-balancer-scheme: internal
```

For a .NET API running inside the same cluster, use `ClusterIP` and connect by pod DNS:

```
sql-server.sql-demo.svc.cluster.local:1433
```
