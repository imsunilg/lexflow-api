# Enums

Domain enums (status/kind fields). Persisted as Postgres `text` + CHECK constraint
per PRD §14 ("Enumerations as PostgreSQL text + CHECK constraints, not native enums,
for migration ease") — these C# enums are the application-side mirror only.
