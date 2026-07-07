# Interceptors

EF Core `SaveChangesInterceptor` implementations: the audit-capture choke point
(PRD §30 "EF Core SaveChanges interceptor — single choke point") and the
tenant-context (`SET app.tenant_id`) interceptor backing Postgres RLS (PRD §14,
§20(5)). Added in Prompt C-1/C-2 once auth and audit land.
