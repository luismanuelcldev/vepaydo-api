-- Partial, covering, and trigram indexes translated from the reference schema.
-- Prisma does not model these, so they live only in handwritten SQL.

-- Refresh tokens: only active sessions matter for lookups.
CREATE INDEX sesiones_usuario ON sesiones (usuario_id) WHERE revocada_en IS NULL;

-- Memberships: only active memberships matter for lookups.
CREATE INDEX membresias_usuario ON membresias (usuario_id) WHERE activo;

-- Customers: fuzzy name search and tenant-scoped listing.
CREATE INDEX clientes_nombre_trgm ON clientes USING gin (nombre_norm gin_trgm_ops);
CREATE INDEX clientes_tenant_nombre ON clientes (tenant_id, nombre_norm);

-- Customer aliases: fuzzy sender matching.
CREATE INDEX clientes_alias_trgm ON clientes_alias USING gin (alias_norm gin_trgm_ops);

-- Charge candidate lookup for the matching engine.
CREATE INDEX cobros_candidatos ON cobros (tenant_id, monto, fecha_emision)
  WHERE estado IN ('OPEN', 'PARTIAL');
CREATE INDEX cobros_cliente ON cobros (tenant_id, cliente_id, estado);

-- Inbox: "what needs my action".
CREATE INDEX comprobantes_bandeja ON comprobantes (tenant_id, estado_conciliacion, creado_en DESC);

-- Daily reconciliation.
CREATE INDEX comprobantes_cuadre ON comprobantes (tenant_id, fecha_pago, banco_id)
  WHERE estado_conciliacion IN ('CONFIRMED', 'PARTIAL', 'OVERPAID');

-- Duplicate detection by fields. NOT unique on purpose: an owner may "confirm anyway"
-- after reviewing the alert, and that must remain allowed and audited.
CREATE INDEX comprobantes_dup_campos ON comprobantes
  (tenant_id, banco_id, referencia_norm, monto, fecha_pago)
  WHERE referencia_norm IS NOT NULL;

-- Duplicate detection by image content.
CREATE INDEX comprobantes_sha256 ON comprobantes (tenant_id, sha256);

-- Similar images by perceptual hash: covering index to scan a recent window and
-- filter with app.hamming_distance(phash, :new) <= 6.
CREATE INDEX comprobantes_phash_reciente ON comprobantes (tenant_id, creado_en DESC)
  INCLUDE (phash) WHERE phash IS NOT NULL;

-- Retention purge.
CREATE INDEX comprobantes_retencion ON comprobantes (tenant_id, creado_en)
  WHERE imagen_eliminada_en IS NULL;

-- Extractions per receipt, newest first.
CREATE INDEX extracciones_comprobante ON extracciones (tenant_id, comprobante_id, creada_en DESC);

-- One active allocation per (receipt, charge) pair.
CREATE UNIQUE INDEX aplicaciones_vigente_unica ON aplicaciones (tenant_id, comprobante_id, cobro_id)
  WHERE anulada_en IS NULL;
CREATE INDEX aplicaciones_cobro ON aplicaciones (tenant_id, cobro_id) WHERE anulada_en IS NULL;

-- One open alert per (receipt, rule); closed alerts are kept for history.
CREATE UNIQUE INDEX alertas_regla_abierta ON alertas (tenant_id, comprobante_id, regla)
  WHERE estado = 'OPEN';
CREATE INDEX alertas_abiertas ON alertas (tenant_id, severidad, creada_en DESC)
  WHERE estado = 'OPEN';

-- Audit navigation.
CREATE INDEX auditoria_entidad ON eventos_auditoria (tenant_id, entidad, entidad_id, creado_en DESC);
CREATE INDEX auditoria_tiempo ON eventos_auditoria (tenant_id, creado_en DESC);
