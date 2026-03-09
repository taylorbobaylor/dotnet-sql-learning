{{- define "sql-dashboard.name" -}}
{{- .Chart.Name | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "sql-dashboard.labels" -}}
app.kubernetes.io/name: {{ include "sql-dashboard.name" . }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version }}
{{- end }}

{{- define "sql-dashboard.selectorLabels" -}}
app.kubernetes.io/name: {{ include "sql-dashboard.name" . }}
{{- end }}
