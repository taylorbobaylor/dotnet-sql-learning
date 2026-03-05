{{- define "sql-demos-api.name" -}}
{{- .Chart.Name | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "sql-demos-api.labels" -}}
app.kubernetes.io/name: {{ include "sql-demos-api.name" . }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version }}
{{- end }}

{{- define "sql-demos-api.selectorLabels" -}}
app.kubernetes.io/name: {{ include "sql-demos-api.name" . }}
{{- end }}
