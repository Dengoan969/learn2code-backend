{{- define "learn2code-api.name" -}}
{{- .Chart.Name | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "learn2code-api.fullname" -}}
{{- include "learn2code-api.name" . -}}
{{- end -}}

{{- define "learn2code-api.labels" -}}
app.kubernetes.io/name: {{ include "learn2code-api.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version | replace "+" "_" }}
{{- end -}}

{{- define "learn2code-api.selectorLabels" -}}
app.kubernetes.io/name: {{ include "learn2code-api.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}