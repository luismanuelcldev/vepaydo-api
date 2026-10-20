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
    "code" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "parser_wave" SMALLINT NOT NULL DEFAULT 3,
    "is_active" BOOLEAN NOT NULL DEFAULT true,

    CONSTRAINT "banks_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "users" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "email" CITEXT NOT NULL,
    "name" TEXT NOT NULL,
    "password_hash" TEXT NOT NULL,
    "is_active" BOOLEAN NOT NULL DEFAULT true,
    "last_login_at" TIMESTAMPTZ,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "preferred_locale" TEXT NOT NULL DEFAULT 'es-DO',

    CONSTRAINT "users_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "sessions" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "user_id" UUID NOT NULL,
    "token_hash" BYTEA NOT NULL,
    "expires_at" TIMESTAMPTZ NOT NULL,
    "revoked_at" TIMESTAMPTZ,
    "user_agent" TEXT,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "sessions_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "tenants" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "name" TEXT NOT NULL,
    "rnc" TEXT,
    "time_zone" TEXT NOT NULL DEFAULT 'America/Santo_Domingo',
    "plan" "tenant_plan" NOT NULL DEFAULT 'PILOT',
    "status" TEXT NOT NULL DEFAULT 'active',
    "cancelled_at" TIMESTAMPTZ,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ NOT NULL,

    CONSTRAINT "tenants_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "tenant_settings" (
    "tenant_id" UUID NOT NULL,
    "date_window_days" SMALLINT NOT NULL DEFAULT 7,
    "amount_tolerance" DECIMAL(14,2) NOT NULL DEFAULT 0,
    "verifier_amount_cap" DECIMAL(14,2),
    "image_retention_months" SMALLINT NOT NULL DEFAULT 24,
    "updated_at" TIMESTAMPTZ NOT NULL,

    CONSTRAINT "tenant_settings_pkey" PRIMARY KEY ("tenant_id")
);

-- CreateTable
CREATE TABLE "memberships" (
    "tenant_id" UUID NOT NULL,
    "user_id" UUID NOT NULL,
    "role" "member_role" NOT NULL,
    "is_active" BOOLEAN NOT NULL DEFAULT true,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "memberships_pkey" PRIMARY KEY ("tenant_id","user_id")
);

