# lexflow-api

.NET 9 Clean Architecture solution for the LexFlow Enterprise Lawyer CRM API.
Companion to `lexflow-database` (schema owner) and `lexflow-web` (Angular clients).
See `LexFlow_PRD.md` and `LexFlow_Build_Playbook.md` (§1.2) for the full spec.

```
lexflow-api/
├── src/
│   ├── LexFlow.Domain/              entities, value objects, domain events, enums — no external dependencies
│   ├── LexFlow.Application/         MediatR CQRS, FluentValidation, Mapster, repository/service interfaces
│   ├── LexFlow.Infrastructure/      EF Core (Database-First) + Npgsql, Redis, Elasticsearch, Blob, Key Vault
│   ├── LexFlow.Api/                 ASP.NET Core Web API: controllers, middleware, SignalR hubs, Program.cs
│   └── LexFlow.Workers/             Hangfire background job host (own Dockerfile)
├── tests/
│   ├── LexFlow.UnitTests/           xUnit + FluentAssertions
│   ├── LexFlow.IntegrationTests/    xUnit + FluentAssertions + Testcontainers.PostgreSql
│   └── LexFlow.E2ETests/            xUnit + FluentAssertions + WebApplicationFactory<Program>
├── client/                          TypeScript client generation for lexflow-web (see client/README.md)
├── deploy/helm/lexflow-api/         Helm chart for AKS blue-green deploys (§38)
├── Dockerfile                       LexFlow.Api image
├── docker-compose.yml               api + workers + postgres + redis + elasticsearch + azurite (local dev)
└── LexFlow.sln
```

## Schema ownership

**Important:** schema is owned exclusively by `lexflow-database` (raw SQL, applied via
its DbUp runner). `LexFlow.Infrastructure`'s `LexFlowDbContext` is Database-First —
Fluent API entity configurations only. **Never run `dotnet ef migrations add` against
this solution.**

## Local development

```bash
docker compose up -d postgres redis elasticsearch azurite
dotnet build LexFlow.sln
dotnet run --project src/LexFlow.Api
```

Or run the full stack (api + workers + every backing service) in containers:

```bash
docker compose up -d --build
```

Swagger UI: `http://localhost:5000/swagger` (or `:8080` under docker compose) — every
environment except Production; the raw `/swagger/v1/swagger.json` OpenAPI 3.1 document
is always served, Production included (see `client/README.md` for why).
Health check: `http://localhost:5000/health`.

Connection strings and other local secrets go in each project's
`appsettings.Development.json` (gitignored) — see the checked-in
`appsettings.Development.json.example` in `src/LexFlow.Api/` for the shape.

Blob storage runs against Azurite locally (`BlobStorage:ConnectionString` defaults to
`UseDevelopmentStorage=true`, matching Azure Storage SDK's built-in Azurite shorthand);
docker-compose's `azurite` service uses the well-known public Azurite dev account key
instead (there's no real credential to leak — that key is Microsoft's own documented
default for the emulator).

## TypeScript client (for lexflow-web)

`client/` generates a typed Angular TypeScript client from the API's own OpenAPI
document — see `client/README.md` for the full workflow and the "why a copied file, not
a published npm package" decision.

## Realtime

SignalR hub stubs are mapped and ready for module wiring:
`/hubs/notifications`, `/hubs/chat`, `/hubs/presence`, `/hubs/jobs`.

## CI/CD

`.github/workflows/api-ci.yml` implements §37's full gate sequence:

1. **build** → **unit** → **integration** (`build-test`, Postgres + Redis service containers)
2. **SonarQube quality gate** (`sonarqube`) — self-activates once `SONAR_TOKEN` /
   `SONAR_HOST_URL` repo secrets exist; a no-op until then (no workflow edit needed)
3. **contract** (`contract`) — OpenAPI 3.1 schema validation via Redocly lint against a
   live instance. Pact (portal/mobile) isn't wired up — no Pact Broker provisioned yet.
4. **security** — `gitleaks` (secrets scan), `zap-baseline` (OWASP ZAP baseline against
   the full docker-compose stack), `trivy` (container CVE scan). G-AC3's cross-tenant
   fuzz suite and G-AC4's permission-matrix generator run as ordinary
   `LexFlow.IntegrationTests`/`LexFlow.UnitTests` inside `build-test`, not as separate jobs.
5. `generate-ts-client` — builds the TypeScript client (see above) and uploads it as a
   workflow artifact.
6. `build-push-images` — on push to `main` only, once every gate above passes: builds +
   pushes `lexflow-api`/`lexflow-workers` images to GHCR, tagged by short SHA.
7. `deploy` — §38 blue-green on AKS behind Azure Front Door, gated by the `production`
   GitHub Environment's required-reviewers rule (manual approval). Pre-deploy DB
   migrations (expand-contract, backward-compatible with the still-live "blue" stack) →
   Helm-upgrade the idle slot (`deploy/helm/lexflow-api`) → in-cluster smoke test → flip
   the live Service's selector to cut traffic over → 30-minute canary watch (health-check
   based; a real Application-Insights error-rate/P95 query is a documented follow-up,
   not wired up here) with auto-rollback on failure.
   The old "blue" slot is deliberately left running rather than torn down —
   `.github/workflows/rollback.yml` (manual, instant) and
   `.github/workflows/cleanup-blue.yml` (scheduled daily, only scales down a slot once
   it's been idle >24h) are the two companion workflows that act on that window.

None of `AZURE_CREDENTIALS`, `AKS_RESOURCE_GROUP`, `AKS_CLUSTER_NAME`, `AKS_NAMESPACE`,
`LEXFLOW_DATABASE_CONNECTION_STRING`, or the `production` Environment itself exist yet
in repo settings — `deploy`/`rollback`/`cleanup-blue` are real, runnable pipelines the
moment that infrastructure is provisioned and those secrets are added, not aspirational
placeholders requiring further workflow-file changes.
