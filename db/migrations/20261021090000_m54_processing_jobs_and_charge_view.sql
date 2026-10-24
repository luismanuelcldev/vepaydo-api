-- M5.4: receipt processing queue, receipt list index, and the complete charge
-- balance read model.

-- ---------------------------------------------------------------------
-- processing_jobs: the queue the worker consumes.
--
-- The job id IS the receipt id, so a retry is idempotent by job id
-- (M5.5 acceptance criterion "jobId = receiptId"). The table deliberately
-- has no RLS: the worker must discover pending jobs across tenants, and the
-- only columns it exposes are opaque identifiers plus the job state. No
-- business data (amounts, names, references, image bytes) is ever written
-- here. See ADR 0008.
-- ---------------------------------------------------------------------
CREATE TABLE processing_jobs (
    id           UUID NOT NULL,
    tenant_id    UUID NOT NULL,
    kind         TEXT NOT NULL,
    status       TEXT NOT NULL DEFAULT 'PENDING',
    attempts     SMALLINT NOT NULL DEFAULT 0,
    available_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_error   TEXT,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at   TIMESTAMPTZ NOT NULL,

    CONSTRAINT processing_jobs_pkey PRIMARY KEY (id),
    CONSTRAINT processing_jobs_status_check
        CHECK (status IN ('PENDING', 'RUNNING', 'DONE', 'FAILED')),
    CONSTRAINT processing_jobs_kind_check
        CHECK (kind IN ('RECEIPT_PROCESSING'))
);

CREATE INDEX processing_jobs_pending ON processing_jobs (available_at, created_at)
  WHERE status = 'PENDING';

CREATE TRIGGER t_processing_jobs_updated_at
  BEFORE UPDATE ON processing_jobs
  FOR EACH ROW EXECUTE FUNCTION app.set_updated_at();

GRANT SELECT, INSERT, UPDATE ON processing_jobs TO vepaydo_app;
GRANT SELECT, DELETE ON processing_jobs TO vepaydo_maintenance;

-- ---------------------------------------------------------------------
-- Receipt listing: keyset pagination over the newest receipts, independent
-- of the inbox filters.
-- ---------------------------------------------------------------------
CREATE INDEX receipts_list ON receipts (tenant_id, created_at DESC, id DESC);

-- ---------------------------------------------------------------------
-- v_charge_balances: the view becomes the complete read model.
--
-- It previously exposed only the aggregation columns, so the repository had
-- to read the charges table and the view separately and join them in
-- memory. The view is now the single source of truth for a charge and its
-- balance, and the repository reads it in one query.
--
-- CREATE OR REPLACE VIEW cannot reorder or insert columns, so the view is
-- dropped and recreated inside this migration (each migration runs in its
-- own transaction, so the swap is atomic). The GRANT is re-applied.
-- ---------------------------------------------------------------------
DROP VIEW IF EXISTS v_charge_balances;

CREATE VIEW v_charge_balances WITH (security_invoker = true) AS
SELECT c.tenant_id,
       c.id AS charge_id,
       c.code,
       c.customer_id,
       c.description,
       c.amount,
       c.currency,
       c.issued_on,
       c.due_on,
       c.status,
       COALESCE(sum(a.amount_applied) FILTER (WHERE a.voided_at IS NULL), 0) AS amount_applied,
       c.amount - COALESCE(sum(a.amount_applied) FILTER (WHERE a.voided_at IS NULL), 0) AS balance,
       c.voided_at,
       c.voided_by,
       c.void_reason,
       c.created_by,
       c.created_at,
       c.updated_at
  FROM charges c
  LEFT JOIN allocations a ON a.tenant_id = c.tenant_id AND a.charge_id = c.id
 GROUP BY c.id;

GRANT SELECT ON v_charge_balances TO vepaydo_app;
