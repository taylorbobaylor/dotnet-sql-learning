{{/*
Expand the name of the chart.
*/}}
{{- define "sql-server.name" -}}
{{- .Chart.Name | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels applied to every resource.
*/}}
{{- define "sql-server.labels" -}}
app.kubernetes.io/name: {{ include "sql-server.name" . }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version }}
{{- end }}

{{/*
Selector labels — used in Deployment/Service matchLabels.
*/}}
{{- define "sql-server.selectorLabels" -}}
app.kubernetes.io/name: {{ include "sql-server.name" . }}
{{- end }}
