# Full local-dev stack

Boots all three repos together, in dependency order, with health checks
gating each step: **Postgres → DB Runner (lexflow-database) → API + Workers
(this repo) → staff-portal + client-portal dev servers (lexflow-web)**.

## Prerequisites

Check out all three repos as siblings:

```
<parent>/
  lexflow-database/
  lexflow-api/        <- run docker compose from here
  lexflow-web/
```

Docker Compose v2.20+ (for `condition: service_completed_successfully`,
used to gate the API on the DB Runner actually finishing, not just
starting).

## Bring everything up

```
cd lexflow-api
docker compose -f docker-compose.full.yml up --build
```

This is additive to the existing `docker-compose.yml` (infra + API +
Workers only, no cross-repo dependency) — that file is untouched and still
works standalone if you don't need the DB Runner or the web dev servers.

## What happens, in order

1. `postgres`, `redis`, `elasticsearch`, `azurite` start; `api`/`workers`/
   `db-runner` all wait on the ones they need via `condition:
   service_healthy` (or `service_started` for azurite, which has no
   healthcheck defined).
2. `db-runner` applies every script under `lexflow-database/Scripts/` via
   the DbUp-based Runner (`dotnet run --project
   Runner/LexFlow.Database.Runner -- --scripts-path Scripts`), then exits.
   Idempotent — DbUp journals applied scripts in
   `public.dbup_schema_versions`, so re-running `up` after the volumes
   already exist just no-ops here.
3. `api` and `workers` start only once `db-runner` exits 0
   (`service_completed_successfully`). `api` additionally has to report
   healthy on `GET /health` (curl is installed in the runtime image
   specifically for this — see the Dockerfile) before anything downstream
   proceeds.
4. `staff-portal` (port 4200) and `client-portal` (port 4300) start only
   once `api` is healthy. Both run `npm run start:staff-portal:full` /
   `start:client-portal:full` — the `:full` variants proxy to
   `http://localhost:8080` (`proxy.full.conf.json`) instead of the plain
   `start:staff-portal` / `start:client-portal` scripts, which proxy to
   `http://localhost:5000` (`proxy.conf.json`) for the simpler "just run
   `dotnet run --project src/LexFlow.Api` on the host" workflow described
   in this repo's own README. Don't mix the two: pick whichever npm script
   matches how the API is actually running. Both run inside a plain
   `node:22` container with `lexflow-web` bind-mounted — there's no
   separate Dockerfile to keep in sync with that repo's actual scripts.
   First boot runs `npm ci` inside the container, which is slow (no
   node_modules cache across runs in this simple setup); subsequent
   `docker compose down` / `up` cycles reinstall every time by design —
   for faster iteration, just run `npm run start:staff-portal:full` /
   `start:client-portal:full` directly on the host once `api` is up.

## Ports

| Service        | Port  |
|----------------|-------|
| api            | 8080  |
| postgres       | 5432  |
| redis          | 6379  |
| elasticsearch  | 9200  |
| azurite (blob) | 10000 |
| staff-portal   | 4200  |
| client-portal  | 4300  |

(The Playwright full-stack E2E suite in lexflow-web uses different ports —
4210/4310 — deliberately, so a local dev session and an E2E run don't
collide if both happen to be running at once. See
`lexflow-web/e2e/playwright.fullstack.config.ts`.)

## Tearing down

```
docker compose -f docker-compose.full.yml down -v
```

`-v` also drops the named volumes (`postgres-data`, `redis-data`,
`es-data`, `azurite-data`) — omit it to keep data between runs.

## Versioning across the three repos

See `COMPATIBILITY.md` at the root of each repo for which versions of
lexflow-database / lexflow-api / lexflow-web are meant to run together.
