-- CreateEnum
CREATE TYPE "member_role" AS ENUM ('OWNER', 'VERIFIER');

-- CreateEnum
CREATE TYPE "processing_status" AS ENUM ('RECEIVED', 'PROCESSING', 'EXTRACTED', 'FAILED');

-- CreateEnum
CREATE TYPE "reconciliation_status" AS ENUM ('PENDING_REVIEW', 'CONFIRMED', 'PARTIAL', 'OVERPAID', 'DUPLICATE', 'SUSPICIOUS', 'UNMATCHED', 'REJECTED');

-- CreateEnum
CREATE TYPE "alert_severity" AS ENUM ('INFO', 'WARNING', 'CRITICAL');

-- CreateEnum
CREATE TYPE "alert_status" AS ENUM ('OPEN', 'RESOLVED');

-- CreateEnum
CREATE TYPE "alert_resolution" AS ENUM ('DISMISSED', 'CONFIRMED_ANYWAY', 'REJECTED', 'AUTO_CLEARED');

-- CreateEnum
CREATE TYPE "charge_status" AS ENUM ('OPEN', 'PARTIAL', 'PAID', 'VOIDED');

-- CreateEnum
CREATE TYPE "receipt_source" AS ENUM ('WEB_UPLOAD', 'CAMERA', 'SHARE_TARGET');

-- CreateEnum
CREATE TYPE "transfer_type" AS ENUM ('SAME_BANK', 'INTERBANK', 'UNKNOWN');

-- CreateEnum
CREATE TYPE "tenant_plan" AS ENUM ('PILOT', 'ESSENTIAL', 'BUSINESS');

-- CreateEnum
CREATE TYPE "alert_rule_code" AS ENUM ('DUPLICATE_REFERENCE', 'IDENTICAL_IMAGE', 'SIMILAR_IMAGE', 'FOREIGN_DESTINATION_ACCOUNT', 'FUTURE_DATE', 'DATE_BEFORE_CHARGE', 'STALE_DATE', 'INVALID_REFERENCE_FORMAT', 'AMOUNT_MISMATCH', 'SENDER_MISMATCH', 'LOW_CONFIDENCE');

-- CreateTable
CREATE TABLE "banks" (
    "id" SMALLSERIAL NOT NULL,
    "codigo" TEXT NOT NULL,
    "nombre" TEXT NOT NULL,
    "oleada" SMALLINT NOT NULL DEFAULT 3,
    "activo" BOOLEAN NOT NULL DEFAULT true,

    CONSTRAINT "banks_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "users" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "email" CITEXT NOT NULL,
    "nombre" TEXT NOT NULL,
    "password_hash" TEXT NOT NULL,
    "activo" BOOLEAN NOT NULL DEFAULT true,
    "ultimo_acceso" TIMESTAMPTZ,
    "creado_en" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "preferred_locale" TEXT NOT NULL DEFAULT 'es-DO',

    CONSTRAINT "users_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "sesiones" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "usuario_id" UUID NOT NULL,
    "token_hash" BYTEA NOT NULL,
    "expira_en" TIMESTAMPTZ NOT NULL,
    "revocada_en" TIMESTAMPTZ,
    "user_agent" TEXT,
    "creada_en" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "sesiones_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "tenants" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "nombre" TEXT NOT NULL,
    "rnc" TEXT,
    "zona_horaria" TEXT NOT NULL DEFAULT 'America/Santo_Domingo',
    "plan" "tenant_plan" NOT NULL DEFAULT 'PILOT',
    "estado" TEXT NOT NULL DEFAULT 'activo',
    "cancelado_en" TIMESTAMPTZ,
    "creado_en" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "actualizado_en" TIMESTAMPTZ NOT NULL,

    CONSTRAINT "tenants_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "tenant_config" (
    "tenant_id" UUID NOT NULL,
    "ventana_fecha_dias" SMALLINT NOT NULL DEFAULT 7,
    "tolerancia_monto" DECIMAL(14,2) NOT NULL DEFAULT 0,
    "tope_monto_verificador" DECIMAL(14,2),
    "retencion_imagenes_meses" SMALLINT NOT NULL DEFAULT 24,
    "actualizado_en" TIMESTAMPTZ NOT NULL,

    CONSTRAINT "tenant_config_pkey" PRIMARY KEY ("tenant_id")
);

-- CreateTable
CREATE TABLE "membresias" (
    "tenant_id" UUID NOT NULL,
    "usuario_id" UUID NOT NULL,
    "rol" "member_role" NOT NULL,
    "activo" BOOLEAN NOT NULL DEFAULT true,
    "creado_en" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "membresias_pkey" PRIMARY KEY ("tenant_id","usuario_id")
);