-- CreateTable
CREATE TABLE "push_devices" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "user_id" UUID NOT NULL,
    "provider" TEXT NOT NULL DEFAULT 'fcm',
    "token" TEXT NOT NULL,
    "user_agent" TEXT,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "last_used" TIMESTAMPTZ,

    CONSTRAINT "push_devices_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "business_accounts" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "bank_id" SMALLINT NOT NULL,
    "label" TEXT NOT NULL,
    "last4" CHAR(4) NOT NULL,
    "currency" CHAR(3) NOT NULL DEFAULT 'DOP',
    "is_active" BOOLEAN NOT NULL DEFAULT true,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ NOT NULL,

    CONSTRAINT "business_accounts_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "customers" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "name" TEXT NOT NULL,
    "name_normalized" TEXT NOT NULL,
    "phone" TEXT,
    "notes" TEXT,
    "is_active" BOOLEAN NOT NULL DEFAULT true,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ NOT NULL,

    CONSTRAINT "customers_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "customer_aliases" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "customer_id" UUID NOT NULL,
    "alias_normalized" TEXT NOT NULL,

    CONSTRAINT "customer_aliases_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "charges" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "customer_id" UUID,
    "code" TEXT NOT NULL,
    "description" TEXT NOT NULL,
    "amount" DECIMAL(14,2) NOT NULL,
    "currency" CHAR(3) NOT NULL DEFAULT 'DOP',
    "issued_on" DATE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "due_on" DATE,
    "status" "charge_status" NOT NULL DEFAULT 'OPEN',
    "voided_at" TIMESTAMPTZ,
    "voided_by" UUID,
    "void_reason" TEXT,
    "created_by" UUID,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ NOT NULL,

    CONSTRAINT "charges_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "receipts" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "idempotency_key" UUID NOT NULL,
    "uploaded_by" UUID NOT NULL,
    "source" "receipt_source" NOT NULL,
    "storage_key" TEXT NOT NULL,
    "mime" TEXT NOT NULL,
    "bytes" INTEGER NOT NULL,
    "width" INTEGER,
    "height" INTEGER,
    "sha256" BYTEA NOT NULL,
    "phash" BIGINT,
    "image_deleted_at" TIMESTAMPTZ,
    "processing_status" "processing_status" NOT NULL DEFAULT 'RECEIVED',
    "reconciliation_status" "reconciliation_status" NOT NULL DEFAULT 'PENDING_REVIEW',
    "risk" "alert_severity",
    "attempts" SMALLINT NOT NULL DEFAULT 0,
    "processing_error" TEXT,
    "bank_id" SMALLINT,
    "transfer_type" "transfer_type" NOT NULL DEFAULT 'UNKNOWN',
    "amount" DECIMAL(14,2),
    "currency" CHAR(3),
    "payment_date" DATE,
    "payment_time" TIME,
    "reference" TEXT,
    "reference_normalized" TEXT,
    "sender_name" TEXT,
    "sender_name_normalized" TEXT,
    "destination_last4" CHAR(4),
    "destination_account_id" UUID,
    "customer_id" UUID,
    "is_free_income" BOOLEAN NOT NULL DEFAULT false,
    "suggested_charge_id" UUID,
    "match_score" DECIMAL(4,3),
    "match_signals" JSONB,
    "decided_by" UUID,
    "decided_at" TIMESTAMPTZ,
    "decision_reason" TEXT,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ NOT NULL,

    CONSTRAINT "receipts_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "extractions" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "receipt_id" UUID NOT NULL,
    "engine" TEXT NOT NULL,
    "parser" TEXT,
    "detected_bank_id" SMALLINT,
    "fields" JSONB NOT NULL,
    "min_confidence" DECIMAL(4,3),
    "raw" JSONB,
    "raw_deleted_at" TIMESTAMPTZ,
    "duration_ms" INTEGER,
    "success" BOOLEAN NOT NULL,
    "error" TEXT,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "extractions_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "allocations" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "receipt_id" UUID NOT NULL,
    "charge_id" UUID NOT NULL,
    "amount_applied" DECIMAL(14,2) NOT NULL,
    "created_by" UUID NOT NULL,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "voided_at" TIMESTAMPTZ,
    "voided_by" UUID,
    "void_reason" TEXT,

    CONSTRAINT "allocations_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "alerts" (
    "id" UUID NOT NULL DEFAULT gen_random_uuid(),
    "tenant_id" UUID NOT NULL,
    "receipt_id" UUID NOT NULL,
    "rule" "alert_rule_code" NOT NULL,
    "severity" "alert_severity" NOT NULL,
    "message" TEXT NOT NULL,
    "details" JSONB NOT NULL DEFAULT '{}',
    "status" "alert_status" NOT NULL DEFAULT 'OPEN',
    "resolution" "alert_resolution",
    "resolved_by" UUID,
    "resolved_at" TIMESTAMPTZ,
    "resolution_reason" TEXT,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "alerts_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "audit_events" (
    "id" BIGSERIAL NOT NULL,
    "tenant_id" UUID NOT NULL,
    "user_id" UUID,
    "actor" TEXT NOT NULL DEFAULT 'USER',
    "entity" TEXT NOT NULL,
    "entity_id" UUID,
    "action" TEXT NOT NULL,
    "before" JSONB,
    "after" JSONB,
    "reason" TEXT,
    "ip" INET,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "audit_events_pkey" PRIMARY KEY ("id")
);

-- CreateIndex
CREATE UNIQUE INDEX "banks_code_key" ON "banks"("code");

-- CreateIndex
CREATE UNIQUE INDEX "users_email_key" ON "users"("email");

-- CreateIndex
CREATE UNIQUE INDEX "sessions_token_hash_key" ON "sessions"("token_hash");

-- CreateIndex
CREATE UNIQUE INDEX "push_devices_provider_token_key" ON "push_devices"("provider", "token");

-- CreateIndex
CREATE UNIQUE INDEX "business_accounts_tenant_id_id_key" ON "business_accounts"("tenant_id", "id");

-- CreateIndex
CREATE UNIQUE INDEX "business_accounts_tenant_id_bank_id_last4_currency_key" ON "business_accounts"("tenant_id", "bank_id", "last4", "currency");

-- CreateIndex
CREATE UNIQUE INDEX "customers_tenant_id_id_key" ON "customers"("tenant_id", "id");

-- CreateIndex
CREATE UNIQUE INDEX "customer_aliases_tenant_id_alias_normalized_key" ON "customer_aliases"("tenant_id", "alias_normalized");

-- CreateIndex
CREATE UNIQUE INDEX "charges_tenant_id_id_key" ON "charges"("tenant_id", "id");

-- CreateIndex
CREATE UNIQUE INDEX "charges_tenant_id_code_key" ON "charges"("tenant_id", "code");

-- CreateIndex
CREATE UNIQUE INDEX "receipts_tenant_id_id_key" ON "receipts"("tenant_id", "id");

-- CreateIndex
CREATE UNIQUE INDEX "receipts_tenant_id_idempotency_key_key" ON "receipts"("tenant_id", "idempotency_key");

-- CreateIndex
CREATE UNIQUE INDEX "extractions_tenant_id_id_key" ON "extractions"("tenant_id", "id");

-- CreateIndex
CREATE UNIQUE INDEX "allocations_tenant_id_id_key" ON "allocations"("tenant_id", "id");

