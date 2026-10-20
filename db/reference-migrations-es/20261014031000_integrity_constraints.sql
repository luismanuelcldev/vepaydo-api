-- CHECK constraints translated from the reference schema (Spanish -> English identifiers).

ALTER TABLE banks
  ADD CONSTRAINT banks_parser_wave_range CHECK (oleada BETWEEN 1 AND 3);

ALTER TABLE tenants
  ADD CONSTRAINT tenants_status_valid CHECK (estado IN ('activo', 'suspendido', 'cancelado')),
  ADD CONSTRAINT tenants_canceled_coherent CHECK ((estado = 'cancelado') = (cancelado_en IS NOT NULL));

ALTER TABLE tenant_config
  ADD CONSTRAINT tenant_config_date_window_range CHECK (ventana_fecha_dias BETWEEN 1 AND 60),
  ADD CONSTRAINT tenant_config_tolerance_non_negative CHECK (tolerancia_monto >= 0),
  ADD CONSTRAINT tenant_config_verifier_cap_positive CHECK (tope_monto_verificador IS NULL OR tope_monto_verificador > 0),
  ADD CONSTRAINT tenant_config_image_retention_range CHECK (retencion_imagenes_meses BETWEEN 1 AND 120);

ALTER TABLE cuentas_negocio
  ADD CONSTRAINT business_accounts_last4_format CHECK (ultimos4 ~ '^[0-9]{4}$'),
  ADD CONSTRAINT business_accounts_currency_valid CHECK (moneda IN ('DOP', 'USD'));

ALTER TABLE cobros
  ADD CONSTRAINT charges_amount_positive CHECK (monto > 0),
  ADD CONSTRAINT charges_currency_valid CHECK (moneda IN ('DOP', 'USD')),
  ADD CONSTRAINT charges_due_after_issue CHECK (fecha_vencimiento IS NULL OR fecha_vencimiento >= fecha_emision),
  ADD CONSTRAINT charges_voided_coherent CHECK ((estado = 'VOIDED') = (anulado_en IS NOT NULL));

ALTER TABLE comprobantes
  ADD CONSTRAINT receipts_mime_allowed CHECK (mime IN ('image/jpeg', 'image/png', 'image/webp', 'application/pdf')),
  ADD CONSTRAINT receipts_bytes_positive CHECK (bytes > 0),
  ADD CONSTRAINT receipts_sha256_length CHECK (octet_length(sha256) = 32),
  ADD CONSTRAINT receipts_amount_positive CHECK (monto IS NULL OR monto > 0),
  ADD CONSTRAINT receipts_currency_valid CHECK (moneda IS NULL OR moneda IN ('DOP', 'USD')),
  ADD CONSTRAINT receipts_final_requires_data CHECK (
    estado_conciliacion NOT IN ('CONFIRMED', 'PARTIAL', 'OVERPAID')
    OR (monto IS NOT NULL AND moneda IS NOT NULL AND fecha_pago IS NOT NULL AND banco_id IS NOT NULL)
  ),
  ADD CONSTRAINT receipts_final_requires_decision CHECK (
    estado_conciliacion NOT IN ('CONFIRMED', 'PARTIAL', 'OVERPAID', 'REJECTED')
    OR (decidido_por IS NOT NULL AND decidido_en IS NOT NULL)
  );

ALTER TABLE extracciones
  ADD CONSTRAINT extractions_confidence_range CHECK (confianza_min BETWEEN 0 AND 1);

ALTER TABLE aplicaciones
  ADD CONSTRAINT allocations_amount_positive CHECK (monto_aplicado > 0),
  ADD CONSTRAINT allocations_voided_coherent CHECK ((anulada_en IS NULL) = (anulada_por IS NULL));

-- §4.5: resolved_by may be NULL only for AUTO_CLEARED resolutions.
ALTER TABLE alertas
  ADD CONSTRAINT alerts_resolved_coherent CHECK (
    estado <> 'RESOLVED'
    OR (resolucion IS NOT NULL
        AND resuelta_en IS NOT NULL
        AND (resuelta_por IS NOT NULL OR resolucion = 'AUTO_CLEARED'))
  ),
  ADD CONSTRAINT alerts_unresolved_clean CHECK (
    estado <> 'OPEN'
    OR (resolucion IS NULL AND resuelta_en IS NULL AND resuelta_por IS NULL)
  ),
  ADD CONSTRAINT alerts_dismissed_requires_reason CHECK (
    resolucion IS DISTINCT FROM 'DISMISSED' OR motivo_resolucion IS NOT NULL
  );
