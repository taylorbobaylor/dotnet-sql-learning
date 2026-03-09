# ============================================================
# Multi-stage build for sql-dashboard (Angular SPA)
# Build context: sql-dashboard/
#
#   docker build -t sql-dashboard sql-dashboard/
# ============================================================

# ----- build stage -------------------------------------------
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

# ----- runtime stage -----------------------------------------
# nginx serves the static files and proxies /health + /scenarios
# to the in-cluster API service (configured via ConfigMap in Helm).
FROM nginx:alpine AS runtime

# Angular 17+ outputs to dist/<name>/browser
COPY --from=build /app/dist/sql-dashboard/browser /usr/share/nginx/html

# Default nginx config is replaced by the Helm ConfigMap at runtime.
# Remove it so the mounted ConfigMap is the only conf in conf.d/.
RUN rm /etc/nginx/conf.d/default.conf

EXPOSE 80
