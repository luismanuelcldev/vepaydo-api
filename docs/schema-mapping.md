# Schema mapping: Spanish SQL (cuadra_schema.sql) -> English (Prisma + code)

Source: `docs/reference/schema/cuadra_schema.sql`. Translation per AGENTS.md section 4.

## 4.1 Domain terms

| Spanish            | English          |
| ------------------ | ---------------- |
| comprobante        | receipt          |
| cobro              | charge           |
| pago               | payment          |
| aplicación         | allocation       |
| cuenta del negocio | business_account |
| cliente            | customer         |
| verificador        | verifier         |
| dueño              | owner            |
| cuadre             | reconciliation   |
| oleada (parsers)   | parser_wave      |

## 4.2 Tables, views, functions, roles

| Reference (Spanish)                               | Target (English)                                                      |
| ------------------------------------------------- | --------------------------------------------------------------------- |
| bancos                                            | banks                                                                 |
| usuarios                                          | users                                                                 |
| sesiones                                          | sessions                                                              |
| tenants                                           | tenants                                                               |
| tenant_config                                     | tenant_settings                                                       |
| membresias                                        | memberships                                                           |
| dispositivos_push                                 | push_devices                                                          |
| cuentas_negocio                                   | business_accounts                                                     |
| clientes                                          | customers                                                             |
| clientes_alias                                    | customer_aliases                                                      |
| cobros                                            | charges                                                               |
| comprobantes                                      | receipts                                                              |
| extracciones                                      | extractions                                                           |
| aplicaciones                                      | allocations                                                           |
| alertas                                           | alerts                                                                |
| eventos_auditoria                                 | audit_events                                                          |
| v_cobros_saldo                                    | v_charge_balances                                                     |
| v_cuadre_diario                                   | v_daily_reconciliation                                                |
| app.crear_negocio                                 | app.create_tenant                                                     |
| app.tenant_actual / usuario_actual / rol_actual   | app.current_tenant_id / app.current_user_id / app.current_member_role |
| app.set_actualizado_en                            | app.set_updated_at                                                    |
| app.distancia_hamming                             | app.hamming_distance                                                  |
| roles cuadra_app / cuadra_mantenimiento           | vepaydo_app / vepaydo_maintenance (+ vepaydo_owner for migrations)    |
| settings app.tenant_id / app.usuario_id / app.rol | app.tenant_id / app.user_id / app.role                                |

`app.role` values: `dueno` -> `owner`, `verificador` -> `verifier`, `sistema` -> `system`, plus `support` for platform support sessions.

### 4.2.1 Objects with no counterpart in the reference (section 4.5)

| Target (English)                          | Purpose                                                                                     |
| ----------------------------------------- | ------------------------------------------------------------------------------------------- |
| `password_reset_tokens`                   | Password-recovery tokens; only the SHA-256 hash is stored. Global, never exposed (UC-AUTH-04/05) |
| `legal_acceptances`                       | Per-user legal acceptance per document version. Global and immutable (UC-AUTH-08)             |
| `ux_events`                               | First-party UX metrics; no personal or financial data. RLS by tenant (section 4.5 item 5)     |
| `support_grants`                          | Owner-granted, time-boxed read-only support access. RLS by tenant (section 4.5 item 8)        |
| `app.has_active_support_grant(uuid)`      | True only for an unrevoked, unexpired grant and `app.role() = 'support'`                      |
| `app.support_tenant_overview()`           | Tenant metadata only, no grant required                                                       |
| `app.support_list_audit_events(uuid, ...)`| Read-only audit events through a `SECURITY DEFINER` function                                  |
| `app.support_list_receipts(uuid, ...)`    | Read-only receipt metadata, never `storage_key` or images                                      |
| `app.support_list_alerts(uuid, ...)`      | Read-only alerts                                                                              |

## 4.3 Key columns

