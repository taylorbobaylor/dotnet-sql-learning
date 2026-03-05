terraform {
  required_version = ">= 1.5"

  required_providers {
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.27"
    }

    helm = {
      source  = "hashicorp/helm"
      version = "~> 2.12"
    }

    null = {
      source  = "hashicorp/null"
      version = "~> 3.0"
    }

    # ---------------------------------------------------------------
    # AWS DIFFERENCE:
    # Add the AWS provider to fetch cluster credentials from EKS.
    #
    # aws = {
    #   source  = "hashicorp/aws"
    #   version = "~> 5.0"
    # }
    # ---------------------------------------------------------------
  }
}


# ---------------------------------------------------------------
# PROVIDERS — Docker Desktop
# Both the kubernetes and helm providers read your local kubeconfig.
# ---------------------------------------------------------------
provider "kubernetes" {
  config_path    = "~/.kube/config"
  config_context = "docker-desktop"
}

provider "helm" {
  kubernetes {
    config_path    = "~/.kube/config"
    config_context = "docker-desktop"
  }
}

# ---------------------------------------------------------------
# AWS DIFFERENCE:
# Replace both provider blocks above with EKS data sources so
# Terraform fetches the cluster endpoint and auth token at plan time.
#
# data "aws_eks_cluster" "cluster" {
#   name = var.eks_cluster_name
# }
#
# data "aws_eks_cluster_auth" "cluster" {
#   name = var.eks_cluster_name
# }
#
# provider "kubernetes" {
#   host                   = data.aws_eks_cluster.cluster.endpoint
#   cluster_ca_certificate = base64decode(data.aws_eks_cluster.cluster.certificate_authority[0].data)
#   token                  = data.aws_eks_cluster_auth.cluster.token
# }
#
# provider "helm" {
#   kubernetes {
#     host                   = data.aws_eks_cluster.cluster.endpoint
#     cluster_ca_certificate = base64decode(data.aws_eks_cluster.cluster.certificate_authority[0].data)
#     token                  = data.aws_eks_cluster_auth.cluster.token
#   }
# }
#
# provider "aws" {
#   region = var.aws_region
# }
# ---------------------------------------------------------------


# ---------------------------------------------------------------
# BUILD — Docker image for sql-demos-api
#
# `null_resource` with a `local-exec` provisioner runs a shell
# command on the machine running Terraform (your laptop).
#
# The `triggers` map is the key concept: Terraform hashes these
# values and stores them in state. On the next `terraform apply`,
# if any trigger value has changed, Terraform destroys and re-creates
# the null_resource — which re-runs the provisioner.
#
# Here we hash the Dockerfile and every .cs/.csproj/.json file in
# src/SqlDemosApi/, so the image rebuilds automatically whenever
# code changes. If nothing changed, the build is skipped entirely.
#
# ---------------------------------------------------------------
# AWS DIFFERENCE:
# In CI/CD (GitHub Actions → ECR), replace this null_resource with
# a proper build-and-push step in your workflow:
#
#   - name: Build and push to ECR
#     run: |
#       aws ecr get-login-password | docker login ...
#       docker build -t $ECR_URI:$SHA .
#       docker push $ECR_URI:$SHA
#
# The Helm release then sets image.repository + image.tag to the ECR URI.
# ---------------------------------------------------------------
resource "null_resource" "build_api_image" {
  triggers = {
    # Rebuild when Dockerfile changes
    dockerfile = filemd5("${path.module}/../Dockerfile")

    # Rebuild when any file inside src/SqlDemosApi/ changes.
    # fileset returns a sorted set of relative paths; we hash each file's
    # content and join them so a single-byte change triggers a rebuild.
    src_hash = sha256(join(",", [
      for f in sort(fileset("${path.module}/../src/SqlDemosApi", "**/*")) :
      "${f}=${filemd5("${path.module}/../src/SqlDemosApi/${f}")}"
    ]))
  }

  provisioner "local-exec" {
    # working_dir resolves relative to the terraform/ directory, so
    # "../" is the repo root where the Dockerfile lives.
    command     = "docker build -t sql-demos-api:latest ."
    working_dir = "${path.module}/.."
  }
}


