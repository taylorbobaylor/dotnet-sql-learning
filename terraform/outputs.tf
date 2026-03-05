output "namespace" {
  description = "Kubernetes namespace both Helm releases were deployed into"
  value       = helm_release.sql_server.namespace
}

output "sql_server_status" {
  description = "sql-server Helm release status"
  value       = helm_release.sql_server.status
}

output "api_status" {
  description = "sql-demos-api Helm release status"
  value       = helm_release.sql_demos_api.status
}

output "api_url" {
  description = "Base URL for the benchmark API on Docker Desktop"
  value       = "http://localhost:${var.api_node_port}"
}

output "api_scalar_ui" {
  description = "Scalar OpenAPI UI — browse here to explore and test all endpoints"
  value       = "http://localhost:${var.api_node_port}/scalar"
}

output "connection_string" {
  description = "ADO.NET connection string for the local SQL Server NodePort endpoint"
  value       = "Server=localhost,${var.node_port};Database=InterviewDemoDB;User Id=sa;Password=${var.sa_password};TrustServerCertificate=True;"
  sensitive   = true
}

output "sqlcmd_connect" {
  description = "sqlcmd command to verify SQL Server connectivity"
  value       = "sqlcmd -S localhost,${var.node_port} -U sa -P \"<sa_password>\" -N disable -Q \"SELECT @@VERSION\""
}

# ---------------------------------------------------------------
# AWS DIFFERENCE:
# When using LoadBalancer services on EKS, output the NLB hostname:
#
# output "api_nlb_endpoint" {
#   description = "NLB DNS name for the API (assigned by AWS after LB is provisioned)"
#   value       = "Check: kubectl get svc sql-demos-api -n ${var.namespace}"
# }
# ---------------------------------------------------------------
