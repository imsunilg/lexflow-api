{{- define "lexflow-api.fullname" -}}
lexflow-api-{{ .Values.slot }}
{{- end -}}

{{- define "lexflow-workers.fullname" -}}
lexflow-workers-{{ .Values.slot }}
{{- end -}}

{{- define "lexflow-api.labels" -}}
app.kubernetes.io/part-of: lexflow
app.kubernetes.io/slot: {{ .Values.slot }}
{{- end -}}

{{- define "lexflow-api.envFrom" -}}
envFrom:
  - secretRef:
      name: lexflow-api-secrets
{{- end -}}
