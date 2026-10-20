-- CHECK constraints translated from the reference schema (Spanish -> English identifiers).

ALTER TABLE banks
  ADD CONSTRAINT banks_parser_wave_range CHECK (parser_wave BETWEEN 1 AND 3);

ALTER TABLE tenants
  ADD CONSTRAINT tenants_status_valid CHECK (status IN ('active', 'suspended', 'cancelled')),
  ADD CONSTRAINT tenants_canceled_coherent CHECK ((status = 'cancelled') = (cancelled_at IS NOT NULL));

ALTER TABLE tenant_settings
  ADD CONSTRAINT tenant_settings_date_window_range CHECK (date_window_days BETWEEN 1 AND 60),
  ADD CONSTRAINT tenant_settings_tolerance_non_negative CHECK (amount_tolerance >= 0),
  ADD CONSTRAINT tenant_settings_verifier_cap_positive CHECK (verifier_amount_cap IS NULL OR verifier_amount_cap > 0),
  ADD CONSTRAINT tenant_settings_image_retention_range CHECK (image_retention_months BETWEEN 1 AND 120);

ALTER TABLE business_accounts
  ADD CONSTRAINT business_accounts_last4_format CHECK (last4 ~ '^[0-9]{4}$'),
  ADD CONSTRAINT business_accounts_currency_valid CHECK (currency IN ('DOP', 'USD'));

ALTER TABLE charges
  ADD CONSTRAINT charges_amount_positive CHECK (amount > 0),
  ADD CONSTRAINT charges_currency_valid CHECK (currency IN ('DOP', 'USD')),
  ADD CONSTRAINT charges_due_after_issue CHECK (due_on IS NULL OR due_on >= issued_on),
  ADD CONSTRAINT charges_voided_coherent CHECK ((status = 'VOIDED') = (voided_at IS NOT NULL));

ALTER TABLE receipts
  ADD CONSTRAINT receipts_mime_allowed CHECK (mime IN ('image/jpeg', 'image/png', 'image/webp', 'application/pdf')),
  ADD CONSTRAINT receipts_bytes_positive CHECK (bytes > 0),
  ADD CONSTRAINT receipts_sha256_length CHECK (octet_length(sha256) = 32),
  ADD CONSTRAINT receipts_amount_positive CHECK (amount IS NULL OR amount > 0),
  ADD CONSTRAINT receipts_currency_valid CHECK (currency IS NULL OR currency IN ('DOP', 'USD')),
  ADD CONSTRAINT receipts_final_requires_data CHECK (
    reconciliation_status NOT IN ('CONFIRMED', 'PARTIAL', 'OVERPAID')
    OR (amount IS NOT NULL AND currency IS NOT NULL AND payment_date IS NOT NULL AND bank_id IS NOT NULL)
  ),
  ADD CONSTRAINT receipts_final_requires_decision CHECK (
    reconciliation_status NOT IN ('CONFIRMED', 'PARTIAL', 'OVERPAID', 'REJECTED')
    OR (decided_by IS NOT NULL AND decided_at IS NOT NULL)
  );

ALTER TABLE extractions
  ADD CONSTRAINT extractions_confidence_range CHECK (min_confidence BETWEEN 0 AND 1);

ALTER TABLE allocations
  ADD CONSTRAINT allocations_amount_positive CHECK (amount_applied > 0),
  ADD CONSTRAINT allocations_voided_coherent CHECK ((voided_at IS NULL) = (voided_by IS NULL));

-- §4.5: resolved_by may be NULL only for AUTO_CLEARED resolutions.
ALTER TABLE alerts
  ADD CONSTRAINT alerts_resolved_coherent CHECK (
    status <> 'RESOLVED'
    OR (resolution IS NOT NULL
        AND resolved_at IS NOT NULL
        AND (resolved_by IS NOT NULL OR resolution = 'AUTO_CLEARED'))
  ),
  ADD CONSTRAINT alerts_unresolved_clean CHECK (
    status <> 'OPEN'
    OR (resolution IS NULL AND resolved_at IS NULL AND resolved_by IS NULL)
  ),
  ADD CONSTRAINT alerts_dismissed_requires_reason CHECK (
    resolution IS DISTINCT FROM 'DISMISSED' OR resolution_reason IS NOT NULL
  );
