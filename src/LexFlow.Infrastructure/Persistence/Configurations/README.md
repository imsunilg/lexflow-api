# Configurations

`IEntityTypeConfiguration<T>` Fluent API classes, one per entity, added per module in
Phase C — column/schema names must match the tables created by `lexflow-database`
exactly (schema-per-concern: core/crm/legal/dms/fin/comm/kb/ops/audit). Discovered
automatically via `ApplyConfigurationsFromAssembly` in `LexFlowDbContext`.
