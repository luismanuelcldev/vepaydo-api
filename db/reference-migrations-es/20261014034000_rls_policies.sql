-- Row Level Security. Fail closed: with no tenant context, app.current_tenant_id()
-- returns NULL and every policy predicate is false.

-- Simple tenant isolation on business tables.
DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['tenant_config', 'dispositivos_push', 'cuentas_negocio', 'clientes',
                           'clientes_alias', 'cobros', 'comprobantes', 'extracciones', 'aplicaciones'] LOOP
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
  USING (id IN (SELECT tenant_id FROM membresias WHERE usuario_id = app.current_user_id() AND activo));

-- membresias: the current tenant, plus the user's own memberships (login flow).
ALTER TABLE membresias ENABLE ROW LEVEL SECURITY;
ALTER TABLE membresias FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON membresias
  USING (tenant_id = app.current_tenant_id()) WITH CHECK (tenant_id = app.current_tenant_id());
CREATE POLICY memberships_own ON membresias FOR SELECT
  USING (usuario_id = app.current_user_id());

-- alertas: critical alerts can only be modified by the owner or the system
-- (re-evaluation after a correction). §4.5: the UPDATE policy allows 'system'.
ALTER TABLE alertas ENABLE ROW LEVEL SECURITY;
ALTER TABLE alertas FORCE ROW LEVEL SECURITY;
CREATE POLICY alerts_select ON alertas FOR SELECT
  USING (tenant_id = app.current_tenant_id());
CREATE POLICY alerts_insert ON alertas FOR INSERT
  WITH CHECK (tenant_id = app.current_tenant_id());
CREATE POLICY alerts_update ON alertas FOR UPDATE
  USING      (tenant_id = app.current_tenant_id()
              AND (severidad <> 'CRITICAL' OR app.current_member_role() IN ('owner', 'system')))
  WITH CHECK (tenant_id = app.current_tenant_id()
              AND (severidad <> 'CRITICAL' OR app.current_member_role() IN ('owner', 'system')));
-- No DELETE policy: alerts are never deleted.

-- eventos_auditoria: anyone may insert; only the owner may read; no UPDATE/DELETE.
ALTER TABLE eventos_auditoria ENABLE ROW LEVEL SECURITY;
ALTER TABLE eventos_auditoria FORCE ROW LEVEL SECURITY;
CREATE POLICY audit_insert ON eventos_auditoria FOR INSERT
  WITH CHECK (tenant_id = app.current_tenant_id());
CREATE POLICY audit_select ON eventos_auditoria FOR SELECT
  USING (tenant_id = app.current_tenant_id() AND app.current_member_role() = 'owner');