-- CreateIndex
CREATE UNIQUE INDEX "alerts_tenant_id_id_key" ON "alerts"("tenant_id", "id");

-- AddForeignKey
ALTER TABLE "sessions" ADD CONSTRAINT "sessions_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "tenant_settings" ADD CONSTRAINT "tenant_settings_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "memberships" ADD CONSTRAINT "memberships_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "memberships" ADD CONSTRAINT "memberships_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "users"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "push_devices" ADD CONSTRAINT "push_devices_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "push_devices" ADD CONSTRAINT "push_devices_tenant_id_user_id_fkey" FOREIGN KEY ("tenant_id", "user_id") REFERENCES "memberships"("tenant_id", "user_id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "business_accounts" ADD CONSTRAINT "business_accounts_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "business_accounts" ADD CONSTRAINT "business_accounts_bank_id_fkey" FOREIGN KEY ("bank_id") REFERENCES "banks"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "customers" ADD CONSTRAINT "customers_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "customer_aliases" ADD CONSTRAINT "customer_aliases_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "customer_aliases" ADD CONSTRAINT "customer_aliases_tenant_id_customer_id_fkey" FOREIGN KEY ("tenant_id", "customer_id") REFERENCES "customers"("tenant_id", "id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "charges" ADD CONSTRAINT "charges_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "charges" ADD CONSTRAINT "charges_tenant_id_customer_id_fkey" FOREIGN KEY ("tenant_id", "customer_id") REFERENCES "customers"("tenant_id", "id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "charges" ADD CONSTRAINT "charges_created_by_fkey" FOREIGN KEY ("created_by") REFERENCES "users"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "charges" ADD CONSTRAINT "charges_voided_by_fkey" FOREIGN KEY ("voided_by") REFERENCES "users"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "receipts" ADD CONSTRAINT "receipts_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "receipts" ADD CONSTRAINT "receipts_uploaded_by_fkey" FOREIGN KEY ("uploaded_by") REFERENCES "users"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "receipts" ADD CONSTRAINT "receipts_decided_by_fkey" FOREIGN KEY ("decided_by") REFERENCES "users"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "receipts" ADD CONSTRAINT "receipts_bank_id_fkey" FOREIGN KEY ("bank_id") REFERENCES "banks"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "receipts" ADD CONSTRAINT "receipts_tenant_id_destination_account_id_fkey" FOREIGN KEY ("tenant_id", "destination_account_id") REFERENCES "business_accounts"("tenant_id", "id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "receipts" ADD CONSTRAINT "receipts_tenant_id_customer_id_fkey" FOREIGN KEY ("tenant_id", "customer_id") REFERENCES "customers"("tenant_id", "id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "receipts" ADD CONSTRAINT "receipts_tenant_id_suggested_charge_id_fkey" FOREIGN KEY ("tenant_id", "suggested_charge_id") REFERENCES "charges"("tenant_id", "id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "extractions" ADD CONSTRAINT "extractions_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "extractions" ADD CONSTRAINT "extractions_tenant_id_receipt_id_fkey" FOREIGN KEY ("tenant_id", "receipt_id") REFERENCES "receipts"("tenant_id", "id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "extractions" ADD CONSTRAINT "extractions_detected_bank_id_fkey" FOREIGN KEY ("detected_bank_id") REFERENCES "banks"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "allocations" ADD CONSTRAINT "allocations_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "allocations" ADD CONSTRAINT "allocations_tenant_id_receipt_id_fkey" FOREIGN KEY ("tenant_id", "receipt_id") REFERENCES "receipts"("tenant_id", "id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "allocations" ADD CONSTRAINT "allocations_tenant_id_charge_id_fkey" FOREIGN KEY ("tenant_id", "charge_id") REFERENCES "charges"("tenant_id", "id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "allocations" ADD CONSTRAINT "allocations_created_by_fkey" FOREIGN KEY ("created_by") REFERENCES "users"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "allocations" ADD CONSTRAINT "allocations_voided_by_fkey" FOREIGN KEY ("voided_by") REFERENCES "users"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "alerts" ADD CONSTRAINT "alerts_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "alerts" ADD CONSTRAINT "alerts_tenant_id_receipt_id_fkey" FOREIGN KEY ("tenant_id", "receipt_id") REFERENCES "receipts"("tenant_id", "id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "alerts" ADD CONSTRAINT "alerts_resolved_by_fkey" FOREIGN KEY ("resolved_by") REFERENCES "users"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "audit_events" ADD CONSTRAINT "audit_events_tenant_id_fkey" FOREIGN KEY ("tenant_id") REFERENCES "tenants"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "audit_events" ADD CONSTRAINT "audit_events_user_id_fkey" FOREIGN KEY ("user_id") REFERENCES "users"("id") ON DELETE SET NULL ON UPDATE CASCADE;
