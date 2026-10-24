# Database bootstrap (roles and ownership)

Cluster-level roles must exist **before** migrations run, and `vepaydo_owner`
must own the database so every migrated object is owned by it — never by the
superuser. Both are done once by a superuser bootstrap; migrations never create
roles.

This matches the reference schema (`docs/reference/schema/cuadra_schema.sql`,
section 0), which also assigns `LOGIN` and passwords outside the SQL file.

## When to run

- Once per database cluster, before the first migration.
- Again after rotating a role password.

## Command

Windows (PowerShell):

```powershell
$env:POSTGRES_SUPERUSER_URL="postgresql://vepaydo:vepaydo@localhost:5433/postgres"
$env:OWNER_DB_PASSWORD="vepaydo"; $env:APP_DB_PASSWORD="vepaydo"
$env:MAINTENANCE_DB_PASSWORD="vepaydo"; $env:APP_DB_NAME="vepaydo_dev"
./scripts/bootstrap-db.ps1
```

Linux/CI (bash):

```bash
POSTGRES_SUPERUSER_URL=... OWNER_DB_PASSWORD=... APP_DB_PASSWORD=... \
  MAINTENANCE_DB_PASSWORD=... APP_DB_NAME=vepaydo ./scripts/bootstrap-db.sh
```

Required environment (see `.env.example`):

| Variable                  | Purpose                                    |
| ------------------------- | ------------------------------------------ |
| `POSTGRES_SUPERUSER_URL`  | Superuser connection; used only here       |
| `OWNER_DB_PASSWORD`       | Password assigned to `vepaydo_owner`       |
| `APP_DB_PASSWORD`         | Password assigned to `vepaydo_app`         |
| `MAINTENANCE_DB_PASSWORD` | Password assigned to `vepaydo_maintenance` |
| `APP_DB_NAME`             | Target database name (default `vepaydo`)   |

The script is idempotent: it creates missing roles, refreshes their attributes
and passwords, and re-asserts database ownership. It never creates tables.

## What it does

1. Creates `vepaydo_owner`, `vepaydo_app` (both `NOSUPERUSER NOBYPASSRLS`) and
   `vepaydo_maintenance` (`BYPASSRLS`) if missing.
2. Assigns `LOGIN` and passwords.
3. Creates the application database owned by `vepaydo_owner` if missing.
4. `ALTER DATABASE <db> OWNER TO vepaydo_owner`.

## Then

Apply the SQL migrations with the owner connection (`DIRECT_URL`). Today this is
the `migrate` one-shot service at deploy time (M7); for local development the
migrations can be applied with `psql` in filename order as the owner connection
(see `Migrations` in `AGENTS.md` section 7). The `banks` catalog is seeded by the
`create_tenant_grants_seed` migration itself.

The `context_and_roles` migration fails fast with a clear message if the
bootstrap was not run.

## Docker dev container

When the database runs in the `vepaydo-pg-tmp` container (port **5433**), the
same steps run through `docker exec`:

```powershell
docker exec vepaydo-pg-tmp psql -U vepaydo -d postgres -c `
  "CREATE DATABASE vepaydo_dev OWNER vepaydo_owner;"
Get-ChildItem db/migrations -Filter *.sql | Sort-Object Name | ForEach-Object {
  Get-Content $_.FullName -Raw | docker exec -i vepaydo-pg-tmp psql -U vepaydo_owner -d vepaydo_dev -v ON_ERROR_STOP=1 -q
}
```

The container's superuser is `vepaydo`/`vepaydo` and its published port is
`5433` (5432 is used by another local PostgreSQL).

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