| Spanish                                                                                             | English                                                                                          |
| --------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| monto                                                                                               | amount                                                                                           |
| moneda                                                                                              | currency                                                                                         |
| fecha_pago                                                                                          | payment_date                                                                                     |
| hora_pago                                                                                           | payment_time                                                                                     |
| referencia                                                                                          | reference (reference_normalized)                                                                 |
| remitente                                                                                           | sender_name (sender_name_normalized)                                                             |
| cuenta_destino_ultimos4                                                                             | destination_last4                                                                                |
| cuenta_destino_id                                                                                   | destination_account_id                                                                           |
| ultimos4                                                                                            | last4                                                                                            |
| subido_por                                                                                          | uploaded_by                                                                                      |
| decidido_por / decidido_en                                                                          | decided_by / decided_at                                                                          |
| motivo_*                                                                                            | *_reason                                                                                         |
| riesgo                                                                                              | risk                                                                                             |
| imagen_eliminada_en                                                                                 | image_deleted_at                                                                                 |
| es_ingreso_libre                                                                                    | is_free_income                                                                                   |
| codigo                                                                                              | code                                                                                             |
| concepto                                                                                            | description                                                                                      |
| fecha_emision / fecha_vencimiento                                                                   | issued_on / due_on                                                                               |
| creado_en / actualizado_en                                                                          | created_at / updated_at                                                                          |
| anulado_en / anulado_por / motivo_anulacion                                                         | voided_at / voided_by / void_reason                                                              |
| intentos / error_procesamiento                                                                      | attempts / processing_error                                                                      |
| confianza_min / crudo / crudo_eliminado_en / duracion_ms / exitosa                                  | min_confidence / raw / raw_deleted_at / duration_ms / success                                    |
| monto_aplicado / creada_por / anulada_en / anulada_por                                              | amount_applied / created_by / voided_at / voided_by                                              |
| regla / severidad / mensaje / detalle / resolucion / resuelta_por / resuelta_en / motivo_resolucion | rule / severity / message / details / resolution / resolved_by / resolved_at / resolution_reason |
| ante(s) / despues / accion / entidad / usuario_id / actor                                           | before / after / action / entity / user_id / actor                                               |
| caducidad (expira_en)                                                                               | expires_at (sessions)                                                                            |
| token_hash / user_agent                                                                             | token_hash / user_agent                                                                          |
| proveedor / token / ultimo_uso                                                                      | provider / token / last_used                                                                     |

## 4.4 Enums (values in UPPER_SNAKE_CASE)

| Enum                 | Values                                                                                                                                                                                                    |
| -------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| MemberRole           | OWNER, VERIFIER                                                                                                                                                                                           |
| ProcessingStatus     | RECEIVED, PROCESSING, EXTRACTED, FAILED                                                                                                                                                                   |
| ReconciliationStatus | PENDING_REVIEW, CONFIRMED, PARTIAL, OVERPAID, DUPLICATE, SUSPICIOUS, UNMATCHED, REJECTED                                                                                                                  |
| AlertSeverity        | INFO, WARNING, CRITICAL                                                                                                                                                                                   |
| AlertStatus          | OPEN, RESOLVED                                                                                                                                                                                            |
| AlertResolution      | DISMISSED, CONFIRMED_ANYWAY, REJECTED, AUTO_CLEARED                                                                                                                                                       |
| ChargeStatus         | OPEN, PARTIAL, PAID, VOIDED                                                                                                                                                                               |
| ReceiptSource        | WEB_UPLOAD, CAMERA, SHARE_TARGET                                                                                                                                                                          |
| TransferType         | SAME_BANK, INTERBANK, UNKNOWN                                                                                                                                                                             |
| TenantPlan           | PILOT, ESSENTIAL, BUSINESS                                                                                                                                                                                |
| AlertRuleCode        | DUPLICATE_REFERENCE, IDENTICAL_IMAGE, SIMILAR_IMAGE, FOREIGN_DESTINATION_ACCOUNT, FUTURE_DATE, DATE_BEFORE_CHARGE, STALE_DATE, INVALID_REFERENCE_FORMAT, AMOUNT_MISMATCH, SENDER_MISMATCH, LOW_CONFIDENCE |
| FailureReason        | NOT_A_RECEIPT, UNREADABLE, OCR_ERROR, TIMEOUT                                                                                                                                                             |
| RejectReasonCode     | WRONG_AMOUNT, DUPLICATE, NOT_THIS_BUSINESS, NOT_A_RECEIPT, UNREADABLE, OTHER                                                                                                                              |
| LegalDocumentType    | TERMS, PRIVACY                                                                                                                                                                                            |
| AuditActor           | USER, SYSTEM, SUPPORT                                                                                                                                                                                     |

### 4.4.1 Enum value mapping (reference Spanish -> target English)

