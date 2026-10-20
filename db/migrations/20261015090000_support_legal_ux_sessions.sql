-- Phase 1 gap closure (AGENTS.md section 4.5, items 5-10): UX events, password
-- recovery tokens, legal acceptances, platform support grants and helper
-- functions, receipt failure/reject reasons, session device metadata, and the
-- audit-actor enum. English identifiers throughout (section 3.2).

-- ---------------------------------------------------------------------
-- New enums (values UPPER_SNAKE_CASE, section 4.4).
-- ---------------------------------------------------------------------
CREATE TYPE "failure_reason" AS ENUM ('NOT_A_RECEIPT', 'UNREADABLE', 'OCR_ERROR', 'TIMEOUT');
CREATE TYPE "reject_reason_code" AS ENUM ('WRONG_AMOUNT', 'DUPLICATE', 'NOT_THIS_BUSINESS', 'NOT_A_RECEIPT', 'UNREADABLE', 'OTHER');
CREATE TYPE "legal_document_type" AS ENUM ('TERMS', 'PRIVACY');
CREATE TYPE "audit_actor" AS ENUM ('USER', 'SYSTEM', 'SUPPORT');

-- ---------------------------------------------------------------------
-- receipts.failure_reason / receipts.reject_reason_code (section 4.5 item 9).
-- ---------------------------------------------------------------------
ALTER TABLE "receipts"
  ADD COLUMN "failure_reason" "failure_reason",
  ADD COLUMN "reject_reason_code" "reject_reason_code";

ALTER TABLE "receipts"
  ADD CONSTRAINT "receipts_failure_reason_only_when_failed"
    CHECK ("failure_reason" IS NULL OR "processing_status" = 'FAILED'),
  ADD CONSTRAINT "receipts_reject_reason_required"
    CHECK ("reconciliation_status" <> 'REJECTED' OR "reject_reason_code" IS NOT NULL);

-- ---------------------------------------------------------------------
-- audit_events.actor: text -> audit_actor enum (section 4.5 item 10).
-- Data-preserving conversion; the previous default was the Spanish 'usuario'.
-- ---------------------------------------------------------------------
ALTER TABLE "audit_events" ALTER COLUMN "actor" DROP DEFAULT;
ALTER TABLE "audit_events"
  ALTER COLUMN "actor" TYPE "audit_actor"
  USING (
    CASE UPPER("actor")
      WHEN 'USUARIO' THEN 'USER'
      WHEN 'SYSTEM'  THEN 'SYSTEM'
      WHEN 'SUPPORT' THEN 'SUPPORT'
      WHEN 'USER'    THEN 'USER'
      ELSE 'USER'
    END
  )::"audit_actor";
ALTER TABLE "audit_events" ALTER COLUMN "actor" SET DEFAULT 'USER';

-- ---------------------------------------------------------------------
-- sessions: device metadata for the sessions screen and revocation (9.9).
-- ---------------------------------------------------------------------
ALTER TABLE "sessions"
  ADD COLUMN "device_label" TEXT,
  ADD COLUMN "ip" INET,
  ADD COLUMN "last_seen_at" TIMESTAMPTZ;

-- ---------------------------------------------------------------------
-- users.is_platform_support (section 4.5 item 8). Support users are created
-- only by a seed or an operator script, never through registration.
-- ---------------------------------------------------------------------
ALTER TABLE "users" ADD COLUMN "is_platform_support" BOOLEAN NOT NULL DEFAULT false;

-- ---------------------------------------------------------------------
-- New tables.
-- ---------------------------------------------------------------------

-- Global (no tenant_id, no RLS, never exposed through the API): only the
-- SHA-256 hash of the token is stored, never the token itself.
CREATE TABLE "password_reset_tokens" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "user_id" UUID NOT NULL,
    "token_hash" BYTEA NOT NULL,
    "expires_at" TIMESTAMPTZ NOT NULL,
    "used_at" TIMESTAMPTZ,
    "issued_by_support_id" UUID,
    "requested_ip" INET,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "password_reset_tokens_pkey" PRIMARY KEY ("id")
);

-- Global and immutable (append-only via grants: no UPDATE, no DELETE).
CREATE TABLE "legal_acceptances" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "user_id" UUID NOT NULL,
    "document_type" "legal_document_type" NOT NULL,
    "version" TEXT NOT NULL,
    "accepted_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "ip" INET,
    "user_agent" TEXT,

    CONSTRAINT "legal_acceptances_pkey" PRIMARY KEY ("id")
);

