variable "namespace" {
  description = "Kubernetes namespace both Helm releases are deployed into"
  type        = string
  default     = "sql-demo"
}

variable "sa_password" {
  description = "SA password — injected into both the SQL Server chart and the API connection string (sensitive)"
  type        = string
  sensitive   = true
}

variable "storage_size" {
  description = "PersistentVolumeClaim size for the SQL Server data directory"
  type        = string
  default     = "5Gi"
}

variable "node_port" {
  description = "NodePort to expose SQL Server on (Docker Desktop only) — connect via localhost:<node_port>"
  type        = number
  default     = 31433
}

variable "api_node_port" {
  description = "NodePort to expose the sql-demos-api on (Docker Desktop only) — browse to http://localhost:<api_node_port>/scalar"
  type        = number
  default     = 30080
}

variable "dashboard_node_port" {
  description = "NodePort to expose the sql-dashboard on (Docker Desktop only) — browse to http://localhost:<dashboard_node_port>"
  type        = number
  default     = 30081
}

# ---------------------------------------------------------------
# AWS DIFFERENCE:
# Add these variables when targeting EKS:
#
# variable "eks_cluster_name" {
#   description = "Name of the EKS cluster to deploy into"
#   type        = string
# }
#
# variable "aws_region" {
#   description = "AWS region"
#   type        = string
#   default     = "us-east-1"
# }
# ---------------------------------------------------------------
