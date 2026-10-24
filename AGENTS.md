# VepayDO — Engineering Master Prompt (ASP.NET Core backend)

**Audience:** an autonomous coding agent acting as a Senior Staff Software Engineer.
**Author's intent:** build the VepayDO MVP backend end to end, production-grade, without cutting corners on security, tenant isolation, or testability.

This repository is the **ASP.NET Core (.NET 10)** implementation of VepayDO. It was migrated from an earlier NestJS + Prisma monorepo, which is preserved as the behavioural reference (use cases, tests, SQL schema, product/UX docs). Every product rule, security rule, and scope decision from the original master prompt still applies; only the **implementation stack** changed.

> This file is loaded as persistent context. Work is executed in phases (`docs/prompts/PHASES.md`); never build everything in one pass. Reply in the language the user writes in (Spanish by default); all code, comments, commits, DB objects, and `docs/` content stay in **English** (except Spanish user-facing copy).

---

## Document map (read from disk; never ask the user to paste)

| What | Where |
|---|---|
| This master prompt | `AGENTS.md` (this file) |
| Phase execution prompts (Phases 0 to 8) | `docs/prompts/PHASES.md` |
| Reference SQL schema (Spanish identifiers; translate per section 4) | `docs/reference/schema/cuadra_schema.sql` |
| Product and architecture document | `docs/reference/product/PRODUCT_DOCUMENT.md` |
| Use case catalogue (**scope, IDs, acceptance tests**) | `docs/product/USE_CASES.md` |
| UX/visual spec (**authoritative for UI, tokens, Spanish copy**) | `docs/design/UX_SPEC.md` |
| Legal texts (product-owner supplied; **never authored by the agent**) | `docs/legal/` |
| Architecture decision records | `docs/adr/` |
| Runbooks | `docs/runbooks/` |
| Spanish→English schema mapping (target names) | `docs/schema-mapping.md` |
| Original PostgreSQL migrations (Spanish identifiers; to be translated) | `db/reference-migrations-es/` |
| Target PostgreSQL migrations (English identifiers) | `db/migrations/` |
| Reference NestJS implementation (behavioural source of truth) | `C:\Mis proyectos\VePayDO` (separate repo) |

**Session protocol:** read this file, the matching section of `docs/prompts/PHASES.md`, the use cases for the phase, and any reference docs the phase needs. Execute **only that phase**. Finish with the report format in section 17 and stop. Check prior phases' acceptance criteria before starting; if unmet, say exactly what is missing and stop.

---

## 1. Role and operating contract

You write boring, explicit code. Optimize for correctness, security, readability, operability, in that order. Non-negotiable:

1. **Plan before coding.** Short plan (files, risks, test strategy), then implement.
2. **No placeholders.** Any `TODO` needs a `docs/BACKLOG.md` entry. Missing credentials/fixtures ⇒ implement the seam, document in `docs/ASSUMPTIONS.md`, say so in the report.
3. **Do not invent bank facts.** No fabricated bank layouts, labels, or reference formats. Generic parser + per-bank config filled from real fixtures later.
4. **Stay in MVP scope** (`docs/product/USE_CASES.md`, status `MVP`). Cite use case IDs (`UC-RCP-07`) in tests/PRs/ADRs. Do not add features.
5. **Ask only for real blockers**; otherwise choose the most conservative option, record it in `docs/ASSUMPTIONS.md`, continue.
6. **Deviations require an ADR** (`docs/adr/NNNN-title.md`).
7. **Small commits**, Conventional Commits in English (`feat(receipts): add idempotent upload endpoint`).
8. **Every feature ships with tests.** A phase is done only when build, analyzers, and tests pass.
9. **Never weaken security to make a test pass** (never disable RLS, never use a superuser connection at runtime, never build SQL with interpolated input).
10. **Report honestly.** Say what you did not verify.

---

## 2. Product context

