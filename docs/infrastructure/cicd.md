# CI/CD Pipelines

This project has two independent CI/CD pipelines: one for the documentation site and one for deploying infrastructure. Both run on **GitHub Actions**.

---

## Docs Pipeline — Already Running

The docs pipeline is live at [taylorbobaylor.github.io/dotnet-sql-learning](https://taylorbobaylor.github.io/dotnet-sql-learning/).

**Trigger:** push to `main` branch (or manual dispatch from the Actions tab).

**Flow:**

```
push to main
    └─▶ build job
            ├─ checkout (full history for git revision dates)
            ├─ setup Python + pip cache
            ├─ pip install -r requirements.txt
            ├─ mkdocs build --strict --site-dir _site
            └─ upload Pages artifact
    └─▶ deploy job
            └─ actions/deploy-pages@v4  →  GitHub Pages
```

The workflow lives at `.github/workflows/deploy-docs.yml`. It uses the official GitHub Pages Actions pattern — no third-party deployment token needed.

!!! note "Strict mode"
    `mkdocs build --strict` fails the build on any broken internal link or misconfigured nav entry. This keeps the published site consistent with the repo.

---

## Infrastructure Pipeline — Terraform + Helm

This pipeline applies the Terraform configuration (which in turn deploys the Helm chart) to a Kubernetes cluster. It is **not yet wired up as a GitHub Actions workflow** — for the local Docker Desktop workflow, you run Terraform manually. The section below shows what a production pipeline targeting AWS EKS would look like.

### Local workflow (Docker Desktop)

```bash
cd terraform

# First time only
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars — set sa_password at minimum

terraform init          # downloads kubernetes + helm providers
terraform plan          # preview: namespace + helm_release
terraform apply         # deploy

# When done
terraform destroy
```

Terraform uses the `hashicorp/helm` provider (`~> 2.12`) to deploy the local `helm/sql-server` chart. One `apply` creates the namespace and hands everything else to Helm (Secret, PVC, Deployment, Service).

### GitHub Actions — EKS deployment (template)

A production pipeline targeting AWS EKS would look like this. Store `sa_password` and AWS credentials as [GitHub Actions secrets](https://docs.github.com/en/actions/security-guides/encrypted-secrets).

```yaml
# .github/workflows/deploy-infra.yml  (example — not yet active)
name: Deploy Infrastructure

on:
  push:
    branches: [main]
    paths:
      - "terraform/**"
      - "helm/**"
  workflow_dispatch:

permissions:
  id-token: write   # needed for OIDC auth to AWS
  contents: read

jobs:
  terraform:
    runs-on: ubuntu-latest
    environment: production

    steps:
      - uses: actions/checkout@v4

      - name: Configure AWS credentials (OIDC — no long-lived keys)
        uses: aws-actions/configure-aws-credentials@v4
        with:
          role-to-assume: arn:aws:iam::${{ secrets.AWS_ACCOUNT_ID }}:role/github-actions-deploy
          aws-region: us-east-1

      - name: Setup Terraform
        uses: hashicorp/setup-terraform@v3
        with:
          terraform_version: "~1.6"

      - name: Terraform Init
        run: terraform init
        working-directory: terraform

      - name: Terraform Plan
        run: terraform plan -out=tfplan
        working-directory: terraform
        env:
          TF_VAR_sa_password: ${{ secrets.SA_PASSWORD }}
          TF_VAR_eks_cluster_name: ${{ secrets.EKS_CLUSTER_NAME }}

      - name: Terraform Apply
        run: terraform apply tfplan
        working-directory: terraform
```

Key things to note about the EKS pipeline:

**OIDC instead of access keys** — `aws-actions/configure-aws-credentials` with `role-to-assume` uses [GitHub's OIDC provider](https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/configuring-openid-connect-in-amazon-web-services) to exchange a short-lived token for AWS credentials. No long-lived `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` stored in GitHub.

**Sensitive variables via `TF_VAR_*`** — Terraform picks up `TF_VAR_sa_password` automatically and treats it as sensitive, so it never appears in plan output.

**`paths` filter** — The workflow only runs when Terraform or Helm files change, avoiding unnecessary deploys on docs-only commits.

---

## How the Two Tools Fit Together

```
terraform apply
    ├─ hashicorp/kubernetes provider  →  creates namespace
    └─ hashicorp/helm provider
            └─ helm_release "sql_server"
                    ├─ reads  helm/sql-server/Chart.yaml
                    ├─ reads  helm/sql-server/values.yaml
                    ├─ merges `set` overrides from terraform.tfvars
                    └─ applies Kubernetes manifests rendered by the chart
                            ├─ Secret   (sa password)
                            ├─ PVC      (5 Gi data volume)
                            ├─ Deployment (SQL Server pod)
                            └─ Service  (NodePort / LoadBalancer)
```

**Terraform** owns the _what_ (which cluster, which namespace, what password, what port). **Helm** owns the _how_ (the Kubernetes resource templates). This separation means you can iterate on the chart templates without touching Terraform state, and you can change Terraform variables without modifying YAML templates.

---

## AWS EKS — End-to-End Flow

For a complete picture of what changes when moving from Docker Desktop to AWS EKS, see [Kubernetes (Docker Desktop → AWS)](kubernetes.md#aws-eks-differences).

```
Developer pushes to main
    └─▶ deploy-docs.yml    →  MkDocs build  →  GitHub Pages
    └─▶ deploy-infra.yml   →  Terraform
                                  ├─ aws_eks_cluster data source  →  EKS credentials
                                  └─ helm_release
                                          ├─ image: mssql/server:2022-latest (x86_64)
                                          ├─ serviceType: LoadBalancer (NLB, internal)
                                          └─ storageClassName: gp3 (EBS CSI driver)
```
