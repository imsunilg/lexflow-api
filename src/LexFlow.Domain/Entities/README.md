# Entities

Domain entities/aggregates, one file (or small cluster) per table family, added
per module in Phase C of the Build Playbook (e.g. `User`, `Role`, `Matter`,
`Invoice`). Column-for-column parity with the schema owned by `lexflow-database`
(PRD §18) — no EF Core attributes here, mapping is Fluent API only in
`LexFlow.Infrastructure`.
