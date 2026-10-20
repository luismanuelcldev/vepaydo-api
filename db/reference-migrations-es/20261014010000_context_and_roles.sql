-- Database roles are cluster-level objects created by the bootstrap step as a
-- superuser (see docs/runbooks/database-bootstrap.md and scripts/bootstrap-db.mjs),
-- NOT by migrations: migrations run as the least-privilege `vepaydo_owner`, which
-- cannot create roles. Fail loudly if the bootstrap has not run.
--
--   vepaydo_owner       owns the database objects; used only by `prisma migrate deploy`.
--   vepaydo_app         API and worker runtime role; least privilege, no BYPASSRLS.
--   vepaydo_maintenance retention/purge role only; BYPASSRLS with narrow grants.
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'vepaydo_owner')
     OR NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'vepaydo_app')
     OR NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'vepaydo_maintenance') THEN
    RAISE EXCEPTION
      'Missing database roles (vepaydo_owner/vepaydo_app/vepaydo_maintenance). Run `pnpm --filter @vepaydo/api db:bootstrap` as a superuser before migrating.';
  END IF;
END $$;

-- Extensions required by the schema (trigram search, case-insensitive email).
-- pg_trgm and citext are trusted extensions, installable by the database owner.
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE EXTENSION IF NOT EXISTS citext;

-- Application-owned helper schema (context functions, domain functions).
CREATE SCHEMA IF NOT EXISTS app;

-- ---------------------------------------------------------------------
-- Context helpers: read the transaction-local settings the backend sets
-- with set_config(..., true) at the start of every tenant transaction.
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION app.current_tenant_id() RETURNS uuid
LANGUAGE sql STABLE AS $$
  SELECT NULLIF(current_setting('app.tenant_id', true), '')::uuid
$$;

CREATE OR REPLACE FUNCTION app.current_user_id() RETURNS uuid
LANGUAGE sql STABLE AS $$
  SELECT NULLIF(current_setting('app.user_id', true), '')::uuid
$$;

CREATE OR REPLACE FUNCTION app.current_member_role() RETURNS text
LANGUAGE sql STABLE AS $$
  SELECT NULLIF(current_setting('app.role', true), '')
$$;

-- Hamming distance between two 64-bit perceptual hashes.
CREATE OR REPLACE FUNCTION app.hamming_distance(a bigint, b bigint) RETURNS integer
LANGUAGE sql IMMUTABLE PARALLEL SAFE AS $$
  SELECT bit_count((a # b)::bit(64))::integer
$$;

-- Generic updated_at trigger function.
CREATE OR REPLACE FUNCTION app.set_updated_at() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  NEW.actualizado_en := now();
  RETURN NEW;
END $$;
