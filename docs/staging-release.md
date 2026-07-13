# Staging deploy & release gate

PRD §38 / Build Playbook E-2. `.github/workflows/release.yml` triggers once
`API CI` (api-ci.yml) finishes successfully on `main` and runs, in order:

```
staging-db-migrate  (DB Runner + tools/E2eSeed against the managed Postgres
                      Flexible Server)
        |
        v
staging-deploy-api --------+
        |                  |
        v                  v
d18-staging-e2e <---- staging-deploy-web
        |
        v
c15-staging-smoke
        |
        v
promote-production   (blue-green, needs manual approval via the
                       `production` GitHub Environment)
```

`d18-staging-e2e` and `c15-staging-smoke` are the actual release gate named
in PRD §38 / Build Playbook E-2 — `promote-production` only starts once
both are green (and even then waits on a human reviewer, since
`production` is a protected Environment).

## What D-18 and C-15 mean here

- **D-18** = lexflow-web's 40-critical-journey Playwright suite
  (`e2e/tests-fullstack/`), the same one `web-ci.yml`'s `e2e-fullstack` job
  runs against a local docker-compose stack — reused as-is against staging
  via `PW_SKIP_WEBSERVER=1` + `PW_STAFF_BASE_URL`/`PW_CLIENT_BASE_URL`
  overrides (see `e2e/playwright.fullstack.config.ts`).
- **C-15** = the integration-test suite in
  `tests/LexFlow.IntegrationTests`. Its existing fixtures
  (`CriticalJourneysSmokeTests`, `CrossTenantFuzzTests`,
  `PermissionMatrixIntegrationTests`) all host the API in-process via
  `WebApplicationFactory` + Testcontainers — that only works against code
  running in the CI job itself, never a real remote deployment, so they
  can't literally run against staging. `tests/LexFlow.IntegrationTests/Staging/`
  (`[Trait("Category", "StagingSmoke")]`) is the black-box subset that
  *can*: authenticated API reachability (`StagingApiSmokeTests`) and the
  real G-AC1/G-AC2/AC-CC3 nightly-integrity-job check run for real against
  staging's actual data (`StagingMoneyIntegrityTests`, reusing
  `AuditIntegrityCheckJob` verbatim). The mutating critical journeys
  (lead→convert→matter→hearing→outcome, WIP→invoice→pay) are deliberately
  **not** re-implemented here — D-18 already covers those, end to end,
  against this same staging deployment; duplicating them in a second
  language/tool would just be two things to keep in sync instead of one.
  `build-test` (the pre-merge job in `api-ci.yml`) excludes
  `Category=StagingSmoke` — these tests only make sense with real staging
  secrets, which that job doesn't have.

## Required secrets (this repo)

None of these exist yet — `release.yml` is real and runnable the moment
they're added in repo/environment settings, same as the pre-existing
`deploy` job's secrets.

| Secret | Used for |
|---|---|
| `AZURE_CREDENTIALS`, `AKS_RESOURCE_GROUP`, `AKS_CLUSTER_NAME` | Already exist for production; reused for staging too. |
| `AKS_STAGING_NAMESPACE` | Separate namespace from `AKS_NAMESPACE` (production) — one plain deployment, no blue/green slots. |
| `LEXFLOW_STAGING_DB_CONNECTION` | Managed Postgres Flexible Server connection string for staging. Same secret name `lexflow-database`'s own `db-migrate.yml` already uses. |
| `LEXFLOW_STAGING_API_URL` | Public URL for the staging API (pre-provisioned, e.g. an Application Gateway/Front Door route or LB DNS name). |
| `LEXFLOW_STAGING_STAFF_URL`, `LEXFLOW_STAGING_CLIENT_URL` | The staging Azure Static Web Apps URLs D-18 points Playwright at. |
| `AZURE_STATIC_WEB_APPS_API_TOKEN_STAFF`, `AZURE_STATIC_WEB_APPS_API_TOKEN_CLIENT` | Per-app SWA deployment tokens (also needed in lexflow-web's own repo secrets for its independent `deploy-staging` job in `web-ci.yml`). |
| `LEXFLOW_STAGING_E2E_TENANT_SLUG/_EMAIL/_PASSWORD`, `LEXFLOW_STAGING_E2E_PORTAL_EMAIL/_PASSWORD` | Optional — default to `tools/E2eSeed`'s own defaults if unset. |
| `RAZORPAY_TEST_KEY_ID/_SECRET` | Optional — journey-02 (WIP→invoice→pay) skips gracefully without them, same as `web-ci.yml`. |

## One-time infra setup this workflow does not do

- Azure App Configuration's "staging" label needs `Cors:AllowedOrigins`
  and `Cors:PortalAllowedOrigins` set to the staging Static Web Apps
  hostnames (see `values.yaml`'s comment on `env:` — CORS is config-driven,
  not a Helm value, in this chart).
- The staging namespace, its public URL/routing, and the two Static Web
  Apps resources themselves are assumed to already exist — this workflow
  deploys *to* them, it doesn't provision them.
