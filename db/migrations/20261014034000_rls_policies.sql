-- Row Level Security. Fail closed: with no tenant context, app.current_tenant_id()
-- returns NULL and every policy predicate is false.

-- Simple tenant isolation on business tables.
DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['tenant_settings', 'push_devices', 'business_accounts', 'customers',
                           'customer_aliases', 'charges', 'receipts', 'extractions', 'allocations'] LOOP
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', t);
    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', t);
    EXECUTE format(
      'CREATE POLICY tenant_isolation ON %I
         USING (tenant_id = app.current_tenant_id())
         WITH CHECK (tenant_id = app.current_tenant_id())', t);
  END LOOP;
END $$;

-- tenants: the current tenant, plus every tenant the authenticated user belongs to
-- (needed by the tenant selector).
ALTER TABLE tenants ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenants FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_self ON tenants
  USING (id = app.current_tenant_id()) WITH CHECK (id = app.current_tenant_id());
CREATE POLICY tenants_of_user ON tenants FOR SELECT
  USING (id IN (SELECT tenant_id FROM memberships WHERE user_id = app.current_user_id() AND is_active));

-- memberships: the current tenant, plus the user's own memberships (login flow).
ALTER TABLE memberships ENABLE ROW LEVEL SECURITY;
ALTER TABLE memberships FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON memberships
  USING (tenant_id = app.current_tenant_id()) WITH CHECK (tenant_id = app.current_tenant_id());
CREATE POLICY memberships_own ON memberships FOR SELECT
  USING (user_id = app.current_user_id());

-- alerts: critical alerts can only be modified by the owner or the system
-- (re-evaluation after a correction). §4.5: the UPDATE policy allows 'system'.
ALTER TABLE alerts ENABLE ROW LEVEL SECURITY;
ALTER TABLE alerts FORCE ROW LEVEL SECURITY;
CREATE POLICY alerts_select ON alerts FOR SELECT
  USING (tenant_id = app.current_tenant_id());
CREATE POLICY alerts_insert ON alerts FOR INSERT
  WITH CHECK (tenant_id = app.current_tenant_id());
CREATE POLICY alerts_update ON alerts FOR UPDATE
  USING      (tenant_id = app.current_tenant_id()
              AND (severity <> 'CRITICAL' OR app.current_member_role() IN ('owner', 'system')))
  WITH CHECK (tenant_id = app.current_tenant_id()
              AND (severity <> 'CRITICAL' OR app.current_member_role() IN ('owner', 'system')));
-- No DELETE policy: alerts are never deleted.

-- audit_events: anyone may insert; only the owner may read; no UPDATE/DELETE.
ALTER TABLE audit_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit_events FORCE ROW LEVEL SECURITY;
CREATE POLICY audit_insert ON audit_events FOR INSERT
  WITH CHECK (tenant_id = app.current_tenant_id());
CREATE POLICY audit_select ON audit_events FOR SELECT
  USING (tenant_id = app.current_tenant_id() AND app.current_member_role() = 'owner');
