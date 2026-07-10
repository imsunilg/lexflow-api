# TypeScript client generation

Generates a typed TypeScript client for `lexflow-web` from the API's own OpenAPI 3.1 document
(`/swagger/v1/swagger.json`), using [NSwag](https://github.com/RicoSuter/NSwag)'s Angular
template. One `XxxClient` Angular-injectable class is generated per PRD §16 "Area" (the same
grouping the Swagger doc's tags use — see `SwaggerAreaTags.cs`), e.g. `LeadsClient`,
`BillingClient`, `PortalClient` — 19 classes total, each with its own `IXxxClient` interface for
DI/mocking, backed by `HttpClient` and returning `Observable<T>` (idiomatic Angular, not a raw
`fetch` wrapper — auth headers, error interceptors, etc. go through the same `HttpClient`
interceptor chain the rest of `lexflow-web` already uses).

## Usage

```bash
npm install                              # once
dotnet run --project ../src/LexFlow.Api  # in another terminal — the client is generated FROM a running instance
npm run build                            # fetch-spec (curl :5000/swagger/v1/swagger.json) + generate
```

Output: `generated/lexflow-api-client.ts` (~1MB, git-ignored — regenerate, don't hand-edit).
Point `LEXFLOW_API_URL` at a different host/port if the API isn't on the default
`http://localhost:5000` (e.g. a docker-compose instance on `:8080`):

```bash
LEXFLOW_API_URL=http://localhost:8080 npm run build
```

## Consuming from lexflow-web

**Decision: a copyable `generated/` file, not a published npm package.** There is no private
npm registry (GitHub Packages / Verdaccio / Azure Artifacts) configured for this org yet, so
publishing a versioned `@lexflow/api-client` would need that infrastructure stood up first for
no real benefit today — this is a two-repo internal pairing, not a client SDK for external
consumers. Once a private registry exists, switching is a small, additive change: add a
`publish` script here (`npm publish --registry <url>`) and a `package.json` with a real
`name`/`version`/`main`, no changes to `nswag.json` or the generation itself.

Until then, `lexflow-web` pulls the generated file in one of two ways:

1. **Local dev** — copy `generated/lexflow-api-client.ts` into
   `lexflow-web/projects/shared/src/lib/api/` directly (both repos are expected to be checked
   out as sibling directories per the other repos' READMEs).
2. **CI** — the `generate-ts-client` job in `.github/workflows/api-ci.yml` uploads
   `lexflow-api-client.ts` as a build artifact on every push to `main`; a `lexflow-web` workflow
   (or a developer) downloads the latest artifact via `gh run download` and commits it, so
   `lexflow-web`'s own CI never needs a live `lexflow-api` instance to build.

## Why NSwag over openapi-typescript

`openapi-typescript` generates types only (no runtime client) and pairs naturally with
`openapi-fetch`, which is a better fit for a `fetch`-based or framework-agnostic consumer.
`lexflow-web` is Angular end-to-end, so NSwag's Angular template — real `@Injectable()` classes
using `HttpClient`, `Observable<T>` returns, an `API_BASE_URL` `InjectionToken` for
environment-based configuration — is less code to hand-write on the consuming side and matches
the rest of the app's DI-based HTTP patterns.