-- CreateTable
CREATE TABLE "dispositivos_push" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "usuario_id" UUID NOT NULL,
    "proveedor" TEXT NOT NULL DEFAULT 'fcm',
    "token" TEXT NOT NULL,
    "user_agent" TEXT,
    "creada_en" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "ultimo_uso" TIMESTAMPTZ,

    CONSTRAINT "dispositivos_push_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "cuentas_negocio" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "banco_id" SMALLINT NOT NULL,
    "alias" TEXT NOT NULL,
    "ultimos4" CHAR(4) NOT NULL,
    "moneda" CHAR(3) NOT NULL DEFAULT 'DOP',
    "activa" BOOLEAN NOT NULL DEFAULT true,
    "creada_en" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "actualizado_en" TIMESTAMPTZ NOT NULL,

    CONSTRAINT "cuentas_negocio_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "clientes" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "nombre" TEXT NOT NULL,
    "nombre_norm" TEXT NOT NULL,
    "telefono" TEXT,
    "notas" TEXT,
    "activo" BOOLEAN NOT NULL DEFAULT true,
    "creado_en" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "actualizado_en" TIMESTAMPTZ NOT NULL,

    CONSTRAINT "clientes_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "clientes_alias" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "cliente_id" UUID NOT NULL,
    "alias_norm" TEXT NOT NULL,

    CONSTRAINT "clientes_alias_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "cobros" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "cliente_id" UUID,
    "codigo" TEXT NOT NULL,
    "concepto" TEXT NOT NULL,
    "monto" DECIMAL(14,2) NOT NULL,
    "moneda" CHAR(3) NOT NULL DEFAULT 'DOP',
    "fecha_emision" DATE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "fecha_vencimiento" DATE,
    "estado" "charge_status" NOT NULL DEFAULT 'OPEN',
    "anulado_en" TIMESTAMPTZ,
    "anulado_por" UUID,
    "motivo_anulacion" TEXT,
    "creado_por" UUID,
    "creado_en" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "actualizado_en" TIMESTAMPTZ NOT NULL,

    CONSTRAINT "cobros_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "comprobantes" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "idempotency_key" UUID NOT NULL,
    "subido_por" UUID NOT NULL,
    "origen" "receipt_source" NOT NULL,
    "storage_key" TEXT NOT NULL,
    "mime" TEXT NOT NULL,
    "bytes" INTEGER NOT NULL,
    "ancho" INTEGER,
    "alto" INTEGER,
    "sha256" BYTEA NOT NULL,
    "phash" BIGINT,
    "imagen_eliminada_en" TIMESTAMPTZ,
    "estado_procesamiento" "processing_status" NOT NULL DEFAULT 'RECEIVED',
    "estado_conciliacion" "reconciliation_status" NOT NULL DEFAULT 'PENDING_REVIEW',
    "riesgo" "alert_severity",
    "intentos" SMALLINT NOT NULL DEFAULT 0,
    "error_procesamiento" TEXT,
    "banco_id" SMALLINT,
    "tipo_transferencia" "transfer_type" NOT NULL DEFAULT 'UNKNOWN',
    "monto" DECIMAL(14,2),
    "moneda" CHAR(3),
    "fecha_pago" DATE,
    "hora_pago" TIME,
    "referencia" TEXT,
    "referencia_norm" TEXT,
    "remitente" TEXT,
    "remitente_norm" TEXT,
    "cuenta_destino_ultimos4" CHAR(4),
    "cuenta_destino_id" UUID,
    "cliente_id" UUID,
    "es_ingreso_libre" BOOLEAN NOT NULL DEFAULT false,
    "suggested_charge_id" UUID,
    "match_score" DECIMAL(4,3),
    "match_signals" JSONB,
    "decidido_por" UUID,
    "decidido_en" TIMESTAMPTZ,
    "motivo_decision" TEXT,
    "creado_en" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "actualizado_en" TIMESTAMPTZ NOT NULL,

    CONSTRAINT "comprobantes_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "extracciones" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "comprobante_id" UUID NOT NULL,
    "motor" TEXT NOT NULL,
    "parser" TEXT,
    "banco_detectado_id" SMALLINT,
    "campos" JSONB NOT NULL,
    "confianza_min" DECIMAL(4,3),
    "crudo" JSONB,
    "crudo_eliminado_en" TIMESTAMPTZ,
    "duracion_ms" INTEGER,
    "exitosa" BOOLEAN NOT NULL,
    "error" TEXT,
    "creada_en" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "extracciones_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "aplicaciones" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "comprobante_id" UUID NOT NULL,
    "cobro_id" UUID NOT NULL,
    "monto_aplicado" DECIMAL(14,2) NOT NULL,
    "creada_por" UUID NOT NULL,
    "creada_en" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "anulada_en" TIMESTAMPTZ,
    "anulada_por" UUID,
    "motivo_anulacion" TEXT,

    CONSTRAINT "aplicaciones_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "alertas" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "comprobante_id" UUID NOT NULL,
    "regla" "alert_rule_code" NOT NULL,
    "severidad" "alert_severity" NOT NULL,
    "mensaje" TEXT NOT NULL,
    "detalle" JSONB NOT NULL DEFAULT '{}',
    "estado" "alert_status" NOT NULL DEFAULT 'OPEN',
    "resolucion" "alert_resolution",
    "resuelta_por" UUID,
    "resuelta_en" TIMESTAMPTZ,
    "motivo_resolucion" TEXT,
    "creada_en" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "alertas_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "eventos_auditoria" (
    "id" BIGSERIAL NOT NULL,
    "tenant_id" UUID NOT NULL,
    "usuario_id" UUID,
    "actor" TEXT NOT NULL DEFAULT 'usuario',
    "entidad" TEXT NOT NULL,
    "entidad_id" UUID,
    "accion" TEXT NOT NULL,
    "antes" JSONB,
    "despues" JSONB,
    "motivo" TEXT,
    "ip" INET,
    "creado_en" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "eventos_auditoria_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "banks_codigo_key" ON "banks"("codigo");

