# ADR 0005: Reuse the PostgreSQL migrations, translated to English identifiers

## Status
Accepted

## Context
The original Prisma migrations already define the full PostgreSQL schema, including
the security-critical objects (roles assertions, extensions, `app` helper schema,
RLS policies with `FORCE`, integrity triggers, `security_invoker` views,
`app.create_tenant`, and the `banks` seed). They are exercised by an RLS test suite.

Two problems:
1. EF Core does not model RLS, triggers, views, functions, or grants, so generating
   migrations from the EF model would drop or miss them.
2. The original migrations use **Spanish identifiers** for several tables and columns
   (`sesiones`, `membresias`, `cuentas_negocio`, `cobros`, `comprobantes`,
   `extracciones`, `aplicaciones`, `alertas`, `eventos_auditoria`, `tenant_config`,
   `dispositivos_push`, and columns such as `nombre`, `creado_en`, `actualizado_en`,
   `monto`). This contradicts the naming rule (English DB objects) documented in
   `docs/schema-mapping.md`.

## Decision
- **Reuse** the migration content, but **translate all database objects to English**
  per `docs/schema-mapping.md` (tables, columns, enum types, policies, triggers,
  views, functions, indexes, constraints).
- The translated migrations live in `db/migrations/` (filename order) and are applied
  by a small idempotent runner that records a migration history table. Schema
  generation is never delegated to `dotnet ef migrations`.
- The original Spanish migrations are preserved in `db/reference-migrations-es/`
  as the translation source.
- EF Core maps its model to the English names via `ToTable`/`HasColumnName`.

## Consequences
- The database matches the documented English naming convention.
- The security-critical SQL is preserved and re-verified by the .NET RLS test suite
  (Testcontainers PostgreSQL).
- All C# queries, repositories, and the EF model use English identifiers end to end.
- The translation must be done carefully to keep trigger/policy/view logic consistent
  with the renamed columns (verified by integration tests).
