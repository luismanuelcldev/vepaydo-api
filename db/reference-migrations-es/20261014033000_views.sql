-- Reporting views. security_invoker = true so they honor the caller's RLS.

CREATE VIEW v_charge_balances WITH (security_invoker = true) AS
SELECT c.tenant_id,
       c.id AS cobro_id,
       c.codigo,
       c.cliente_id,
       c.monto,
       c.moneda,
       c.estado,
       COALESCE(sum(a.monto_aplicado) FILTER (WHERE a.anulada_en IS NULL), 0) AS monto_aplicado,
       c.monto - COALESCE(sum(a.monto_aplicado) FILTER (WHERE a.anulada_en IS NULL), 0) AS saldo
  FROM cobros c
  LEFT JOIN aplicaciones a ON a.tenant_id = c.tenant_id AND a.cobro_id = c.id
 GROUP BY c.id;

CREATE VIEW v_daily_reconciliation WITH (security_invoker = true) AS
SELECT tenant_id, fecha_pago, banco_id, cuenta_destino_id, moneda, estado_conciliacion,
       count(*) AS cantidad, sum(monto) AS total
  FROM comprobantes
 WHERE fecha_pago IS NOT NULL AND monto IS NOT NULL
 GROUP BY tenant_id, fecha_pago, banco_id, cuenta_destino_id, moneda, estado_conciliacion;