-- CreateIndex
CREATE UNIQUE INDEX "users_email_key" ON "users"("email");

-- CreateIndex
CREATE UNIQUE INDEX "sesiones_token_hash_key" ON "sesiones"("token_hash");

-- CreateIndex
CREATE UNIQUE INDEX "dispositivos_push_proveedor_token_key" ON "dispositivos_push"("proveedor", "token");

-- CreateIndex
CREATE UNIQUE INDEX "cuentas_negocio_tenant_id_id_key" ON "cuentas_negocio"("tenant_id", "id");

-- CreateIndex
CREATE UNIQUE INDEX "cuentas_negocio_tenant_id_banco_id_ultimos4_moneda_key" ON "cuentas_negocio"("tenant_id", "banco_id", "ultimos4", "moneda");

-- CreateIndex
CREATE UNIQUE INDEX "clientes_tenant_id_id_key" ON "clientes"("tenant_id", "id");

-- CreateIndex
CREATE UNIQUE INDEX "clientes_alias_tenant_id_alias_norm_key" ON "clientes_alias"("tenant_id", "alias_norm");

-- CreateIndex
CREATE UNIQUE INDEX "cobros_tenant_id_id_key" ON "cobros"("tenant_id", "id");

-- CreateIndex
CREATE UNIQUE INDEX "cobros_tenant_id_codigo_key" ON "cobros"("tenant_id", "codigo");

-- CreateIndex
CREATE UNIQUE INDEX "comprobantes_tenant_id_id_key" ON "comprobantes"("tenant_id", "id");

-- CreateIndex
CREATE UNIQUE INDEX "comprobantes_tenant_id_idempotency_key_key" ON "comprobantes"("tenant_id", "idempotency_key");

-- CreateIndex
CREATE UNIQUE INDEX "extracciones_tenant_id_id_key" ON "extracciones"("tenant_id", "id");

-- CreateIndex
CREATE UNIQUE INDEX "aplicaciones_tenant_id_id_key" ON "aplicaciones"("tenant_id", "id");

-- CreateIndex
CREATE UNIQUE INDEX "alertas_tenant_id_id_key" ON "alertas"("tenant_id", "id");

