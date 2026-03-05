# ============================================================
# Multi-stage build for SqlDemosApi (ASP.NET Core Minimal API)
# Build context: repo root
#
#   docker build -t sql-demos-api .
#   docker run --rm -p 5000:8080 sql-demos-api
# ============================================================

# ----- build stage -------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy source and publish.
# Note: --no-restore is intentionally omitted. Microsoft.AspNetCore.OpenApi
# is an ASP.NET Core framework package that the SDK resolves from its own
# internal paths; skipping restore causes NETSDK1064 on publish even though
# the restore step above succeeds. Letting publish restore is reliable.
COPY src/SqlDemosApi/ src/SqlDemosApi/
RUN dotnet publish src/SqlDemosApi/SqlDemosApi.csproj \
      -c Release \
      -o /app/out

# ----- runtime stage -----------------------------------------
# Use aspnet (not runtime) — includes the ASP.NET Core shared framework.
# In .NET 8+, the default port inside the container is 8080 (not 80).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/out .

# ASP.NET Core listens on 8080 by default in .NET 8+.
# The Helm chart's Service maps NodePort 30080 → container 8080.
# AWS DIFFERENCE: behind an ALB/NLB the container port stays 8080;
# the load balancer handles external port mapping.
EXPOSE 8080

# In Kubernetes, inject the in-cluster connection string via:
#   env: ConnectionStrings__InterviewDemo
#   value: Server=sql-server.sql-demo.svc.cluster.local,1433;...
# (double-underscore = nested .NET config key separator)
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "SqlDemosApi.dll"]