| Reference value                                                                                                                                                                                                           | Target value                                                                                                                                                                                                        |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| dueno / verificador                                                                                                                                                                                                       | OWNER / VERIFIER                                                                                                                                                                                                    |
| recibido / procesando / extraido / fallido                                                                                                                                                                                | RECEIVED / PROCESSING / EXTRACTED / FAILED                                                                                                                                                                          |
| pendiente_revision / confirmado / parcial / excedente / duplicado / sospechoso / sin_match / rechazado                                                                                                                    | PENDING_REVIEW / CONFIRMED / PARTIAL / OVERPAID / DUPLICATE / SUSPICIOUS / UNMATCHED / REJECTED                                                                                                                     |
| informativa / advertencia / critica                                                                                                                                                                                       | INFO / WARNING / CRITICAL                                                                                                                                                                                           |
| abierta / resuelta                                                                                                                                                                                                        | OPEN / RESOLVED                                                                                                                                                                                                     |
| descartada / confirmada_igual / rechazada                                                                                                                                                                                 | DISMISSED / CONFIRMED_ANYWAY / REJECTED (+ AUTO_CLEARED)                                                                                                                                                            |
| abierto / parcial / pagado / anulado                                                                                                                                                                                      | OPEN / PARTIAL / PAID / VOIDED                                                                                                                                                                                      |
| carga_web / camara / share_target                                                                                                                                                                                         | WEB_UPLOAD / CAMERA / SHARE_TARGET                                                                                                                                                                                  |
| mismo_banco / interbancaria / desconocida                                                                                                                                                                                 | SAME_BANK / INTERBANK / UNKNOWN                                                                                                                                                                                     |
| piloto / esencial / negocio                                                                                                                                                                                               | PILOT / ESSENTIAL / BUSINESS                                                                                                                                                                                        |
| referencia_duplicada / imagen_identica / imagen_similar / cuenta_destino_ajena / fecha_futura / fecha_anterior_cobro / fecha_antigua / referencia_formato_invalido / monto_distinto / remitente_distinto / confianza_baja | DUPLICATE_REFERENCE / IDENTICAL_IMAGE / SIMILAR_IMAGE / FOREIGN_DESTINATION_ACCOUNT / FUTURE_DATE / DATE_BEFORE_CHARGE / STALE_DATE / INVALID_REFERENCE_FORMAT / AMOUNT_MISMATCH / SENDER_MISMATCH / LOW_CONFIDENCE |
| activo / suspendido / cancelado (`tenants.estado`)                                                                                                                                                                        | kept as text: `active` / `suspended` / `cancelled`                                                                                                                                                                  |

## 4.5 Required schema changes versus the reference

1. `receipts.suggested_charge_id` (nullable, composite FK to `charges`), `receipts.match_score numeric(4,3)`, `receipts.match_signals jsonb`.
2. `users.preferred_locale text NOT NULL DEFAULT 'es-DO'` and `users.must_change_password boolean NOT NULL DEFAULT false`.
3. `tenants.plan` uses `PILOT` / `ESSENTIAL` / `BUSINESS`.
4. `AlertResolution.AUTO_CLEARED`; `resolved_by` may be NULL only when resolution is `AUTO_CLEARED`; the alert UPDATE policy allows the `system` role.
5. `ux_events` table (RLS by tenant, 12-month retention).
6. `password_reset_tokens` table (global, no RLS, never exposed through the API).
7. `legal_acceptances` table (global, immutable: no UPDATE or DELETE grants).
8. Support access: `users.is_platform_support`, `support_grants` (RLS by tenant), `app.has_active_support_grant`, and the `SECURITY DEFINER` read-only functions `app.support_tenant_overview`, `app.support_list_audit_events`, `app.support_list_receipts`, `app.support_list_alerts` (never `storage_key` or images).
9. `receipts.failure_reason` (allowed only when `processing_status = FAILED`) and `receipts.reject_reason_code` (required when the status is `REJECTED`).
10. `sessions.device_label`, `sessions.ip`, `sessions.last_seen_at`; `audit_events.actor` uses the `AuditActor` enum (`USER`, `SYSTEM`, `SUPPORT`).
11. Kept from the reference: composite `(tenant_id, id)` foreign keys, RLS with `FORCE`, the immutable `audit_events` trigger, the allocation-validation trigger, the verifier-rules trigger, `security_invoker` views, and the non-unique duplicate-reference index (owner may "confirm anyway").