-- AddForeignKey
ALTER TABLE "sesiones" ADD CONSTRAINT "sesiones_usuario_id_fkey" FOREIGN KEY ("usuario_id") REFERENCES "users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "tenant_config" ADD CONSTRAINT "tenant_config_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "membresias" ADD CONSTRAINT "membresias_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "membresias" ADD CONSTRAINT "membresias_usuario_id_fkey" FOREIGN KEY ("usuario_id") REFERENCES "users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "dispositivos_push" ADD CONSTRAINT "dispositivos_push_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "dispositivos_push" ADD CONSTRAINT "dispositivos_push_tenant_id_usuario_id_fkey" FOREIGN KEY ("tenant_id", "usuario_id") REFERENCES "membresias"("tenant_id", "usuario_id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "cuentas_negocio" ADD CONSTRAINT "cuentas_negocio_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "cuentas_negocio" ADD CONSTRAINT "cuentas_negocio_banco_id_fkey" FOREIGN KEY ("banco_id") REFERENCES "banks"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "clientes" ADD CONSTRAINT "clientes_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "clientes_alias" ADD CONSTRAINT "clientes_alias_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "clientes_alias" ADD CONSTRAINT "clientes_alias_tenant_id_cliente_id_fkey" FOREIGN KEY ("tenant_id", "cliente_id") REFERENCES "clientes"("tenant_id", "id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "cobros" ADD CONSTRAINT "cobros_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "cobros" ADD CONSTRAINT "cobros_tenant_id_cliente_id_fkey" FOREIGN KEY ("tenant_id", "cliente_id") REFERENCES "clientes"("tenant_id", "id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "cobros" ADD CONSTRAINT "cobros_creado_por_fkey" FOREIGN KEY ("creado_por") REFERENCES "users"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "cobros" ADD CONSTRAINT "cobros_anulado_por_fkey" FOREIGN KEY ("anulado_por") REFERENCES "users"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "comprobantes" ADD CONSTRAINT "comprobantes_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "comprobantes" ADD CONSTRAINT "comprobantes_subido_por_fkey" FOREIGN KEY ("subido_por") REFERENCES "users"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "comprobantes" ADD CONSTRAINT "comprobantes_decidido_por_fkey" FOREIGN KEY ("decidido_por") REFERENCES "users"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "comprobantes" ADD CONSTRAINT "comprobantes_banco_id_fkey" FOREIGN KEY ("banco_id") REFERENCES "banks"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "comprobantes" ADD CONSTRAINT "comprobantes_tenant_id_cuenta_destino_id_fkey" FOREIGN KEY ("tenant_id", "cuenta_destino_id") REFERENCES "cuentas_negocio"("tenant_id", "id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "comprobantes" ADD CONSTRAINT "comprobantes_tenant_id_cliente_id_fkey" FOREIGN KEY ("tenant_id", "cliente_id") REFERENCES "clientes"("tenant_id", "id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "comprobantes" ADD CONSTRAINT "comprobantes_tenant_id_suggested_charge_id_fkey" FOREIGN KEY ("tenant_id", "suggested_charge_id") REFERENCES "cobros"("tenant_id", "id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "extracciones" ADD CONSTRAINT "extracciones_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "extracciones" ADD CONSTRAINT "extracciones_tenant_id_comprobante_id_fkey" FOREIGN KEY ("tenant_id", "comprobante_id") REFERENCES "comprobantes"("tenant_id", "id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "extracciones" ADD CONSTRAINT "extracciones_banco_detectado_id_fkey" FOREIGN KEY ("banco_detectado_id") REFERENCES "banks"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "aplicaciones" ADD CONSTRAINT "aplicaciones_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "aplicaciones" ADD CONSTRAINT "aplicaciones_tenant_id_comprobante_id_fkey" FOREIGN KEY ("tenant_id", "comprobante_id") REFERENCES "comprobantes"("tenant_id", "id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "aplicaciones" ADD CONSTRAINT "aplicaciones_tenant_id_cobro_id_fkey" FOREIGN KEY ("tenant_id", "cobro_id") REFERENCES "cobros"("tenant_id", "id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "aplicaciones" ADD CONSTRAINT "aplicaciones_creada_por_fkey" FOREIGN KEY ("creada_por") REFERENCES "users"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "aplicaciones" ADD CONSTRAINT "aplicaciones_anulada_por_fkey" FOREIGN KEY ("anulada_por") REFERENCES "users"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "alertas" ADD CONSTRAINT "alertas_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "alertas" ADD CONSTRAINT "alertas_tenant_id_comprobante_id_fkey" FOREIGN KEY ("tenant_id", "comprobante_id") REFERENCES "comprobantes"("tenant_id", "id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "alertas" ADD CONSTRAINT "alertas_resuelta_por_fkey" FOREIGN KEY ("resuelta_por") REFERENCES "users"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "eventos_auditoria" ADD CONSTRAINT "eventos_auditoria_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "eventos_auditoria" ADD CONSTRAINT "eventos_auditoria_usuario_id_fkey" FOREIGN KEY ("usuario_id") REFERENCES "users"("id") ON DELETE SET NULL ON UPDATE CASCADE;
