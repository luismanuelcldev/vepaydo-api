# VepayDO — Milestone Prompts (ASP.NET Core)

Run one milestone (or one sub-step within a milestone) per session. The master
prompt is the repository's `AGENTS.md` (at the root); it is always in context and
is authoritative. The user starts a session by saying "Run M5.3" (or "Ejecuta el
M5.3"); the agent then follows the session preamble below and executes the
matching section of this file.

> **This file is the .NET edition.** An earlier version of this file described
> the NestJS/Prisma implementation (phases 0–8) that lived in the reference
> monorepo. That implementation is preserved for behaviour only; everything below
> is written for the ASP.NET Core solution in this repository. Where a use case ID
> is cited, the authoritative catalogue is `docs/product/USE_CASES.md`.

---

## Session preamble (run at the start of every session)

```text
You are working on VepayDO under the rules in AGENTS.md (the VepayDO Engineering
Master Prompt for the ASP.NET Core backend). Milestone prompts live in
docs/prompts/PHASES.md; reference documents live under docs/.

Before writing code:
1. Read AGENTS.md, docs/ARCHITECTURE.md, docs/PATTERNS.md, docs/ASSUMPTIONS.md,
   docs/BACKLOG.md, and docs/schema-mapping.md. Read the use cases listed for this
   milestone in docs/product/USE_CASES.md.
2. Inspect the repository state (git log, git status, the solution tree) and
   summarize it in five lines.
3. Confirm the previous milestone's acceptance criteria are met. If they are not,
   say exactly what is missing and stop.
4. Write a short plan (files, risks, test strategy), then execute it.

Rules recap: English code, comments, commits, DB objects and docs; PostgreSQL with
Row Level Security (RLS is the last line, guards come first); money is decimal via
the Money value object, never double; time via IClock and the tenant time zone;
PostgreSQL enum labels are UPPER_SNAKE_CASE and shared by the C# name, the JSON
value and the DB label; creational patterns only where they remove real variation
(Factory Method, Abstract Factory, Builder, Prototype, Singleton) and documented in
PATTERNS.md; no placeholders (a TODO needs a BACKLOG.md entry); no invented bank
layouts; no scope creep; a test with every feature; an ADR for any deviation; the
honest final report in the format of AGENTS.md section 17.
```

---

## Milestone map

| Milestone | Scope | State |
| --- | --- | --- |
| M0 | repository, .NET 10 toolchain, package pins, docs, ADRs | done |
| M1 | domain + application kernel (`Money`, clock, config, errors) | done |
| M2 | SQL schema (English), migration runner, RLS/integrity suite, EF model | done |
| M3 | tenant context and unit of work (fail-closed) | done |
| M4 | HTTP pipeline and security (JWT, problem details, gates, health) | done |
| M5 | business modules (auth gaps, tenants, members, accounts, customers, charges, receipts, alerts, allocations, audit) | pending |
| M6 | storage, OCR service, extraction, matching, reconciliation, notifications, retention, support | pending |
| M7 | infra, CI/CD, hardening, pilot readiness | pending |

---

# Completed milestones (reference, do not rebuild)

## M0 — repository, toolchain, package pins — DONE

.NET 10 solution (`VePayDO.slnx`), `Directory.Build.props` (`TreatWarningsAsErrors`,
`AnalysisMode=Recommended`, `nullable` enabled), `Directory.Packages.props` as the
single package-version authority, `global.json`, `.env.example`, the ported
`docs/`, and ADRs 0003 (migrate to ASP.NET Core), 0004 (PostgreSQL with RLS),
0005 (reuse the SQL migrations translated to English). See `docs/ASSUMPTIONS.md`
(M0).

## M1 — domain + application kernel — DONE

`Money` (readonly struct over `decimal`, bound currency, cross-currency throws),
`IClock`/`SystemClock`/`FixedClock` + `TenantTimeZone` (`America/Santo_Domingo`),
`TextNormalizer`, the `UPPER_SNAKE_CASE` enums, domain error base classes,
`AppConfig` + `AppConfigValidator` (all problems at once, fail fast). The layering
test suite (`VepayDO.Architecture.Tests`) enforces the dependency rule.

## M2 — schema, migration runner, RLS suite, EF model — DONE

The 11 English SQL migrations under `db/migrations/` (the schema's only owner),
`SqlMigrationRunner` (applies each file in lexical order inside its own
transaction, records `infra_schema_migrations`), the RLS/integrity/schema suites
on Testcontainers, and `AppDbContext` with the 15 PostgreSQL enums and all 43
hand-declared foreign keys.

## M3 — tenant context and unit of work — DONE

`TenantContext`, `TenantContextStore` (singleton, `AsyncLocal`), `TxProvider`
(throws `MissingTenantContextException`), `TenantTransactionRunner`
(`RunAsync`, `RunAsUserAsync`, `RunAsSystemAsync`, `ActivateAsync`,
`CreateTenantAsync`). Fail-closed: outside a tenant transaction every policy
matches zero rows.

## M4 — HTTP pipeline and security — DONE

Problem-details kernel (`ProblemJson`, `ProblemCatalog`,
`ProblemDetailsExceptionHandler`), `ValidationActionFilter` over FluentValidation,
`OriginGuardMiddleware`, JWT bearer with the `sid` revocation check,
`AccessAuthorizationHandler` + one policy per capability, account gates (temporary
password, legal acceptance), `TenantContextMiddleware` (one transaction per
request), `/api/v1` route-prefix convention, `/health/live` + `/health/ready`, and
`VepayDO.Api.Tests` (33 tests) over `WebApplicationFactory`.

The partial `auth` module already ships: register, login, select-tenant, refresh
(rotation + reuse detection), logout, me, change-password, locale preference,
forgot/reset password, sessions list/revoke/revoke-all, and the legal gate. The
remaining authentication-adjacent work is listed in M5.1.

---

# Pending milestones

## M5 — Business modules

**Goal:** the product's core on top of M0–M4: tenants and settings, members,
business accounts, customers, charges, receipt upload and reads, matching, alerts,
allocations/confirmation, and complete audit. This milestone is delivered in
sub-steps; run one per session.

**Use cases in scope** (`docs/product/USE_CASES.md`): UC-AUTH-01 to 09 (gaps),
UC-SET-01 to 05, UC-DATA-01 to 06, UC-RCP-01 to 18, UC-ALR-01 to 07.

### M5.1 — tenant settings and the membership/member module

**Use cases:** UC-SET-02 to 05, UC-AUTH-09.

1. `tenants` module: `TenantSettings` entity with defaults built via the
   **Prototype** (`TenantSettings.Default.FromPrototype()` — deep-independent
   clone, including `decimal` fields); `GET /settings` and `PATCH /settings` with
   validation (window 1–60 days, tolerance ≥ 0, cap null or > 0, retention 1–120),
   audit of every change. Settings are per-tenant and read/written inside the
   request transaction.
2. `memberships` module (OWNER): list members; create a `VERIFIER` with a one-time
   temporary password (`users.must_change_password = true`, password returned once);
   patch role/active with the last-active-owner guard; `POST /members/:userId/revoke-sessions`
   revokes all of a member's sessions. Deactivating a member revokes all their
   sessions.
3. Wire the `audit` module into every mutation above (`AuditEventBuilder` +
   `AuditService` writing inside the caller's transaction).
4. Update `docs/PATTERNS.md` with the Prototype usage (settings) and its
   independence test.

**Acceptance criteria**

- `TenantSettings.Default.FromPrototype()` returns an object that shares no
  mutable state with the prototype (test mutates the clone and re-reads the
  prototype).
- A verifier cannot reach owner-only endpoints (guard test) and the DB rules still
  reject it (RLS suite green).
- Creating a member returns a temporary password exactly once and forces the
  password-change gate on the next request.
- The last active owner cannot be deactivated or demoted; a deactivated member's
  sessions stop working immediately.
- Every mutation writes exactly one audit event in the same transaction (test).

### M5.2 — business accounts and customers

**Use cases:** UC-DATA-01, UC-DATA-02, UC-SET-03.

1. `business-accounts` module (OWNER): CRUD with the destination-last4 shape; only
   the last 4 digits are ever stored or returned.
2. `customers` module: CRUD plus **aliases** (`customer_aliases`), with
   `TextNormalizer`-based normalized names; alias uniqueness per tenant surfaced as
   a domain error; moving an alias between customers is OWNER-only.
3. Emit audit events for every change.

**Acceptance criteria**

- Destination accounts never expose a full account number (test asserts only the
  last 4 appear in responses).
- Alias uniqueness per tenant is enforced by the DB and surfaced as a controlled
  domain error, not a raw constraint violation.
- A verifier can read customers but not mutate business accounts (permission
  matrix, guards + RLS).

### M5.3 — charges module

**Use cases:** UC-DATA-03 to 06.

1. `charges` module: CRUD, unique short human-friendly `code` per tenant
   (collision-safe generator), `void` with reason, CSV import (up to 1,000 rows,
   per-row validation report, all-or-nothing option).
2. `POST /charges/:id/duplicate` using the **Prototype** pattern
   (`ChargeDraft.Clone()`: new identity, cleared status and allocations, deep
   independent copy — tested).
3. `ChargeBalance` read model through the `v_charge_balances` view.

**Acceptance criteria**

- Cloning a charge never shares state with the original (test asserts deep
  independence across status, allocations and decimals).
- CSV import reports per-row errors without aborting unless all-or-nothing is
  requested.
- Charge codes are unique per tenant under concurrent creation (DB constraint +
  retry).

### M5.4 — storage toolkit and receipt upload

**Use cases:** UC-RCP-01 to 03, UC-RCP-17.

1. `StorageToolkitFactory` (**Abstract Factory**) with `S3CompatibleStorageFactory`
   (AWS SDK for .NET) and `InMemoryStorageFactory`; products `IObjectStorage`,
   `ISignedUrlService`, `IStorageKeyGenerator`. Selection by configuration.
2. Upload pipeline: multipart parsing with limits, magic-byte sniffing (never
   trust `Content-Type`), ImageSharp re-encode and EXIF strip, SHA-256 on the
   stored bytes, key `{tenantId}/{yyyy}/{mm}/{uuid}`, `Receipt(RECEIVED)` creation,
   audit event, and enqueue of the processing job. `Idempotency-Key` honored (the
   same key returns the existing receipt).
3. Receipt read side: list with filters and cursor pagination, detail, signed
   image URL (writes an access audit event).
4. Add the `VepayDO.Worker` project (entrypoint) and the `BackgroundService`
   queue consumer with a stub processor (replaced in M6). The worker sets the
   tenant context per job exactly like HTTP.

**Acceptance criteria**

- Uploading the same file twice with the same key creates one receipt; with a
  different key, two receipts with the same SHA-256 (the duplicate alert comes in
  M6).
- A non-image file with a spoofed `Content-Type` is rejected; EXIF (including GPS)
  is gone from the stored image (fixture test).
- Storage keys always include the tenant prefix; swapping the storage family
  requires no change in client code (test with `InMemoryStorageFactory`).

### M5.5 — manual field entry and receipt corrections

**Use cases:** UC-RCP-05, UC-RCP-06.

1. `PATCH /receipts/:id/fields` with before/after snapshots (**Prototype**:
   `ReceiptSnapshot.Clone()`), validation, audit, and a re-evaluation hook (wired
   in M5.7/M6).
2. `POST /receipts/:id/retry-processing`.
3. The review path works with empty editable fields (manual-first; OCR is deferred
   per `AGENTS.md` section 8).

**Acceptance criteria**

- A manual correction writes an audit event carrying the before/after values and
  never loses the original (the extraction row keeps the raw value).
- Retry is idempotent by `jobId = receiptId`.

### M5.6 — audit completion

**Use cases:** covers UC-AUD-01 and AGENTS.md section 9.8.

1. Ensure every mutating use case from M5.1–M5.5 writes at least one audit event
   in the same transaction; the receipt history endpoint (`GET /receipts/:id/history`)
   renders it.
2. A test that enumerates the mutating use cases and asserts an audit row per
   call.

### M5.7 — matching engine and alerts

**Use cases:** UC-RCP-07, UC-RCP-18, UC-ALR-01 to 07.

1. `matching` module: `MatchingEngine` as a pure function of
   `(receipt, candidates, settings, clock)` composed of signal Strategies
   (`CHARGE_CODE` 0.40 only when OCR text exists, `AMOUNT` 0.30, `SENDER` 0.20 via
   `pg_trgm`, `DATE` 0.10); constants in one file; persist `suggested_charge_id`,
   `match_score`, `match_signals`; strong match ≥ 0.60 with margin ≥ 0.15;
   candidates list ≥ 0.25 up to 5.
2. `alerts` module: `AlertEvaluator` with one Strategy per rule (AGENTS.md
   section 9.4) producing `AlertCandidate`s through **`AlertBuilder`**; idempotent
   upsert by `(tenant_id, receipt_id, rule)` while open; `AUTO_CLEARED` when a rule
   stops firing; receipt `risk` = max severity among open alerts; resolution
   endpoint with permission and reason rules.
3. The system outcome after processing (AGENTS.md section 9.1): `DUPLICATE`,
   `SUSPICIOUS`, `UNMATCHED`, else `PENDING_REVIEW` with a suggestion.

**Acceptance criteria**

- Table-driven unit tests for every alert rule (positive, negative, boundary) and
  the matching score thresholds.
- Re-evaluation closes obsolete alerts with `AUTO_CLEARED` and preserves manual
  resolutions.
- Alerts are never presented as verdicts (copy check; messages are alerts).

### M5.8 — allocations, confirmation, reject, revert

**Use cases:** UC-RCP-08 to 16.

1. `allocations`: the confirmation algorithm (AGENTS.md section 9.2) inside one
   transaction with a row lock on the receipt: no charge ⇒ `CONFIRMED` +
   `is_free_income`; allocate `min(remaining, ri)` in order; status computed with
   the tenant tolerance (exact / partial / overpaid, excess recorded in the note);
   charge status updates (`PAID` within tolerance, else `PARTIAL`); currencies must
   match.
2. `reject` with a `RejectReasonCode` (note optional) ⇒ `REJECTED`; `revert`
   (OWNER only, reason required) from a final status ⇒ `PENDING_REVIEW`, voiding
   active allocations.
3. Bulk confirm for strong matches without open alerts (owner and verifier, caps
   respected).
4. State machine for both axes (explicit transition table with exhaustive tests).
5. Sender association (UC-DATA-02): "remember this sender" on confirmation
   (default off) creates a normalized alias with an audit event; the `SENDER`
   signal and `SENDER_MISMATCH` suppression use aliases.

**Acceptance criteria**

- Unit tests for the allocation algorithm (exact, partial, overpaid, multiple
  charges, tolerance, currency mismatch) and the state transitions.
- A receipt with an open CRITICAL alert can be confirmed only by an OWNER
  ("confirm anyway" → `CONFIRMED_ANYWAY` + reason); a verifier is rejected by both
  guard and DB.
- Concurrent confirmations of the same receipt: one wins, the other gets a domain
  error (row lock).
- Verifier cap, revert permission and allocation over-limit agree between the
  application layer and the DB rules.

---

## M6 — Storage pipeline, OCR, reconciliation, notifications, retention, support

**Goal:** from an uploaded image to structured fields, the daily deliverable for
the accountant, alerts that reach people, and the data lifecycle.

> OCR is deferred per `AGENTS.md` section 8 (no engine chosen, and the app is
> manual-first). The extraction seam, the worker and the matching/alerts pipeline
> are still built so manual entry flows end to end; the PaddleOCR service is built
> behind the extraction port and feature-flagged.

**Use cases in scope** (`docs/product/USE_CASES.md`): UC-RCP-04 to 06, UC-REC-01
to 04, UC-NTF-01 to 03, UC-OPS-01 to 05, UC-AUTH-05, UC-SET-05, UC-SET-06.

### M6.1 — OCR service (`services/ocr`) and the extraction module

1. `services/ocr` (Python 3.12 + FastAPI + PaddleOCR, CPU): `POST /extract`
   (multipart) → `{ lines:[{text,box,confidence}], phash, width, height, engine,
   durationMs }`; health endpoints; the engine loaded once (warm,
   semaphore-limited), request timeout, pixel/byte limits, PDF first page only.
   Stateless, persists nothing, **never logs bytes or recognized text**. pytest
   with synthetic Pillow images (no bank branding).
2. `extraction` module: `ExtractionToolkitFactory` (**Abstract Factory**) with
   `PaddleOcrToolkitFactory`, `VisionModelToolkitFactory` (stub behind a feature
   flag), `FakeToolkitFactory`; pure normalizers; the generic Spanish transfer
   parser; **Factory Method** parsers (`ReceiptParserCreator` per bank +
   `GenericSpanishTransferParserCreator`, selection via a `ParserCreatorRegistry`
   keyed by `BankCode`); **Builder** `ParsedReceiptBuilder` with the
   `requiresManualEntry` flag.
3. Worker processor replaces the M5.4 stub: tenant context per job,
   `PROCESSING → EXTRACTED | FAILED`, extraction persistence, retries with
   exponential backoff, idempotency by `jobId = receiptId`, graceful shutdown.
4. Failure classification (UC-RCP-05): `FailureReason` heuristics in one tunable
   file, persisted in `receipts.failure_reason`; borderline prefers `UNREADABLE`.
5. Fixture harness and golden tests; `docs/runbooks/add-bank-parser.md`; only
   synthetic fixtures (real bank configs await real captures — document in
   `ASSUMPTIONS.md`).

**Acceptance criteria**

- End-to-end integration test (Testcontainers + `FakeToolkitFactory`) from upload
  to `EXTRACTED`; the failure path reaches `FAILED` after retries and allows
  manual entry.
- Adding a new bank parser requires one creator and one config file, no edits to
  existing parsers (demonstrated with a dummy bank in the runbook).
- No OCR text or image data appears in logs (log-capture test).
- UC-RCP-05 acceptance: blank image ⇒ `UNREADABLE`, text without amount ⇒
  `NOT_A_RECEIPT`, receipt-like ⇒ `EXTRACTED`; the reason is persisted and
  returned.

### M6.2 — reconciliation and report exports

1. `reconciliation` module: query over `v_daily_reconciliation` plus open charges;
   grouped by payment date by status, bank and destination account. The verifier
   sees the current day only.
2. Report generation with **Builder + Director**: `ReconciliationReportDirector`,
   `XlsxReportBuilder` (ClosedXML), `CsvReportBuilder`, `PdfReportBuilder`
   (QuestPDF); a single data model feeds all three; totals compared across outputs;
   export audited; Spanish labels and `es-DO` number formatting.

**Acceptance criteria**

- The three formats agree to the cent on a seeded dataset.
- The verifier is restricted to the current day (test).

### M6.3 — notifications and push devices

1. `notifications` module: FCM sender behind a port, `push_devices` registration,
   token cleanup on invalid-token responses, the notification policy (critical
   alerts, reviews pending beyond a threshold, processing failures) with
   deduplication and quiet batching; deep links; `FakePushSender` for tests.

**Acceptance criteria**

- No notification is sent twice for the same alert (test).

### M6.4 — retention and usage

1. `retention` module (`MaintenancePrismaService` equivalent: maintenace
   connection via `ConnectionStrings:MaintenanceConnection`): nightly repeatable
   job at 03:00 America/Santo_Domingo implementing the policy in AGENTS.md
   section 13 (images 24 mo, raw OCR 12 mo, cancelled tenants); idempotent,
   logged, dry-run mode, fixed-clock tests.
2. `usage` endpoint: receipts this month versus plan limit (informational).

**Acceptance criteria**

- Images beyond the tenant window are removed while hashes and extracted data
  remain; a tenant's own retention setting is respected; audit rows are deleted
  only through the maintenance path.

### M6.5 — support (grants, console, operator password reset)

**Use cases:** UC-SET-06, UC-OPS-01/02, UC-AUTH-05.

1. `support` module: `support_grants` CRUD for Owners (1/24/72 h); the support
   console API over the `SECURITY DEFINER` functions (read audit events, receipt
   metadata and alerts; never images); operator-issued password-reset links
   (15 min, shown once); the seed/script that creates Support users; audit with
   actor `SUPPORT`; the email notice to the Owner.

**Acceptance criteria**

- UC-SET-06 / UC-OPS-01/02 / UC-AUTH-05 acceptance: no grant ⇒ no data; expiry and
  revocation stop access immediately; writes always fail; the operator token works
  once. Support never reads tenant tables directly.

### M6.6 — health readiness

1. `/health/ready` checks PostgreSQL, Redis, storage and the OCR service.

---

## M7 — Infrastructure, hardening, pilot readiness

**Goal:** a deployable, observable, recoverable system that one person can
operate.

**Use cases in scope** (`docs/product/USE_CASES.md`): UC-OPS-04 to 06.

1. Production `docker-compose.yml` and `docker-compose.dev.yml`: `caddy`, `api`,
   `worker`, `ocr`, `postgres`, `redis`, plus `minio`/`mailpit` in dev. Caddyfile
   (TLS terminated at Caddy, `/api/*` → api), multi-stage Dockerfiles (pinned
   bases, non-root users, read-only root filesystems where feasible), health
   checks, resource limits, a private network, and a one-shot `migrate` service
   that runs `SqlMigrationRunner` with the owner credentials **before** api/worker
   start.
2. GitHub Actions: `ci.yml` (restore, build, `dotnet format --verify-no-changes`,
   analyzers, unit, integration with Testcontainers, OCR pytest, i18n parity for
   the frontend's ARB files), `e2e.yml`, `release.yml` (images to GHCR, SSH
   deploy, migrations, `/health/ready` smoke, rollback).
3. Replace the in-process `LoginAttemptStore` and `InMemorySessionRevocationStore`
   with Redis-backed implementations; set `REDIS_URL` for any multi-instance
   deployment.
4. Backups and restore rehearsal per AGENTS.md section 15; evidence in
   `docs/runbooks/backup-restore.md`.
5. Observability: Serilog JSON, redaction audit, Sentry (env), uptime on
   `/health/ready`.
6. Security review pass against AGENTS.md section 13 with a checklist in
   `docs/SECURITY-CHECKLIST.md`.
7. Runbooks: deploy, rollback, incident (queue stuck, OCR down, database full),
   onboarding a pilot tenant, adding a bank parser, data export/deletion request,
   operator-assisted password reset, support access
   (`docs/runbooks/support-identity-verification.md` is product-owner defined; ship
   a clear placeholder).
8. Load smoke test (k6 or similar) at pilot volumes; record results.
9. Final documentation pass: `README.md`, `ARCHITECTURE.md` (Mermaid diagrams),
   `PATTERNS.md` complete with the five creational patterns and their tests,
   `ASSUMPTIONS.md` and `BACKLOG.md` current.

**Acceptance criteria**

- A fresh VPS can be brought to a running system from the README and runbooks
  only.
- A rollback and a backup restore have each been rehearsed, with evidence.
- The full CI pipeline is green; coverage thresholds from AGENTS.md section 14 are
  met.
- A written list of everything that still requires human input is current.

---

## Pilot readiness checklist (human-owned, outside the agent's scope)

- [ ] 10 business interviews done; pricing hypotheses adjusted.
- [ ] 50+ real receipt captures collected with consent, at least 10 per bank in
      the first wave; per-bank parser configs filled and accepted at ≥ 90% on
      amount, date and reference fields.
- [ ] Name "VepayDO" cleared (ONAPI, `.do` and `.com` domains, app stores, social
      handles).
- [ ] Legal review of the retention policy and the terms of use; DGII
      formalization if invoicing.
- [ ] Backup restore rehearsed; rollback rehearsed.
- [ ] Legal texts supplied in `docs/legal/terms.es.md` and `docs/legal/privacy.es.md`
      after legal review (the agent only ships placeholders).
- [ ] Support identity verification checklist defined in
      `docs/runbooks/support-identity-verification.md`.
- [ ] SMTP provider chosen and `MAIL_*` configured (password recovery depends on
      it).
- [ ] Support user accounts created through the operator script.
- [ ] Pilot businesses onboarded with a feedback loop and agreed success metrics.
