-- Reporting views. security_invoker = true so they honor the caller's RLS.

CREATE VIEW v_charge_balances WITH (security_invoker = true) AS
SELECT c.tenant_id,
       c.id AS charge_id,
       c.code,
       c.customer_id,
       c.amount,
       c.currency,
       c.status,
       COALESCE(sum(a.amount_applied) FILTER (WHERE a.voided_at IS NULL), 0) AS amount_applied,
       c.amount - COALESCE(sum(a.amount_applied) FILTER (WHERE a.voided_at IS NULL), 0) AS balance
  FROM charges c
  LEFT JOIN allocations a ON a.tenant_id = c.tenant_id AND a.charge_id = c.id
 GROUP BY c.id;

CREATE VIEW v_daily_reconciliation WITH (security_invoker = true) AS
SELECT tenant_id, payment_date, bank_id, destination_account_id, currency, reconciliation_status,
       count(*) AS count, sum(amount) AS total
  FROM receipts
 WHERE payment_date IS NOT NULL AND amount IS NOT NULL
 GROUP BY tenant_id, payment_date, bank_id, destination_account_id, currency, reconciliation_status;
