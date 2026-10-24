# Creates the cluster-level database roles and the application database for
# local development and deployments. Idempotent: safe to re-run. Windows
# equivalent of scripts/bootstrap-db.sh; see docs/runbooks/database-bootstrap.md.
#
# Usage:
#   $env:POSTGRES_SUPERUSER_URL="postgresql://vepaydo:vepaydo@localhost:5433/postgres"
#   $env:OWNER_DB_PASSWORD="vepaydo"
#   $env:APP_DB_PASSWORD="vepaydo"
#   $env:MAINTENANCE_DB_PASSWORD="vepaydo"
#   $env:APP_DB_NAME="vepaydo_dev"
#   ./scripts/bootstrap-db.ps1
#
# Requires psql on PATH (or a running container; see the runbook for the
# docker exec variant).

[CmdletBinding()]
param(
    [string]$SuperuserUrl = $env:POSTGRES_SUPERUSER_URL,
    [string]$OwnerPassword = $env:OWNER_DB_PASSWORD,
    [string]$AppPassword = $env:APP_DB_PASSWORD,
    [string]$MaintenancePassword = $env:MAINTENANCE_DB_PASSWORD,
    [string]$DatabaseName = $(if ($env:APP_DB_NAME) { $env:APP_DB_NAME } else { 'vepaydo' })
)

$ErrorActionPreference = 'Stop'

if (-not $SuperuserUrl) { throw 'POSTGRES_SUPERUSER_URL is required.' }
if (-not $OwnerPassword) { throw 'OWNER_DB_PASSWORD is required.' }
if (-not $AppPassword) { throw 'APP_DB_PASSWORD is required.' }
if (-not $MaintenancePassword) { throw 'MAINTENANCE_DB_PASSWORD is required.' }

# Point the admin connection at the maintenance database, not the app database.
$adminUrl = ($SuperuserUrl -replace '/[^/]+$', '/postgres')

$rolesSql = @"
DO `$`$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'vepaydo_owner') THEN
    CREATE ROLE vepaydo_owner LOGIN PASSWORD '$OwnerPassword';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'vepaydo_app') THEN
    CREATE ROLE vepaydo_app LOGIN PASSWORD '$AppPassword'
      NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'vepaydo_maintenance') THEN
    CREATE ROLE vepaydo_maintenance LOGIN PASSWORD '$MaintenancePassword' BYPASSRLS;
  END IF;
END
`$`$;

ALTER ROLE vepaydo_owner       LOGIN PASSWORD '$OwnerPassword';
ALTER ROLE vepaydo_app         LOGIN PASSWORD '$AppPassword'
  NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
ALTER ROLE vepaydo_maintenance LOGIN PASSWORD '$MaintenancePassword' BYPASSRLS;
"@

& psql $adminUrl -v ON_ERROR_STOP=1 -c $rolesSql
if ($LASTEXITCODE -ne 0) { throw 'Creating/refreshing roles failed.' }

$exists = (& psql $adminUrl -tAc "SELECT 1 FROM pg_database WHERE datname = '$DatabaseName'").Trim()
if ($exists -ne '1') {
    & psql $adminUrl -v ON_ERROR_STOP=1 -c "CREATE DATABASE $DatabaseName OWNER vepaydo_owner;"
    if ($LASTEXITCODE -ne 0) { throw 'Creating the database failed.' }
}

& psql $adminUrl -v ON_ERROR_STOP=1 -c "ALTER DATABASE $DatabaseName OWNER TO vepaydo_owner;"
if ($LASTEXITCODE -ne 0) { throw 'Re-asserting database ownership failed.' }

Write-Host "Bootstrap complete. Apply migrations with the owner connection (DIRECT_URL)."
