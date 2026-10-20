-- Tenant onboarding: the ONLY way to create a tenant. SECURITY DEFINER so that a
-- caller cannot insert into tenants directly (that privilege is revoked below).
CREATE OR REPLACE FUNCTION app.create_tenant(p_name text, p_user uuid) RETURNS uuid
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_id uuid := gen_random_uuid();
BEGIN
  PERFORM set_config('app.tenant_id',  v_id::text,      true);
  PERFORM set_config('app.user_id',    p_user::text,    true);
  PERFORM set_config('app.role',       'owner',         true);

  INSERT INTO tenants (id, name, updated_at) VALUES (v_id, p_name, now());
  INSERT INTO tenant_settings (tenant_id, updated_at) VALUES (v_id, now());
  INSERT INTO memberships (tenant_id, user_id, role) VALUES (v_id, p_user, 'OWNER');
  INSERT INTO audit_events (tenant_id, user_id, entity, entity_id, action, after)
    VALUES (v_id, p_user, 'tenant', v_id, 'create', jsonb_build_object('name', p_name));
  RETURN v_id;
END $$;

-- Banks catalog seed. Idempotent so re-running migrations is safe.
INSERT INTO banks (code, name, parser_wave) VALUES
  ('banreservas',   'Banreservas',                                      1),
  ('popular',       'Banco Popular Dominicano',                         1),
  ('bhd',           'Banco BHD',                                        1),
  ('santa_cruz',    'Banco Santa Cruz',                                 2),
  ('apap',          'Asociacion Popular de Ahorros y Prestamos (APAP)', 2),
  ('scotiabank',    'Scotiabank',                                       2),
  ('acap',          'Asociacion Cibao de Ahorros y Prestamos',          2),
  ('promerica',     'Banco Promerica',                                  3),
  ('caribe',        'Banco Caribe',                                     3),
  ('banesco',       'Banesco',                                          3),
  ('vimenca',       'Banco Vimenca',                                    3),
  ('lopez_de_haro', 'Banco Lopez de Haro',                              3),
  ('ademi',         'Banco Ademi',                                      3),
  ('lafise',        'Banco Lafise',                                     3),
  ('otro',          'Otro / no identificado',                           3)
ON CONFLICT (code) DO UPDATE
  SET name = EXCLUDED.name, parser_wave = EXCLUDED.parser_wave, is_active = true;

-- ---------------------------------------------------------------------
-- Grants (section 7).
-- ---------------------------------------------------------------------
GRANT USAGE ON SCHEMA app TO vepaydo_app, vepaydo_maintenance;

REVOKE ALL ON FUNCTION app.create_tenant(text, uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION app.create_tenant(text, uuid),
      app.current_tenant_id(), app.current_user_id(), app.current_member_role(),
      app.hamming_distance(bigint, bigint) TO vepaydo_app;

GRANT SELECT ON banks TO vepaydo_app;
GRANT SELECT, INSERT, UPDATE ON users, tenants, tenant_settings, business_accounts, customers, charges,
      receipts, extractions, allocations, alerts TO vepaydo_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON sessions, memberships, push_devices, customer_aliases TO vepaydo_app;
GRANT SELECT, INSERT ON audit_events TO vepaydo_app;          -- no UPDATE, no DELETE
GRANT USAGE, SELECT ON SEQUENCE audit_events_id_seq TO vepaydo_app;
GRANT SELECT ON v_charge_balances, v_daily_reconciliation TO vepaydo_app;

-- tenants are never inserted directly: only through app.create_tenant().
REVOKE INSERT ON tenants FROM vepaydo_app;

-- Maintenance (retention purge, cancellations): BYPASSRLS with narrow DELETE.
GRANT SELECT, UPDATE, DELETE ON receipts, extractions, allocations, alerts, charges, customers,
      customer_aliases, business_accounts, tenant_settings, memberships, push_devices, tenants
      TO vepaydo_maintenance;
GRANT SELECT, DELETE ON audit_events TO vepaydo_maintenance;
