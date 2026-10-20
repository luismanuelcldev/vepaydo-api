# Database bootstrap (roles and ownership)

Cluster-level roles must exist **before** migrations run, and `vepaydo_owner`
must own the database so every migrated object is owned by it — never by the
superuser. Both are done once by a superuser bootstrap; migrations never create
roles.

This matches the reference schema (`docs/reference/schema/cuadra_schema.sql`,
section 0), which also assigns `LOGIN` and passwords outside the SQL file.

## When to run

- Once per database cluster, before the first `prisma migrate deploy`.
- Again after rotating a role password.

## Command

```bash
pnpm --filter @vepaydo/api db:bootstrap
```

Required environment (see `.env.example`):

| Variable                  | Purpose                                    |
| ------------------------- | ------------------------------------------ |
| `POSTGRES_SUPERUSER_URL`  | Superuser connection; used only here       |
| `OWNER_DB_PASSWORD`       | Password assigned to `vepaydo_owner`       |
| `APP_DB_PASSWORD`         | Password assigned to `vepaydo_app`         |
| `MAINTENANCE_DB_PASSWORD` | Password assigned to `vepaydo_maintenance` |

The script is idempotent: it creates missing roles, refreshes their attributes
and passwords, and re-asserts database ownership. It never creates tables.

## What it does

1. Creates `vepaydo_owner`, `vepaydo_app` (both `NOSUPERUSER NOBYPASSRLS`) and
   `vepaydo_maintenance` (`BYPASSRLS`) if missing.
2. Assigns `LOGIN` and passwords.
3. `ALTER DATABASE <db> OWNER TO vepaydo_owner`.
4. Prints the three connection URLs to put in `.env`.

## Then

```bash
pnpm --filter @vepaydo/api db:migrate   # runs as vepaydo_owner (DIRECT_URL)
pnpm --filter @vepaydo/api db:seed      # runs as vepaydo_owner (needs banks writes)
```

The `context_and_roles` migration fails fast with a clear message if the
bootstrap was not run.

## Verify

```sql
-- as superuser
SELECT tablename, tableowner FROM pg_tables WHERE schemaname = 'public';
-- expect: vepaydo_owner
SELECT datname, pg_get_userbyid(datdba) AS owner FROM pg_database WHERE datname = current_database();
-- expect: vepaydo_owner
```

RLS still applies to the owner because every policy uses
`FORCE ROW LEVEL SECURITY`; the `vepaydo_app` role is the only runtime
connection, and it has no DDL and no write access to `banks`.

## Tests

`apps/api/test/support/database.ts` performs the same bootstrap inside the
Testcontainers PostgreSQL before applying migrations, so the suite exercises the
production ownership model (migrations as owner, requests as `vepaydo_app`).
