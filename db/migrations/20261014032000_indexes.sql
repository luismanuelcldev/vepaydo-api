-- Partial, covering, and trigram indexes translated from the reference schema.
-- Prisma does not model these, so they live only in handwritten SQL.

-- Refresh tokens: only active sessions matter for lookups.
CREATE INDEX sessions_user ON sessions (user_id) WHERE revoked_at IS NULL;

-- Memberships: only active memberships matter for lookups.
CREATE INDEX memberships_user ON memberships (user_id) WHERE is_active;

-- Customers: fuzzy name search and tenant-scoped listing.
CREATE INDEX customers_name_trgm ON customers USING gin (name_normalized gin_trgm_ops);
CREATE INDEX customers_tenant_name ON customers (tenant_id, name_normalized);

-- Customer aliases: fuzzy sender matching.
CREATE INDEX customer_aliases_trgm ON customer_aliases USING gin (alias_normalized gin_trgm_ops);

-- Charge candidate lookup for the matching engine.
CREATE INDEX charges_candidates ON charges (tenant_id, amount, issued_on)
  WHERE status IN ('OPEN', 'PARTIAL');
CREATE INDEX charges_customer ON charges (tenant_id, customer_id, status);

-- Inbox: "what needs my action".
CREATE INDEX receipts_inbox ON receipts (tenant_id, reconciliation_status, created_at DESC);

-- Daily reconciliation.
CREATE INDEX receipts_reconciliation ON receipts (tenant_id, payment_date, bank_id)
  WHERE reconciliation_status IN ('CONFIRMED', 'PARTIAL', 'OVERPAID');

-- Duplicate detection by fields. NOT unique on purpose: an owner may "confirm anyway"
-- after reviewing the alert, and that must remain allowed and audited.
CREATE INDEX receipts_dup_fields ON receipts
  (tenant_id, bank_id, reference_normalized, amount, payment_date)
  WHERE reference_normalized IS NOT NULL;

-- Duplicate detection by image content.
CREATE INDEX receipts_sha256 ON receipts (tenant_id, sha256);

-- Similar images by perceptual hash: covering index to scan a recent window and
-- filter with app.hamming_distance(phash, :new) <= 6.
CREATE INDEX receipts_phash_recent ON receipts (tenant_id, created_at DESC)
  INCLUDE (phash) WHERE phash IS NOT NULL;

-- Retention purge.
CREATE INDEX receipts_retention ON receipts (tenant_id, created_at)
  WHERE image_deleted_at IS NULL;

-- Extractions per receipt, newest first.
CREATE INDEX extractions_receipt ON extractions (tenant_id, receipt_id, created_at DESC);

-- One active allocation per (receipt, charge) pair.
CREATE UNIQUE INDEX allocations_active_unique ON allocations (tenant_id, receipt_id, charge_id)
  WHERE voided_at IS NULL;
CREATE INDEX allocations_charge ON allocations (tenant_id, charge_id) WHERE voided_at IS NULL;

-- One open alert per (receipt, rule); closed alerts are kept for history.
CREATE UNIQUE INDEX alerts_rule_open ON alerts (tenant_id, receipt_id, rule)
  WHERE status = 'OPEN';
CREATE INDEX alerts_open ON alerts (tenant_id, severity, created_at DESC)
  WHERE status = 'OPEN';

-- Audit navigation.
CREATE INDEX audit_entity ON audit_events (tenant_id, entity, entity_id, created_at DESC);
CREATE INDEX audit_time ON audit_events (tenant_id, created_at DESC);