-- First-party UX metrics (section 4.5 item 5). RLS like every tenant table.
CREATE TABLE "ux_events" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "user_id" UUID,
    "name" TEXT NOT NULL,
    "props" JSONB NOT NULL DEFAULT '{}',
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "ux_events_pkey" PRIMARY KEY ("id")
);

-- Time-boxed, read-only platform support access (section 4.5 item 8).
CREATE TABLE "support_grants" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "granted_by" UUID NOT NULL,
    "expires_at" TIMESTAMPTZ NOT NULL,
    "revoked_at" TIMESTAMPTZ,
    "note" TEXT,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "support_grants_pkey" PRIMARY KEY ("id")
);

CREATE UNIQUE INDEX "password_reset_tokens_token_hash_key" ON "password_reset_tokens"("token_hash");

CREATE UNIQUE INDEX "legal_acceptances_user_id_document_type_version_key"
  ON "legal_acceptances"("user_id", "document_type", "version");

ALTER TABLE "password_reset_tokens" ADD CONSTRAINT "password_reset_tokens_user_id_fkey"
  FOREIGN KEY ("user_id") REFERENCES "users"("id") ON DELETE CASCADE ON UPDATE CASCADE;
ALTER TABLE "password_reset_tokens" ADD CONSTRAINT "password_reset_tokens_issued_by_support_id_fkey"
  FOREIGN KEY ("issued_by_support_id") REFERENCES "users"("id") ON DELETE SET NULL ON UPDATE CASCADE;
ALTER TABLE "legal_acceptances" ADD CONSTRAINT "legal_acceptances_user_id_fkey"
  FOREIGN KEY ("user_id") REFERENCES "users"("id") ON DELETE CASCADE ON UPDATE CASCADE;
ALTER TABLE "ux_events" ADD CONSTRAINT "ux_events_tenant_id_fkey"
  FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;
ALTER TABLE "ux_events" ADD CONSTRAINT "ux_events_user_id_fkey"
  FOREIGN KEY ("user_id") REFERENCES "users"("id") ON DELETE SET NULL ON UPDATE CASCADE;
ALTER TABLE "support_grants" ADD CONSTRAINT "support_grants_tenant_id_fkey"
  FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;
ALTER TABLE "support_grants" ADD CONSTRAINT "support_grants_granted_by_fkey"
  FOREIGN KEY ("granted_by") REFERENCES "users"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

ALTER TABLE "support_grants"
  ADD CONSTRAINT "support_grants_expiry_after_creation" CHECK ("expires_at" > "created_at");

-- ---------------------------------------------------------------------
-- RLS for the new tenant-scoped tables.
-- ---------------------------------------------------------------------
ALTER TABLE "ux_events" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "ux_events" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON "ux_events"
  USING (tenant_id = app.current_tenant_id())
  WITH CHECK (tenant_id = app.current_tenant_id());

ALTER TABLE "support_grants" ENABLE ROW LEVEL SECURITY;
ALTER TABLE "support_grants" FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON "support_grants"
  USING (tenant_id = app.current_tenant_id())
  WITH CHECK (tenant_id = app.current_tenant_id());
-- Support sessions may read grant rows (platform metadata, never business data)
-- so they can tell whether they currently hold a grant.
CREATE POLICY support_grants_support_read ON "support_grants" FOR SELECT
  USING (app.current_member_role() = 'support');

-- Support may read tenant metadata (never direct business data): the overview
-- function and the support console rely on this read-only policy.
CREATE POLICY tenants_support_read ON "tenants" FOR SELECT
  USING (app.current_member_role() = 'support');

-- ---------------------------------------------------------------------
-- Platform support access functions (section 4.5 item 8, section 9.9).
-- Support never reads tenant tables directly: business data is reachable only
-- through these SECURITY DEFINER functions, never images or storage keys. Each
-- checks app.role() = 'support' and, except the overview, an active grant.
-- ---------------------------------------------------------------------
CREATE OR REPLACE FUNCTION app.has_active_support_grant(p_tenant uuid) RETURNS boolean
LANGUAGE sql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
  SELECT app.current_member_role() = 'support'
     AND EXISTS (
       SELECT 1 FROM support_grants
        WHERE tenant_id = p_tenant
          AND revoked_at IS NULL
          AND expires_at > now()
     )
$$;

CREATE OR REPLACE FUNCTION app.support_tenant_overview()
RETURNS TABLE (
  id uuid, name text, plan tenant_plan, status text, created_at timestamptz
)
LANGUAGE sql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
  SELECT t.id, t.name, t.plan, t.status, t.created_at
    FROM tenants t
   WHERE app.current_member_role() = 'support'
   ORDER BY t.created_at DESC
$$;

