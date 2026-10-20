-- Integrity and business-rule triggers.

-- 1. updated_at maintenance on every mutable business table.
DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['tenants', 'tenant_config', 'cuentas_negocio', 'clientes', 'cobros', 'comprobantes'] LOOP
    EXECUTE format(
      'CREATE TRIGGER %I BEFORE UPDATE ON %I FOR EACH ROW EXECUTE FUNCTION app.set_updated_at()',
      't_' || t || '_updated_at', t);
  END LOOP;
END $$;

-- 2. Allocations never exceed the receipt amount and never mix currencies.
CREATE OR REPLACE FUNCTION app.validate_allocation() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
  v_receipt_amount numeric;
  v_receipt_currency char(3);
  v_charge_currency char(3);
  v_applied numeric;
BEGIN
  IF NEW.anulada_en IS NOT NULL THEN
    RETURN NEW;
  END IF;

  -- Row lock serializes concurrent allocations on the same receipt.
  SELECT monto, moneda INTO v_receipt_amount, v_receipt_currency
    FROM comprobantes
   WHERE tenant_id = NEW.tenant_id AND id = NEW.comprobante_id
   FOR UPDATE;

  SELECT moneda INTO v_charge_currency
    FROM cobros
   WHERE tenant_id = NEW.tenant_id AND id = NEW.cobro_id;

  IF v_receipt_amount IS NULL THEN
    RAISE EXCEPTION 'The receipt has no amount' USING ERRCODE = 'check_violation';
  END IF;
  IF v_receipt_currency IS DISTINCT FROM v_charge_currency THEN
    RAISE EXCEPTION 'Receipt and charge currencies do not match' USING ERRCODE = 'check_violation';
  END IF;

  SELECT COALESCE(sum(monto_aplicado), 0) INTO v_applied
    FROM aplicaciones
   WHERE tenant_id = NEW.tenant_id
     AND comprobante_id = NEW.comprobante_id
     AND anulada_en IS NULL
     AND id <> NEW.id;

  IF v_applied + NEW.monto_aplicado > v_receipt_amount THEN
    RAISE EXCEPTION 'Allocated amount (%) exceeds the receipt amount (%)',
      v_applied + NEW.monto_aplicado, v_receipt_amount USING ERRCODE = 'check_violation';
  END IF;
  RETURN NEW;
END $$;

CREATE TRIGGER t_allocations_validate
  BEFORE INSERT OR UPDATE OF monto_aplicado, anulada_en, comprobante_id, cobro_id ON aplicaciones
  FOR EACH ROW EXECUTE FUNCTION app.validate_allocation();

-- 3. Permission matrix enforced by the database (verifier rules).
CREATE OR REPLACE FUNCTION app.enforce_receipt_state_rules() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE v_cap numeric;
BEGIN
  IF NEW.estado_conciliacion IS NOT DISTINCT FROM OLD.estado_conciliacion THEN
    RETURN NEW;
  END IF;

  -- Reverting a final status is owner-only.
  IF OLD.estado_conciliacion IN ('CONFIRMED', 'PARTIAL', 'OVERPAID', 'REJECTED')
     AND COALESCE(app.current_member_role(), '') <> 'owner' THEN
    RAISE EXCEPTION 'Only the owner can revert a final status' USING ERRCODE = 'insufficient_privilege';
  END IF;

  -- A verifier confirms within their cap and with no open critical alerts.
  IF NEW.estado_conciliacion IN ('CONFIRMED', 'PARTIAL', 'OVERPAID')
     AND app.current_member_role() = 'verifier' THEN
    SELECT tope_monto_verificador INTO v_cap
      FROM tenant_config WHERE tenant_id = NEW.tenant_id;
    IF v_cap IS NOT NULL AND NEW.monto > v_cap THEN
      RAISE EXCEPTION 'The amount exceeds the verifier confirmation cap' USING ERRCODE = 'insufficient_privilege';
    END IF;
    IF EXISTS (
      SELECT 1 FROM alertas
       WHERE tenant_id = NEW.tenant_id AND comprobante_id = NEW.id
         AND estado = 'OPEN' AND severidad = 'CRITICAL'
    ) THEN
      RAISE EXCEPTION 'There are open critical alerts: only the owner can resolve them' USING ERRCODE = 'insufficient_privilege';
    END IF;
  END IF;
  RETURN NEW;
END $$;

CREATE TRIGGER t_receipts_state_rules
  BEFORE UPDATE OF estado_conciliacion ON comprobantes
  FOR EACH ROW EXECUTE FUNCTION app.enforce_receipt_state_rules();

-- 4. Audit log is append-only; only the maintenance role may purge (retention/cancellation).
CREATE OR REPLACE FUNCTION app.enforce_audit_immutability() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' AND current_user = 'vepaydo_maintenance' THEN
    RETURN OLD;
  END IF;
  RAISE EXCEPTION 'eventos_auditoria is append-only' USING ERRCODE = 'insufficient_privilege';
END $$;

CREATE TRIGGER t_audit_immutable
  BEFORE UPDATE OR DELETE OR TRUNCATE ON eventos_auditoria
  FOR EACH STATEMENT EXECUTE FUNCTION app.enforce_audit_immutability();
