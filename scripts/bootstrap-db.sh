# Creates the cluster-level database roles and the application database for
# local development and deployments. Idempotent: safe to re-run.
#
# The roles are created here (as a superuser), never by the migrations, because
# migrations run as the least-privilege `vepaydo_owner`, which cannot create
# roles. This ports the NestJS `scripts/bootstrap-db.mjs` to the .NET project.
#
# Usage:
#   ./scripts/bootstrap-db.sh            (bash / CI / Linux containers)
#   powershell -File scripts/bootstrap-db.ps1   (Windows)
#
# Required environment (see .env.example):
#   POSTGRES_SUPERUSER_URL   superuser connection; used only here
#   OWNER_DB_PASSWORD        password assigned to vepaydo_owner
#   APP_DB_PASSWORD          password assigned to vepaydo_app
#   MAINTENANCE_DB_PASSWORD  password assigned to vepaydo_maintenance
#   APP_DB_NAME              target database name (default: vepaydo)

#!/usr/bin/env bash
set -euo pipefail

: "${POSTGRES_SUPERUSER_URL:?POSTGRES_SUPERUSER_URL is required}"
OWNER_DB_PASSWORD="${OWNER_DB_PASSWORD:?OWNER_DB_PASSWORD is required}"
APP_DB_PASSWORD="${APP_DB_PASSWORD:?APP_DB_PASSWORD is required}"
MAINTENANCE_DB_PASSWORD="${MAINTENANCE_DB_PASSWORD:?MAINTENANCE_DB_PASSWORD is required}"
APP_DB_NAME="${APP_DB_NAME:-vepaydo}"

# Admin connection points at the maintenance database, not the app database
# (which may not exist yet).
ADMIN_URL="${POSTGRES_SUPERUSER_URL%/*}/postgres"

psql "$ADMIN_URL" -v ON_ERROR_STOP=1 <<SQL
DO \$\$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'vepaydo_owner') THEN
    CREATE ROLE vepaydo_owner LOGIN PASSWORD '${OWNER_DB_PASSWORD}';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'vepaydo_app') THEN
    CREATE ROLE vepaydo_app LOGIN PASSWORD '${APP_DB_PASSWORD}'
      NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'vepaydo_maintenance') THEN
    CREATE ROLE vepaydo_maintenance LOGIN PASSWORD '${MAINTENANCE_DB_PASSWORD}' BYPASSRLS;
  END IF;
END
\$\$;

ALTER ROLE vepaydo_owner       LOGIN PASSWORD '${OWNER_DB_PASSWORD}';
ALTER ROLE vepaydo_app         LOGIN PASSWORD '${APP_DB_PASSWORD}'
  NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
ALTER ROLE vepaydo_maintenance LOGIN PASSWORD '${MAINTENANCE_DB_PASSWORD}' BYPASSRLS;
SQL

# CREATE DATABASE cannot run inside a transaction block, so it is conditional.
if [ "$(psql "$ADMIN_URL" -tAc "SELECT 1 FROM pg_database WHERE datname = '${APP_DB_NAME}'")" != "1" ]; then
  psql "$ADMIN_URL" -v ON_ERROR_STOP=1 -c "CREATE DATABASE ${APP_DB_NAME} OWNER vepaydo_owner;"
fi

psql "$ADMIN_URL" -v ON_ERROR_STOP=1 -c "ALTER DATABASE ${APP_DB_NAME} OWNER TO vepaydo_owner;"

echo "Bootstrap complete."
echo "  DATABASE_URL=$(printf '%s' "${ADMIN_URL%/*}/vepaydo_app:${APP_DB_PASSWORD}@${ADMIN_URL#*@}" | sed 's|://[^@]*@|://|')"
echo "  owner owns ${APP_DB_NAME}; apply migrations with the owner connection (DIRECT_URL)."