# ---------------------------------------------------------------
# HELM RELEASE — deploys the local helm/sql-server chart.
#
# Terraform is the single entry point: one `terraform apply`
# creates the namespace and deploys all Kubernetes resources
# (Secret, PVC, Deployment, Service) via the Helm chart.
#
# Values are passed from Terraform variables so there is one
# source of truth — terraform.tfvars — for both infrastructure
# config and chart configuration.
# ---------------------------------------------------------------
resource "helm_release" "sql_server" {
  name             = "sql-server"
  chart            = "${path.module}/../helm/sql-server"
  namespace        = var.namespace
  create_namespace = true

  # SA password injected from the sensitive Terraform variable.
  # The chart writes it into a Kubernetes Secret; it never appears
  # in values files or source control.
  set {
    name  = "sqlServer.saPassword"
    value = var.sa_password
  }

  set {
    name  = "sqlServer.nodePort"
    value = tostring(var.node_port)
  }

  set {
    name  = "storage.size"
    value = var.storage_size
  }

  # ---------------------------------------------------------------
  # AWS DIFFERENCE:
  # Override image and service type for x86_64 EKS nodes:
  #
  # set {
  #   name  = "sqlServer.image"
  #   value = "mcr.microsoft.com/mssql/server:2022-latest"
  # }
  #
  # set {
  #   name  = "sqlServer.serviceType"
  #   value = "LoadBalancer"
  # }
  #
  # Enable EBS-backed storage (requires EBS CSI driver add-on):
  #
  # set {
  #   name  = "storage.storageClassName"
  #   value = "gp3"
  # }
  # ---------------------------------------------------------------
}


# ---------------------------------------------------------------
# HELM RELEASE — sql-demos-api (ASP.NET Core Minimal API)
#
# Deploys the benchmark API as a Kubernetes Deployment + Service.
# Unlike the SQL Server chart (which is infrastructure), this is
# the application layer — the key difference from an API perspective:
#
#   SQL Server  → StatefulSet-style workload, PVC, never scaled out
#   API         → Deployment, stateless, can scale replicas freely
#
# Prerequisites: build the image locally first:
#   docker build -t sql-demos-api .
#
# The API connects to SQL Server via in-cluster DNS:
#   sql-server.sql-demo.svc.cluster.local:1433
# ---------------------------------------------------------------
resource "helm_release" "sql_demos_api" {
  name             = "sql-demos-api"
  chart            = "${path.module}/../helm/sql-demos-api"
  namespace        = var.namespace
  create_namespace = false  # namespace already created by sql_server release above

  # Wait for both: SQL Server to be deployed AND the image to be built.
  # Without null_resource.build_api_image here, Terraform might try to
  # deploy the API chart before the image exists, causing ErrImageNeverPull.
  depends_on = [helm_release.sql_server, null_resource.build_api_image]

  # Build the connection string with the SA password injected at apply time.
  # The chart stores this in a Kubernetes Secret — never in plain ConfigMap.
  set {
    name  = "connectionString"
    value = "Server=sql-server.${var.namespace}.svc.cluster.local,1433;Database=InterviewDemoDB;User Id=sa;Password=${var.sa_password};TrustServerCertificate=True;"
  }

  set {
    name  = "service.nodePort"
    value = tostring(var.api_node_port)
  }

  # ---------------------------------------------------------------
  # AWS DIFFERENCE:
  # Push the image to ECR and override repository + pullPolicy:
  #
  # set {
  #   name  = "image.repository"
  #   value = "<account>.dkr.ecr.<region>.amazonaws.com/sql-demos-api"
  # }
  # set {
  #   name  = "image.pullPolicy"
  #   value = "Always"
  # }
  #
  # Switch to ClusterIP + Ingress (ALB) for internet-facing access:
  #
  # set {
  #   name  = "service.type"
  #   value = "ClusterIP"
  # }
  # ---------------------------------------------------------------
}