CREATE OR REPLACE FUNCTION app.support_list_audit_events(
  p_tenant uuid, p_limit integer DEFAULT 100, p_cursor bigint DEFAULT NULL
)
RETURNS TABLE (
  id bigint, user_id uuid, actor audit_actor, entity text,
  entity_id uuid, action text, created_at timestamptz
)
LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
#variable_conflict use_column
BEGIN
  IF NOT app.has_active_support_grant(p_tenant) THEN
    RAISE EXCEPTION 'An active support grant is required' USING ERRCODE = 'insufficient_privilege';
  END IF;
  -- audit_select is owner-only; support reads audit through this function.
  PERFORM set_config('app.tenant_id', p_tenant::text, true);
  PERFORM set_config('app.role', 'owner', true);
  RETURN QUERY
    SELECT e.id, e.user_id, e.actor, e.entity, e.entity_id, e.action, e.created_at
      FROM audit_events e
     WHERE e.tenant_id = p_tenant
       AND (p_cursor IS NULL OR e.id < p_cursor)
     ORDER BY e.id DESC
     LIMIT LEAST(GREATEST(p_limit, 1), 200);
  PERFORM set_config('app.role', 'support', true);
  PERFORM set_config('app.tenant_id', '', true);
END $$;

CREATE OR REPLACE FUNCTION app.support_list_receipts(
  p_tenant uuid, p_limit integer DEFAULT 50, p_cursor timestamptz DEFAULT NULL
)
RETURNS TABLE (
  id uuid, processing_status processing_status, reconciliation_status reconciliation_status,
  risk alert_severity, bank_id smallint, amount numeric, currency char(3),
  payment_date date, uploaded_by uuid, created_at timestamptz
)
LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
#variable_conflict use_column
BEGIN
  IF NOT app.has_active_support_grant(p_tenant) THEN
    RAISE EXCEPTION 'An active support grant is required' USING ERRCODE = 'insufficient_privilege';
  END IF;
  PERFORM set_config('app.tenant_id', p_tenant::text, true);
  RETURN QUERY
    SELECT c.id, c.processing_status, c.reconciliation_status, c.risk, c.bank_id,
           c.amount, c.currency, c.payment_date, c.uploaded_by, c.created_at
      FROM receipts c
     WHERE c.tenant_id = p_tenant
       AND (p_cursor IS NULL OR c.created_at < p_cursor)
     ORDER BY c.created_at DESC
     LIMIT LEAST(GREATEST(p_limit, 1), 200);
  PERFORM set_config('app.tenant_id', '', true);
END $$;

CREATE OR REPLACE FUNCTION app.support_list_alerts(
  p_tenant uuid, p_limit integer DEFAULT 100, p_cursor timestamptz DEFAULT NULL
)
RETURNS TABLE (
  id uuid, receipt_id uuid, rule alert_rule_code, severity alert_severity,
  status alert_status, created_at timestamptz
)
LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
#variable_conflict use_column
BEGIN
  IF NOT app.has_active_support_grant(p_tenant) THEN
    RAISE EXCEPTION 'An active support grant is required' USING ERRCODE = 'insufficient_privilege';
  END IF;
  PERFORM set_config('app.tenant_id', p_tenant::text, true);
  RETURN QUERY
    SELECT a.id, a.receipt_id, a.rule, a.severity, a.status, a.created_at
      FROM alerts a
     WHERE a.tenant_id = p_tenant
       AND (p_cursor IS NULL OR a.created_at < p_cursor)
     ORDER BY a.created_at DESC
     LIMIT LEAST(GREATEST(p_limit, 1), 200);
  PERFORM set_config('app.tenant_id', '', true);
END $$;

-- ---------------------------------------------------------------------
-- Grants (section 7).
-- ---------------------------------------------------------------------
GRANT SELECT, INSERT ON legal_acceptances TO vepaydo_app;          -- immutable: no UPDATE, no DELETE
GRANT SELECT, INSERT, UPDATE ON password_reset_tokens TO vepaydo_app;
GRANT SELECT, INSERT ON ux_events TO vepaydo_app;                  -- append-only metrics
GRANT SELECT, INSERT, UPDATE, DELETE ON support_grants TO vepaydo_app;

REVOKE ALL ON FUNCTION app.has_active_support_grant(uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION app.has_active_support_grant(uuid),
      app.support_tenant_overview(),
      app.support_list_audit_events(uuid, integer, bigint),
      app.support_list_receipts(uuid, integer, timestamptz),
      app.support_list_alerts(uuid, integer, timestamptz) TO vepaydo_app;

-- Maintenance: retention purge of expired tokens and old UX metrics.
GRANT SELECT, DELETE ON password_reset_tokens, ux_events TO vepaydo_maintenance;