VepayDO is a multi-tenant PWA that verifies and reconciles bank-transfer payment receipts for businesses in the Dominican Republic. A customer sends a screenshot or PDF of a transfer receipt; the system extracts amount, date, reference, bank, and destination account; matches it against expected charges; raises alerts for duplicates and suspicious signals; and produces a daily reconciliation report.

**Guiding principle:** the system processes everything; the human only decides exceptions.
**Positioning rule:** VepayDO **alerts, it does not certify**. Copy/API/docs must never claim to prove a payment. Bank-statement verification is a future feature.
**Users:** `OWNER` (configures, supervises, resolves critical alerts) and `VERIFIER` (uploads, reviews, confirms within limits).
**Pilot reality:** 3–5 businesses, low volume, mostly Android, unreliable connectivity, one operator. Design for simplicity of operation.

---

## 3. Stack (this repository)

| Layer | Choice |
|---|---|
| Runtime | .NET 10 (LTS), C# `latest`, nullable enabled, `TreatWarningsAsErrors` |
| Web | ASP.NET Core (Minimal APIs / MVC controllers), OpenAPI in non-prod |
| ORM | EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL` |
| Database | PostgreSQL 16 with Row Level Security, `pg_trgm`, `citext` |
| Validation | FluentValidation |
| Queue | `BackgroundService` + Redis (BullMQ equivalent), or Hangfire (decide per phase, ADR) |
| OCR service | Python 3.12 + FastAPI + PaddleOCR (CPU) — separate service, unchanged |
| Object storage | S3-compatible (R2/B2 prod, MinIO dev) via AWS SDK for .NET |
| Images | ImageSharp (EXIF strip, resize), SHA-256 on upload |
| Exports | ClosedXML (XLSX), QuestPDF (PDF), CSV |
| Auth | JWT access token (15 min) + rotating refresh token in httpOnly cookie; argon2id |
| Logging/errors | Serilog (structured, redacting); Sentry optional |
| Tests | xUnit + FluentAssertions + Testcontainers (PostgreSQL, Redis) + NetArchTest; Playwright for e2e (web) |
| Infra | Docker Compose on one VPS, Caddy, Cloudflare, GitHub Actions |

Verify exact current package APIs against official docs; do not rely on memory. Record material changes in an ADR.

---

## 4. Language rule and naming

- **All code is English**: identifiers, comments, commit messages, branch names, **DB tables/columns/enums**, API routes/fields, error codes, logs, tests. `docs/` in English (except explicitly user-facing Spanish).
- UI copy is **Spanish (es-DO)** by default (English secondary), always via i18n keys, never hard-coded.
- API errors expose a stable English `code` and an `i18nKey`; the frontend translates.
- **DB object names are English**, exactly per `docs/schema-mapping.md` (e.g. `sessions`, `memberships`, `business_accounts`, `customers`, `charges`, `receipts`, `extractions`, `allocations`, `alerts`, `audit_events`, `tenant_settings`, `push_devices`; columns `name`, `created_at`, `updated_at`, `amount`, `currency`, `payment_date`, `reference`/`reference_normalized`, `sender_name`/`sender_name_normalized`, `destination_last4`, `uploaded_by`, `decided_by`/`decided_at`, `*_reason`, `risk`, `image_deleted_at`, `is_free_income`, `code`, `description`, `issued_on`/`due_on`). The brand name lives in one config constant (`BRAND_NAME`), never hard-coded.

### 4.1 Enums (values `UPPER_SNAKE_CASE`)
`MemberRole` (OWNER, VERIFIER); `ProcessingStatus` (RECEIVED, PROCESSING, EXTRACTED, FAILED); `ReconciliationStatus` (PENDING_REVIEW, CONFIRMED, PARTIAL, OVERPAID, DUPLICATE, SUSPICIOUS, UNMATCHED, REJECTED); `AlertSeverity` (INFO, WARNING, CRITICAL); `AlertStatus` (OPEN, RESOLVED); `AlertResolution` (DISMISSED, CONFIRMED_ANYWAY, REJECTED, AUTO_CLEARED); `ChargeStatus` (OPEN, PARTIAL, PAID, VOIDED); `ReceiptSource` (WEB_UPLOAD, CAMERA, SHARE_TARGET); `TransferType` (SAME_BANK, INTERBANK, UNKNOWN); `TenantPlan` (PILOT, ESSENTIAL, BUSINESS); `AlertRuleCode` (DUPLICATE_REFERENCE, IDENTICAL_IMAGE, SIMILAR_IMAGE, FOREIGN_DESTINATION_ACCOUNT, FUTURE_DATE, DATE_BEFORE_CHARGE, STALE_DATE, INVALID_REFERENCE_FORMAT, AMOUNT_MISMATCH, SENDER_MISMATCH, LOW_CONFIDENCE); `FailureReason` (NOT_A_RECEIPT, UNREADABLE, OCR_ERROR, TIMEOUT); `RejectReasonCode` (WRONG_AMOUNT, DUPLICATE, NOT_THIS_BUSINESS, NOT_A_RECEIPT, UNREADABLE, OTHER); `LegalDocumentType` (TERMS, PRIVACY); `AuditActor` (USER, SYSTEM, SUPPORT).

### 4.2 Schema requirements (vs the reference SQL)
Composite `(tenant_id, id)` foreign keys; RLS with `FORCE ROW LEVEL SECURITY`; immutable `audit_events` (no UPDATE/DELETE for the app role); allocation-validation trigger; verifier-rules trigger; `security_invoker` views; non-unique duplicate-reference index; `AlertResolution.AUTO_CLEARED` (relax `resolved_by`); `receipts.suggested_charge_id`/`match_score`/`match_signals`; `users.preferred_locale`/`must_change_password`/`is_platform_support`; `tenants.plan` (PILOT/ESSENTIAL/BUSINESS); `ux_events`, `password_reset_tokens`, `legal_acceptances`, `support_grants`; support functions (`app.has_active_support_grant`, `app.support_tenant_overview`, `app.support_list_audit_events`, `app.support_list_receipts`, `app.support_list_alerts`); `receipts.failure_reason`/`reject_reason_code`; `sessions.device_label`/`ip`/`last_seen_at`. See `docs/schema-mapping.md` §4.5 for the full list.

---

## 5. Repository layout

```
VePayDO/
├─ src/
│  ├─ VePayDO.Api/            # HTTP entrypoint (composition root)
│  ├─ VePayDO.Worker/         # queue worker entrypoint (added in Phase 3+)
│  ├─ VePayDO.Domain/         # entities, value objects, domain services, domain errors (no I/O)
│  ├─ VePayDO.Application/    # use cases (commands/queries), ports (interfaces)
│  ├─ VePayDO.Infrastructure/ # EF Core, S3, OCR client, SMTP, FCM, queues (implements ports)
│  └─ VePayDO.Contracts/      # DTOs / enums shared with clients
├─ tests/
│  ├─ VepayDO.Domain.Tests/
│  ├─ VepayDO.Application.Tests/
│  ├─ VepayDO.Infrastructure.Tests/   # Testcontainers (PostgreSQL, Redis)
│  ├─ VepayDO.Api.Tests/              # WebApplicationFactory
│  └─ VepayDO.Architecture.Tests/     # NetArchTest dependency rules
├─ services/ocr/             # FastAPI + PaddleOCR (Python)
├─ db/
│  ├─ migrations/            # target PostgreSQL migrations (English identifiers)
│  └─ reference-migrations-es/  # original migrations (Spanish), translation source
├─ infra/                    # docker-compose, Caddyfile, backup scripts
├─ docs/                     # ARCHITECTURE, PATTERNS, ASSUMPTIONS, BACKLOG, adr/, runbooks/, references
└─ .github/workflows/
```

---

## 6. Architecture and layering

Modular monolith: one solution, process entrypoints `Api` and `Worker`, plus the separate OCR service.

**Modules:** `auth`, `tenants`, `memberships`, `business-accounts`, `customers`, `charges`, `receipts`, `extraction`, `matching`, `alerts`, `allocations`, `audit`, `reconciliation`, `notifications`, `retention`, `legal`, `support`, `health`.

**Layering (dependency rule: inward only):**
- `Domain`: entities, value objects, domain services, domain events, domain errors. **No EF Core, no ASP.NET, no I/O.**
- `Application`: use cases and ports; depends on `Domain` only.
- `Infrastructure`: EF Core, S3, OCR client, SMTP, FCM, queue processors; implements ports.
- `Presentation` (inside `Api`): controllers/minimal APIs, validated DTOs, filters, mappers.

Enforce with `VepayDO.Architecture.Tests` (NetArchTest) in CI. Modules are folders/namespaces; the EF model and the application service of another module are not referenced directly — go through public interfaces or domain events.

**Pipeline (worker):** upload → store original + SHA-256 → `Receipt(RECEIVED)` → enqueue → PROCESSING → OCR → parse (Factory Method) → build `ParsedReceipt` (Builder) → persist Extraction → EXTRACTED → MatchingEngine → AlertEvaluator → set ReconciliationStatus → notify if needed. Jobs idempotent (`jobId = receiptId`), retried 3× with backoff, then FAILED. The worker sets tenant context per job exactly like HTTP.

---

## 7. EF Core and PostgreSQL RLS strategy

EF Core maps tables/columns/enums/relations (including composite `(tenant_id, id)` keys). Hand-written SQL migrations own extensions, RLS policies, `FORCE ROW LEVEL SECURITY`, CHECK constraints, partial/covering/trigram indexes, functions, triggers, views, roles and grants, and the `banks` seed.

- **Migrations**: apply the SQL in `db/migrations/` with a small idempotent runner over a migration-history table (do **not** rely on `dotnet ef` to generate RLS/triggers). Each migration runs **once**, in filename order, inside a transaction.
- **Column mapping**: money `numeric(14,2)` ↔ `decimal`; dates `date`; time `time`; sha256 `bytea(32)`; phash `bigint`; email `citext`; ids `uuid` `gen_random_uuid()`.
- **Roles/connections**: `vepaydo_owner` (migrations only, never runtime), `vepaydo_app` (API+worker, `NOSUPERUSER NOBYPASSRLS`, no DELETE on business tables, INSERT/SELECT only on `audit_events`), `vepaydo_maintenance` (`BYPASSRLS`, retention only).

**Tenant context per transaction (fail closed):**
1. An ambient `TenantContext` (`AsyncLocal<...>`) set by interceptor/middleware (HTTP) and job wrapper (worker).
2. `TenantTransactionRunner.Run(ctx, fn)` opens an EF transaction and runs `SELECT set_config('app.tenant_id', $1, true), set_config('app.user_id', $2, true), set_config('app.role', $3, true)` with **parameterized** SQL, then `fn(tx)`.
3. Repositories obtain the DbContext/transaction from a provider that **throws `MissingTenantContextException` if no transaction is present**. They never use the root context for tenant tables.
4. Pre-auth flows use `RunAsUser(userId)` (sets `app.user_id` only). 5. Tenant creation calls `SELECT app.create_tenant($1, $2)`. 6. One request = one transaction by default; long exports read inside a transaction and write files outside.

**Required RLS tests** (Testcontainers PostgreSQL): tenant A cannot read/write tenant B in every tenant table; composite FKs reject cross-tenant references; a connection without context sees zero rows; verifier cannot revert a final status, exceed the cap, or confirm with an open critical alert; `audit_events` rejects UPDATE/DELETE for `vepaydo_app`; allocations cannot exceed the receipt amount or mix currencies; only `create_tenant` creates tenants; a support session reads zero rows directly and only through the support functions with an active grant, losing access on expiry/revocation, and can never write.

---

## 8. Design patterns (mandatory where they remove real variation)

Document every usage in `docs/PATTERNS.md` (problem, participants→class names, why, proving test).

- **Factory Method**: `IReceiptParser` + abstract `ReceiptParserCreator.createParser()` with a shared `Extract` template; concrete creators per bank + generic fallback; selection via a registry keyed by `BankCode` (register through DI so adding a bank touches no existing code).
- **Abstract Factory**: extraction toolkit (`ITextRecognizer`, `IConfidenceNormalizer`, `IRawPayloadSerializer`) with PaddleOCR / Vision-stub / Fake families; storage toolkit (`IObjectStorage`, `ISignedUrlService`, `IStorageKeyGenerator`) with S3 / InMemory families.
- **Builder**: `ParsedReceiptBuilder`, `AlertBuilder`, `AuditEventBuilder`; report builders (`Xlsx`/`Csv`/`Pdf`) driven by a `ReconciliationReportDirector`; test data builders.
- **Prototype**: `Cloneable<T>`; `ChargeDraft.Clone()` (duplicate/bulk), `TenantSettings.DEFAULTS.FromPrototype()`, `ReceiptSnapshot.Clone()` for audit diffs. Tests assert deep independence (including `decimal` and nested objects).
- **Singleton** only via DI (default lifetime): `AppDbContext` scoped, `AppConfig` (validated, frozen), `RedisConnection`, `Clock`. No `static GetInstance()` or mutable globals. Python OCR engine: lazy thread-safe singleton (`OcrEngineHolder`).

Non-creational patterns allowed where natural (Strategy for matching/alert rules, State for status transitions, Adapter for external services, Chain of Responsibility for upload validation).

---

## 9. Business rules

### 9.1 Receipt lifecycle
Processing axis `RECEIVED → PROCESSING → EXTRACTED | FAILED` (`FAILED → PROCESSING` retry, `FAILED → EXTRACTED` manual entry). Reconciliation axis set by the system: `DUPLICATE` (duplicate reference or identical image), `SUSPICIOUS` (other critical alert), `UNMATCHED` (no candidate), else `PENDING_REVIEW` with a suggested match. Human: `confirm` → `CONFIRMED|PARTIAL|OVERPAID`; `reject` (reason required) → `REJECTED`; `revert` (OWNER only, reason required) → `PENDING_REVIEW`, voiding active allocations. Final statuses are never deleted; reversals are recorded. A receipt with an open CRITICAL alert can be confirmed only by an OWNER ("confirm anyway" → `CONFIRMED_ANYWAY` + reason). `FAILED` carries `failure_reason` (NOT_A_RECEIPT, UNREADABLE, OCR_ERROR, TIMEOUT); thresholds live in one tunable constants file; borderline prefers UNREADABLE; the image is never lost.

### 9.2 Confirmation and allocation
Given receipt amount `a`, tolerance `t` (tenant setting, default 0), selected charges in user order with remaining balances `ri`: none selected ⇒ CONFIRMED + `is_free_income`; allocate `min(remaining, ri)` in order; `|a − Σallocated| ≤ t` and all covered ⇒ CONFIRMED; some selected charge uncovered beyond `t` ⇒ PARTIAL; `a` exceeds total remaining beyond `t` ⇒ OVERPAID (note the excess); update charges (balance within `t` ⇒ PAID, else PARTIAL if any allocation); currencies must match (domain + trigger); all in one transaction under a row lock on the receipt.

### 9.3 Matching engine (Strategy per signal)
Candidates: tenant charges `OPEN|PARTIAL`, same currency. Signals/weights: `CHARGE_CODE` 0.40, `AMOUNT` 0.30, `SENDER` 0.20 (trigram ≥ 0.6), `DATE` 0.10 (window, linear decay to 2×). Strong match: top ≥ 0.60 and margin ≥ 0.15 over second. Candidates list: ≥ 0.25, up to 5. Persist `suggested_charge_id`, `match_score`, `match_signals`. Pure function of `(receipt, candidates, settings, clock)`. Aliases feed SENDER; no `SENDER_MISMATCH` when the sender matches an alias; aliases created only on opt-in at confirmation; normalized alias unique per tenant; moving an alias is OWNER-only.

### 9.4 Alert rules (`AlertEvaluator`, each rule a Strategy)
`DUPLICATE_REFERENCE` (CRITICAL), `IDENTICAL_IMAGE` (CRITICAL), `FOREIGN_DESTINATION_ACCOUNT` (CRITICAL), `SIMILAR_IMAGE` (WARNING, Hamming ≤ 6 within 90 days), `FUTURE_DATE` (WARNING), `DATE_BEFORE_CHARGE` (WARNING), `STALE_DATE` (WARNING, > 30 days), `INVALID_REFERENCE_FORMAT` (WARNING), `AMOUNT_MISMATCH` (INFO), `SENDER_MISMATCH` (INFO), `LOW_CONFIDENCE` (INFO, amount/reference confidence < 0.80). Receipt `risk` = max severity among open alerts. Idempotent upsert by `(tenant_id, receipt_id, rule)` while open; re-evaluate after edits; close obsolete with `AUTO_CLEARED`. Messages are alerts, never verdicts.

### 9.5 Permissions matrix
Upload/review receipts: OWNER+VERIFIER. Confirm/reject: OWNER; VERIFIER up to `verifier_amount_cap` and only without open critical alerts. Resolve CRITICAL/revert: OWNER only. Create/edit charges: both. Business accounts/members/settings: OWNER. Reconciliation: OWNER full, VERIFIER current day. Audit log: OWNER read. Sessions: both (own); revoke others: OWNER. Support grants: OWNER. Enforced in guards **and** by DB rules.

### 9.6 Tenant settings defaults
`dateWindowDays=7`, `amountTolerance=0.00`, `verifierAmountCap=null`, `imageRetentionMonths=24`. OCR confidence threshold is internal, not a setting.

### 9.7 Reconciliation (MVP)
Computed view by **payment date** by status/bank/destination account, plus open charges. No day-closing (future). Keep `payment_date` and audit so snapshots can be added later without migration pain.

### 9.8 Audit
Every state change, field correction (before/after), alert resolution, allocation, revert, export, and image access writes an `audit_events` row in the same transaction. Corrections keep the original OCR value in `extractions`.

### 9.9 Account security, legal, support
- **Password recovery** (UC-AUTH-04/05): 256-bit tokens, only SHA-256 stored, single use, 30 min (15 operator-issued), neutral responses, rate limits per email and per IP, all sessions revoked on use, "password changed" notice. `IMailSender` port with SMTP impl, `FakeMailSender` for tests, Mailpit in dev. Templates from i18n (es-DO).
- **Sessions** (UC-AUTH-06/07): access JWT carries `sid`; guard rejects tokens whose session is in a Redis revocation set (TTL = access lifetime); deactivating a member revokes all their sessions.
- **Legal acceptance** (UC-AUTH-08): `legal_acceptances` per user per version; while pending, every endpoint except `GET /legal/documents`, `POST /legal/accept`, `GET /auth/me`, logout returns `LEGAL_ACCEPTANCE_REQUIRED`. Documents from `docs/legal/terms.es.md` and `docs/legal/privacy.es.md`, supplied by the product owner. **Never write legal text**: files carry `[PENDING LEGAL REVIEW]` and non-prod builds show a warning while the marker is present.
- **Support access** (UC-SET-06, UC-OPS-01/02): support users have `is_platform_support` and role value `support`; created only by seed/operator script. Without a grant: tenant metadata only via `app.support_tenant_overview()`. With an Owner-granted, time-boxed grant (1/24/72 h): read audit events, receipt metadata, alerts via the support functions, read-only, never images. Every support action audited with actor `SUPPORT`, visible to the Owner.

---

## 10. API surface (`/api/v1`, JSON, tenant from the JWT)

RFC 9457 problem-details errors with `code`, `i18nKey`, `traceId`; cursor pagination (`limit`, `cursor`); `Idempotency-Key` required on receipt upload; rate limiting on auth/upload; OpenAPI served in non-prod.

- **Auth:** register, login, select-tenant, refresh, logout, me, change-password, PATCH me (locale), forgot-password, reset-password, sessions list/revoke/revoke-all.
- **Members (OWNER):** list/create (temporary password that must change at first login), patch (role/active), revoke-sessions.
- **Business accounts / Customers / Charges / Receipts / Alerts / Reconciliation / Settings / Notifications / Usage / Audit / Legal / Support grants / Support console / Health:** see `docs/product/USE_CASES.md` and the original master prompt §10 for the full route list.

Upload validation: magic-byte sniffing (do not trust `Content-Type`), JPEG/PNG/WebP/PDF, size caps, re-encode to strip EXIF, reject absurd dimensions, SHA-256 on stored bytes, key `{tenantId}/{yyyy}/{mm}/{uuid}`.

---

## 11. Extraction pipeline and OCR service

OCR service (`services/ocr`): `POST /extract` (multipart) → `{ lines:[{text,box,confidence}], phash, width, height, engine, durationMs }`; health endpoints; PaddleOCR loaded once (warm, semaphore-limited), request timeout, pixel/byte limits, PDF first page only. Stateless, persists nothing, **never logs bytes or recognized text**. pytest with synthetic Pillow images (no bank branding). Parsing: generic Spanish parser (amounts DOP/USD, dates, reference labels, destination last4, names), bank detection by keyword scoring, per-bank config files marked "needs real fixtures". Golden tests run against recorded OCR JSON. Vision-model fallback: interface + stub only, feature-flagged.

---

## 12. Frontend/PWA

`docs/design/UX_SPEC.md` is authoritative for UI. The web app (Vite + React PWA) is a separate concern; this backend must satisfy its contracts (routes, i18n keys, problem-details, SSE-less polling). Keep response shapes stable.

---

## 13. Security, privacy, retention

argon2id; JWT 15 min + refresh rotation with reuse detection (revoke family on reuse), `httpOnly`/`Secure`/`SameSite=Lax` cookie, CSRF on refresh; HTTPS/HSTS/helmet-equivalent headers/CORS allowlist/request size limits; guards + RLS (RLS is the last line); private bucket, signed URLs ≤ 5 min, tenant-prefixed keys, EXIF stripped; structured logs with request/tenant id and **redaction** of sender names, account digits, references, tokens (never image content or OCR text); secrets in env only, validated at boot, `.env.example` committed; lockfiles + audit in CI; do not adopt package versions published within 14 days; only last-4 of accounts stored. **Retention defaults (pending legal review):** images 24 mo (configurable ≤ 120), raw OCR JSON 12 mo, extracted data while active, audit + 12 mo, logs 90 days, backups 30-day rotation, cancelled tenant: 30 days to export / 90 days hard delete. Nightly job 03:00 America/Santo_Domingo using the maintenance role purges expired images (set `image_deleted_at`, keep hash) and raw OCR JSON, logs every purge. Never train on customer images. Flag in `docs/ASSUMPTIONS.md`: Ley 172-13 review, DGII formalization, "alerts, does not certify" language.

---

## 14. Testing and quality gates

| Level | Tooling | Coverage |
|---|---|---|
| Unit | xUnit + FluentAssertions | Domain: Money, allocation, matching, alert evaluator (every rule, boundaries), transitions, normalizers, patterns (identity, independence). Domain line coverage ≥ 90% |
| Integration | xUnit + Testcontainers (PostgreSQL, Redis) | Repositories, RLS suite (§7), triggers, pipeline to EXTRACTED with a Fake toolkit, idempotent upload, retention job |
| Contract | pytest + xUnit | OCR response parsed by the API client |
| Parser golden | xUnit | Recorded OCR JSON → expected JSON per bank |
| E2E | Playwright | Onboarding; upload→review→confirm; critical alert path; share-target queue offline→online; export download |
| Architecture | NetArchTest | Dependency rule (§6), no `Npgsql`/EF in Domain/Application |
| Static | `dotnet build` (`TreatWarningsAsErrors`), analyzers, `dotnet format --verify-no-changes`, `ruff`/`mypy` for Python | Zero warnings |

Overall line coverage ≥ 80%. CI blocks merges on any failure. UX gates per `docs/design/UX_SPEC.md`.

---

## 15. Infrastructure and CI/CD

`infra/docker-compose.yml` (prod) and `docker-compose.dev.yml`: `caddy`, `api`, `worker`, `ocr`, `postgres`, `redis`, `web`, plus `minio`/`mailpit` in dev. Health checks, restart policies, resource limits, named volumes, non-root users, read-only root filesystems where possible, private network (only Caddy publishes). Multi-stage Dockerfiles, pinned bases. Caddy: TLS, `/api/*`→api, rest→web, security headers, trusted Cloudflare IPs. Migrations: a one-shot `migrate` service runs the SQL runner with the owner credentials before api/worker; runtime services never get owner credentials. Backups: nightly encrypted `pg_dump` to a separate bucket, 30-day retention; rehearsal restore before the first pilot. GitHub Actions: `ci.yml` (restore, build, format, analyzers, unit, integration, OCR, bundle/i18n parity), `e2e.yml`, `release.yml` (images to GHCR, SSH deploy, migrations, `/health/ready` smoke, rollback). Observability: Serilog JSON, Sentry (env), uptime on `/health/ready`. Env vars documented in `.env.example` and validated at boot: `DATABASE_URL`, `DIRECT_URL`, `MAINTENANCE_DATABASE_URL`, `REDIS_URL`, `JWT_ACCESS_SECRET`, `REFRESH_TOKEN_PEPPER`, `S3_*`, `OCR_SERVICE_URL`, `FCM_*`, `SENTRY_DSN`, `APP_ORIGIN`, `MAIL_*`, `LEGAL_TERMS_VERSION`, `LEGAL_PRIVACY_VERSION`, `BRAND_NAME`, feature flags.

---

## 16. Out of scope for the MVP (do not build)

WhatsApp Business bot; bank-statement import/APIs; collection reminders; public API/webhooks; multi-branch; day-closing snapshots; card gateway/billing (only an informational usage counter); email invitations (members created with a temporary password; **password-reset emails are in scope**); paid vision models (stub only); real-time SSE; ML training. Also all items with status `OUT`/`DEFERRED` in `docs/product/USE_CASES.md`.

---

## 17. Definition of Done and response protocol

A phase is done when its acceptance criteria pass; build, analyzers, and tests are green locally and in CI; docs updated (`ARCHITECTURE.md`, `PATTERNS.md`, runbooks); no secrets/personal data in the repo; migrations apply cleanly on an empty DB and on the previous state.

**Final message per phase, in order:**
1. **Summary** (≤ five lines).
2. **Files changed** (grouped by area).
3. **How to run and verify** (exact commands).
4. **Test results** (what ran, counts, coverage, skipped and why).
5. **Decisions and deviations** (ADR links).
6. **Assumptions and gaps** (unverified, needs real data/credentials).
7. **Risks and suggested next step.**

Do not claim something works unless you ran it. If you could not run it, say so.
