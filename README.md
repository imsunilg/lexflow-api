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
│   └── LexFlow.Workers/             Hangfire background job host
├── tests/
│   ├── LexFlow.UnitTests/           xUnit + FluentAssertions
│   ├── LexFlow.IntegrationTests/    xUnit + FluentAssertions + Testcontainers.PostgreSql
│   └── LexFlow.E2ETests/            xUnit + FluentAssertions + WebApplicationFactory<Program>
├── Dockerfile
├── docker-compose.yml               api + postgres + redis + elasticsearch (local dev)
└── LexFlow.sln
```

## Schema ownership

**Important:** schema is owned exclusively by `lexflow-database` (raw SQL, applied via
its DbUp runner). `LexFlow.Infrastructure`'s `LexFlowDbContext` is Database-First —
Fluent API entity configurations only. **Never run `dotnet ef migrations add` against
this solution.**

## Local development

```bash
docker compose up -d postgres redis elasticsearch
dotnet build LexFlow.sln
dotnet run --project src/LexFlow.Api
```

Swagger UI: `http://localhost:5000/swagger` (Development environment).
Health check: `http://localhost:5000/health`.

Connection strings and other local secrets go in each project's
`appsettings.Development.json` (gitignored) — see the checked-in
`appsettings.Development.json.example` in `src/LexFlow.Api/` for the shape.

## Realtime

SignalR hub stubs are mapped and ready for module wiring:
`/hubs/notifications`, `/hubs/chat`, `/hubs/presence`, `/hubs/jobs`.

## CI

`.github/workflows/api-ci.yml`: build → unit tests → integration tests (Postgres +
Redis service containers) → SonarQube gate (stub, disabled until `SONAR_TOKEN` /
`SONAR_HOST_URL` secrets exist) → Trivy container scan.
