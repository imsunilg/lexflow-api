# Compatibility

This repo's version lives in `VERSION` at the repo root. None of the three
repos (`lexflow-database`, `lexflow-api`, `lexflow-web`) has ever been
tagged or released — the matrix below starts from a declared baseline
(`0.1.0` in all three), not a retroactive reconstruction of prior history.

## What "API version" means here

Semver in `VERSION`, bumped by whoever changes the API surface, using this
rule:

- **MAJOR** — a breaking change to the contract surface: an endpoint
  removed/renamed, a required request field added to an existing endpoint,
  a response field removed/renamed/retyped, the `ApiResponse<T>`/
  `ApiErrorResponse` envelope shape changed, or an auth flow changed
  (e.g. the `lexflow_refresh`/`lexflow_2fa_pending` cookie contract in
  `AuthController`). The OpenAPI document this repo generates
  (`client/swagger.json`, Swashbuckle-produced) is the actual source of
  truth for what counts as breaking — if a diff of that file for a given
  change removes or narrows anything a consumer could have depended on,
  that's a MAJOR change.
- **MINOR** — additive: new endpoints, new optional request fields, new
  response fields consumers can ignore.
- **PATCH** — internal fixes with no observable contract change.

## Pairing rule with lexflow-database

`LexFlow.Infrastructure`'s `LexFlowDbContext` is Database-First — this
repo owns no schema, `lexflow-database` does (see that repo's
`COMPATIBILITY.md` for the DB-side half of this rule). Concretely: this
API version requires `lexflow-database >=` whatever DB version last
touched every table its Fluent API configurations reference; a DB MAJOR
bump (destructive/shape-breaking) requires this repo's Fluent
configurations — and therefore this repo's own VERSION — to bump in the
same deploy window, or queries against the changed table(s) fail at
runtime with no earlier warning (Database-First has no build-time check
against a live schema).

## Pairing rule with lexflow-web

`lexflow-web`'s TypeScript client (`client/` in this repo generates it —
see `client/README.md`) is generated **from this repo's own
`client/swagger.json`**, at a point in time. That means:

- A web build is only as current as the API version its client was last
  generated against — regenerating is a manual step (`client/README.md`),
  not something that happens automatically on every API change.
- An API **MAJOR** bump requires lexflow-web to regenerate its client and
  bump its own MAJOR version in the same window — an un-regenerated web
  client will silently keep calling the old contract shape (TypeScript
  won't catch a server-side rename it never saw) until someone notices at
  runtime.
- An API **MINOR** bump is safe to ignore in lexflow-web until convenient —
  new optional fields/endpoints don't break an already-generated client.

## Current baseline

| lexflow-database | lexflow-api | lexflow-web | Notes |
|---|---|---|---|
| 0.1.0 | 0.1.0 | 0.1.0 | Declared baseline — see note above; not a tagged release. |

Append a row here whenever any repo's MAJOR or MINOR version changes in a
way that affects what the other two repos require.
