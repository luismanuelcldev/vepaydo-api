# VepayDO — Phase Prompts

Run one phase per session. The master prompt is the repository's `AGENTS.md` (at the root); it is always in context and is authoritative. The user starts a session by saying "Run Phase N" (or "Ejecuta la Fase N"); the agent then follows the preamble below and executes the matching phase section of this file.

## Session preamble (the agent runs this at the start of every session)

```text
You are working on VepayDO under the rules in AGENTS.md (the VepayDO Engineering Master Prompt)
Phase prompts live in docs/prompts/PHASES.md; reference documents live under docs/reference/.
Before writing code:
1. Read AGENTS.md, docs/ARCHITECTURE.md, docs/PATTERNS.md, docs/ASSUMPTIONS.md and docs/BACKLOG.md if they exist. For any frontend work (Phases 0 and 7) also read docs/design/UX_SPEC.md in full. Read the use cases listed for this phase in docs/product/USE_CASES.md.
2. Inspect the current repository state (git log, git status, tree) and summarize it in five lines.
3. Write a short plan for this phase (files, risks, test strategy) and then execute it.
Rules recap: English code everywhere, Prisma, creational patterns where specified, no placeholders, no invented bank layouts,
no scope creep, tests with every feature, ADR for any deviation, honest final report in the format of section 17.
```

---

## Phase 0 — Foundation and shared kernel

**Goal:** a clean, enforceable monorepo skeleton where later phases cannot violate the architecture.

**Tasks**

1. Initialize the pnpm + Turborepo monorepo with the layout from section 5. Package scope `@vepaydo/*`. Pin Node via `.nvmrc` and `engines`.
2. Create `packages/config` (tsconfig strict presets, ESLint flat config with typescript-eslint strict, `eslint-plugin-boundaries` or `dependency-cruiser` rules for the module layering, `no-restricted-imports` for `@prisma/client`, Prettier) and wire Husky, lint-staged, commitlint (Conventional Commits).
3. Create `packages/shared` with Zod schemas and TypeScript enums from section 4.4, plus a `BRAND_NAME` constant source (the API reads it from env, the web from build config).
4. Scaffold `apps/api` (NestJS + Fastify) with two entrypoints (`main.api.ts`, `main.worker.ts`), and the `shared/` kernel:
   - `AppConfig`: Zod-validated environment, frozen, provided as a **singleton through DI** (section 8.5). Boot fails with a readable error when the environment is invalid.
   - `Clock` interface with `SystemClock` and `FixedClock`; tenant time zone helpers.
   - `Money` value object over `Prisma.Decimal` with currency, arithmetic, comparison, rounding, serialization; exhaustive unit tests including cross-currency rejection.
   - Domain error base classes and an RFC 9457 problem-details exception filter with stable `code` and `i18nKey`.
   - `pino` logger with redaction paths, request id middleware, and a `traceId` in responses.
5. Scaffold `apps/web` (Vite + React + TS + Tailwind + shadcn/ui + i18next + TanStack Router/Query) with only a shell page and i18n wiring. No features yet. Read `docs/design/UX_SPEC.md` sections 2 and 3 and implement the **design tokens** (colors, typography, spacing, radii, elevation, motion) as CSS variables and the Tailwind theme in `packages/config`; self-host the fonts (Fontsource, Latin subset, no external font requests); add the single `<Logo>` component (placeholder mark until the owner provides assets in `docs/design/brand/`); add the automated contrast test described in UX spec section 3.1.
6. Scaffold `services/ocr` with FastAPI app, `/health/live`, `/health/ready`, Dockerfile, `ruff`, `mypy`, `pytest`. No OCR logic yet.
7. Create `docs/` skeletons: `ARCHITECTURE.md`, `PATTERNS.md` (empty table with the five creational patterns and "planned in phase N"), `ASSUMPTIONS.md`, `BACKLOG.md`, `adr/0001-record-architecture-decisions.md`, `adr/0002-prisma-instead-of-drizzle.md`.
8. Create `.github/workflows/ci.yml` running install, lint, typecheck, unit tests for all workspaces and the OCR service. Add `.env.example`.
9. `docker-compose.dev.yml` with PostgreSQL 16, Redis, MinIO. A `make dev` or `pnpm dev` that brings everything up.

**Acceptance criteria**

- `pnpm install && pnpm lint && pnpm typecheck && pnpm test` pass on a clean checkout; CI is green.
- A deliberate boundary violation (importing `@prisma/client` from a `domain/` file) fails lint. Prove it with a documented negative test or a CI script.
- Invalid environment prevents boot with a clear message.
- `Money` tests cover rounding, equality, and cross-currency errors.
- The token contrast test passes; fonts are self-hosted (a test or network check proves no requests to external font CDNs).

---

## Phase 1 — Database, Prisma, RLS, tenant context

**Goal:** the data layer is correct, isolated per tenant, and proven by tests before any feature depends on it.

**Use cases in scope** (`docs/product/USE_CASES.md`): None directly. The schema changes in section 4.5 (items 1 to 10) are the foundation for UC-AUTH-04 to 08, UC-SET-06, UC-OPS-01, UC-OPS-02, and UC-RCP-05.

**Tasks**

1. Translate `cuadra_schema.sql` into `schema.prisma` using the glossary in section 4 and **all** the schema changes in section 4.5 (items 1 to 10). Use `@@map`/`@map` for snake_case, composite foreign keys with `@@unique([tenantId, id])` on parents, Prisma enums mapped to DB enums, column types from section 7. Produce `docs/schema-mapping.md`.
2. Generate the initial migration and then add hand-written SQL migrations (`--create-only`) for: extensions; helper functions in schema `app` (`current_tenant_id`, `current_user_id`, `current_member_role`, `hamming_distance`, `set_updated_at`); CHECK constraints; partial, covering, and trigram indexes; integrity triggers (allocation validation, verifier rules, audit immutability); views with `security_invoker`; RLS (`ENABLE` + `FORCE`) and policies; roles and grants (`vepaydo_owner`, `vepaydo_app`, `vepaydo_maintenance`); `app.create_tenant`; `banks` catalog seed with `parserWave`. Keep the semantics of the reference SQL, with the changes from 4.5.
3. Verify Prisma drift behavior as described in section 7; document the outcome in `docs/runbooks/prisma-drift.md`.
4. Implement `PrismaService` (singleton) and `MaintenancePrismaService` (singleton, injectable only from the `retention` module, enforced by a lint rule or a DI-level guard).
5. Implement the tenant context stack: `AsyncLocalStorage`/`nestjs-cls` context, `TenantTransactionRunner`, `TxProvider` that **throws `MissingTenantContextError`** without a transaction, `runAsUser`, `runAsSystem(tenantId)`, and a `create_tenant` wrapper.
6. Write the **RLS and integrity test suite** from section 7 with Testcontainers: migrations as owner, tests as `vepaydo_app`. Include the support-role tests from AGENTS.md section 7 and the negative tests (a connection without context sees nothing; cross-tenant FK references fail; verifier rules; allocation limits; audit immutability; tenants only creatable through `create_tenant`).
7. `prisma/seed.ts`: banks catalog (idempotent) and, for development only, a demo tenant with fake data.

**Acceptance criteria**

- Migrations apply on an empty database and again on top of the previous state; CI runs them.
- All RLS and integrity tests pass; a mutation check (temporarily dropping a policy) makes at least one test fail, proving the suite detects regressions.
- No repository can read tenant tables without context (test).
- `prisma migrate diff` shows no destructive changes to protected objects, or the mitigation from `prisma-drift.md` is in CI.

---

## Phase 2 — Identity, tenants, members, settings, master data

**Goal:** a business can sign up, authenticate, invite a verifier, and register accounts and customers.

**Use cases in scope** (`docs/product/USE_CASES.md`): UC-AUTH-01 to 04, UC-AUTH-06 to 09, UC-SET-02 to 04, UC-DATA-01.

**Tasks**

1. `auth` module: register (user + tenant through `app.create_tenant`), login (argon2id), tenant selection when a user has several memberships, access JWT (15 min), refresh rotation with reuse detection stored in `sessions` (hash only), logout, `me`. Rate limiting and lockout backoff. Cookie flags as in section 13.
2. Guards: `JwtAuthGuard`, `AuthorizationGuard` enforcing `@RequirePermission(Permission.X)` over the `PermissionPolicy` matrix (9.5) and re-asserted in use cases (defense in depth); `@Roles()`/`RolesGuard` are **not** used. `AuthorizationGuard` also owns the `mustChangePassword` gate (`@AllowPasswordChangeRequired()` opts a route out). `TenantContextInterceptor` wraps each authenticated request in exactly one `TenantTransactionRunner.run` transaction.
3. `memberships`: owner creates a verifier with a one-time temporary password (`users.must_change_password = true`); every endpoint except `POST /auth/change-password`, `GET /auth/me`, and logout rejects such a user with a dedicated error code until the password is changed; change role and active flag; cannot remove the last owner.
4. `tenants`/settings: `TenantSettings` entity with defaults built via the **Prototype** (`TenantSettings.fromPrototype()`), get/patch with validation (window 1 to 60 days, tolerance ≥ 0, cap null or > 0, retention 1 to 120).
5. `business-accounts` and `customers` (with aliases and normalized names) CRUD, with unique constraints surfaced as domain errors.
6. Audit: `AuditEventBuilder` (**Builder**) and an `AuditService` that writes inside the caller's transaction. Audit all mutations in this phase.
7. Shared Zod schemas for every request and response in `packages/shared`; OpenAPI served outside production.
8. Update `docs/PATTERNS.md` (Singleton: config and Prisma; Prototype: settings; Builder: audit events) with tests that prove each.
9. **Password recovery** (UC-AUTH-04): `forgot-password` and `reset-password` with the rules in the use case and AGENTS.md section 9.9; a `MailSender` port with an SMTP implementation (nodemailer), `FakeMailSender` for tests, Mailpit in development; Spanish email templates through i18n; rate limits; neutral responses; session revocation on use.
10. **Sessions** (UC-AUTH-06, UC-AUTH-07): `sid` claim in the access JWT, Redis revocation set checked by the auth guard, endpoints to list and revoke sessions, Owner revocation of a member's sessions, automatic revocation when a member is deactivated.
11. **Legal acceptance** (UC-AUTH-08): `legal` module, `legal_acceptances`, blocking error `LEGAL_ACCEPTANCE_REQUIRED`, document endpoints reading `docs/legal/*.md`. Create the placeholder documents marked `[PENDING LEGAL REVIEW]`; never draft real legal text.

**Acceptance criteria**

- Full auth flow covered by integration tests including refresh-token reuse detection and lockout.
- A verifier cannot reach owner-only endpoints (guard test) and cannot bypass via direct DB rules (RLS suite still green).
- Tenant isolation verified through the API with two tenants in the same test.
- `TenantSettings.fromPrototype()` independence test passes.
- The acceptance tests of UC-AUTH-04, UC-AUTH-06, UC-AUTH-07, and UC-AUTH-08 (`docs/product/USE_CASES.md` section 2) pass.

---

## Phase 3 — Storage, upload, charges

**Goal:** reliable, idempotent receipt upload and the charge catalog.

**Use cases in scope** (`docs/product/USE_CASES.md`): UC-RCP-01 to 03 (backend side), UC-RCP-17 (list and filters), UC-DATA-03 to 06.

**Tasks**

1. `StorageToolkitFactory` (**Abstract Factory**, section 8.2) with `S3CompatibleStorageFactory` and `InMemoryStorageFactory`; products `ObjectStorage`, `SignedUrlService`, `StorageKeyGenerator`. Select by configuration. Test the key generator always prefixes the tenant id.
2. Upload pipeline: multipart parsing with limits, magic-byte sniffing, `sharp` re-encode and EXIF strip, SHA-256 on stored bytes, storage under `{tenantId}/{yyyy}/{mm}/{uuid}`, `Receipt(RECEIVED)` creation, audit event, enqueue job. `Idempotency-Key` honored (same key returns the existing receipt). Reject unsupported or oversized files with clear problem-details errors.
3. Receipts read side: list with filters and cursor pagination, detail, signed image URL (writes an access audit event).
4. `charges` module: CRUD, unique code per tenant (short, human-friendly, collision-safe generator), void with reason, CSV import (up to 1,000 rows, per-row validation report, all-or-nothing option), and `POST /charges/:id/duplicate` using **Prototype** (`ChargeDraft.clone()`: new identity, cleared status and allocations, independent deep copy).
5. `ChargeBalance` read model through the `v_charge_balances` view.
6. Queue plumbing: BullMQ queue `receipt-processing`, producer in the API, a stub processor that is replaced in Phase 4. Queue registry as a singleton provider.

**Acceptance criteria**

- Uploading the same file twice with the same key creates one receipt; with different keys creates two receipts with the same SHA-256 (the alert comes later).
- Non-image files with a spoofed `Content-Type` are rejected; EXIF is gone from stored images (test with a fixture carrying GPS EXIF).
- Cloning a charge never shares state with the original; storage keys never omit the tenant prefix; swapping the storage family requires no change in client code (test with `InMemoryStorageFactory`).

---

## Phase 4 — OCR service and extraction pipeline

**Goal:** from an uploaded image to structured, normalized, auditable extracted fields.

**Use cases in scope** (`docs/product/USE_CASES.md`): UC-RCP-04, UC-RCP-05, UC-RCP-06.

**Tasks**

1. `services/ocr`: implement the contract in section 11.1 with PaddleOCR. Verify option names against the installed version's documentation and record an ADR. `OcrEngineHolder` as a thread-safe lazy singleton warmed at startup; concurrency semaphore; limits; PDF first-page rasterization; perceptual hash (64-bit). pytest with Pillow-generated synthetic receipts; a test proving the holder initializes once under concurrent requests.
2. `extraction` module:
   - `ExtractionToolkitFactory` (**Abstract Factory**) with `PaddleOcrToolkitFactory`, `VisionModelToolkitFactory` (stub behind a feature flag), `FakeToolkitFactory`.
   - Normalizers (pure functions) and the generic Spanish transfer parser.
   - **Factory Method** parsers: `ReceiptParser`, `ReceiptParserCreator`, per-bank creators from configuration, `GenericSpanishTransferParserCreator`, `ParserCreatorRegistry` with a multi-provider token, bank detection by keyword scoring.
   - **Builder**: `ParsedReceiptBuilder` with validation of the required minimum; incomplete results flagged `requiresManualEntry`.
3. Worker processor replaces the Phase 3 stub: tenant context per job, state `PROCESSING → EXTRACTED | FAILED`, `Extraction` persistence (raw payload via the toolkit's serializer), update of current receipt fields, retries with exponential backoff, idempotency by `jobId = receiptId`, graceful shutdown.
4. Manual correction endpoint `PATCH /receipts/:id/fields` with before/after snapshots (**Prototype**: `ReceiptSnapshot.clone()`), validation, audit, and re-evaluation hook (wired in Phase 5). `POST /receipts/:id/retry-processing`.
5. Fixture harness and golden tests as in 11.2, plus `docs/runbooks/add-bank-parser.md`. Create **synthetic** fixtures only; write in `docs/ASSUMPTIONS.md` that bank configs await real captures.
6. **Failure classification** (UC-RCP-05): `FailureReason` heuristics in one tunable constants file, persisted in `receipts.failure_reason`, applied after retries and OCR errors, with synthetic blank, text-without-amount, and receipt-like test images. Borderline cases prefer `UNREADABLE`.

**Acceptance criteria**

- End-to-end integration test (Testcontainers with `FakeToolkitFactory`) from upload to `EXTRACTED`; failure path reaches `FAILED` after retries and allows manual entry.
- Adding a new bank parser requires adding one creator and one config file, no edits to existing parsers (demonstrate in the runbook with a dummy bank).
- Parser golden tests are deterministic and run without the OCR service.
- No OCR text or image data appears in logs (test with a log capture).
- The acceptance tests of UC-RCP-05 pass: a blank image gives `UNREADABLE`, an image with text but no amount gives `NOT_A_RECEIPT`, a receipt-like image reaches `EXTRACTED`, and the reason is persisted and returned by the API.

---

## Phase 5 — Matching, alerts, confirmation, audit

**Goal:** the business core: suggestions, alerts, human decisions, allocations, and permissions, all consistent with the database rules.

**Use cases in scope** (`docs/product/USE_CASES.md`): UC-RCP-07 to 16, UC-RCP-18 (backend), UC-DATA-02, UC-ALR-01 to 07.

**Tasks**

1. `matching` module: `MatchingEngine` as a pure function composed of signal Strategies (section 9.3), constants in `matching.constants.ts`, persistence of `suggested_charge_id`, `match_score`, `match_signals`. Trigram similarity for sender versus customer name/aliases through a typed raw query using `Prisma.sql`.
2. `alerts` module: `AlertEvaluator` with one Strategy per rule (section 9.4) producing `AlertCandidate`s through **`AlertBuilder`**; idempotent upsert; `AUTO_CLEARED` on re-evaluation; risk computation; resolution endpoint with permission and reason rules.
3. State machine for both axes (State pattern or an explicit transition table with exhaustive tests); the system outcome after extraction (section 9.1).
4. `allocations`: confirmation use case implementing the algorithm in 9.2 inside one transaction with the row lock, charge status updates, free-income path, reject, revert (owner only, voids allocations).
5. Domain events: `ReceiptExtracted → MatchingEngine → AlertEvaluator → status → notify`. Re-evaluation after manual corrections.
6. Audit completeness (section 9.8) with a test that every mutating use case writes at least one audit event in the same transaction.
7. Bulk confirm endpoint for strong matches without alerts (owner and verifier, caps respected).
8. **Sender association** (UC-DATA-02): the "Remember this sender" option on confirmation (default off) creates a normalized `customer_aliases` row with an audit event; alias conflicts with another customer are reported, and moving an alias is Owner-only; the `SENDER` signal and `SENDER_MISMATCH` suppression use aliases.
9. **Reject with reason code** (`RejectReasonCode`) and the discard path of UC-RCP-05 (rejection with `NOT_A_RECEIPT` or `UNREADABLE`, no free income created).

**Acceptance criteria**

- Table-driven unit tests for every alert rule (positive, negative, boundary), the matching score thresholds, and the allocation algorithm (exact, partial, overpaid, multiple charges, tolerance, currency mismatch).
- Integration tests prove that the application layer and the database rules agree: verifier cap, open critical alert, revert permissions, allocation over-limit, concurrent confirmations of the same receipt (one wins, the other gets a domain error).
- "Confirm anyway" by the owner writes the alert resolution and an audit event; a verifier attempting it is rejected both by guard and DB.
- The acceptance tests of UC-DATA-02 pass (alias suppresses `SENDER_MISMATCH` and raises the sender signal; alias uniqueness enforced).

---

## Phase 6 — Reconciliation, exports, notifications, retention, usage

**Goal:** the daily deliverable for the accountant, alerts that reach people, and the data lifecycle.

**Use cases in scope** (`docs/product/USE_CASES.md`): UC-REC-01 to 04, UC-NTF-01 to 03, UC-OPS-01 to 05, UC-AUTH-05, UC-SET-05, UC-SET-06.

**Tasks**

1. `reconciliation` module: query over `v_daily_reconciliation` and open charges; the verifier sees the current day only.
2. Report generation with the **Builder + Director** design from 8.3: `ReconciliationReportDirector`, `XlsxReportBuilder`, `CsvReportBuilder`, `PdfReportBuilder`. A single data model feeds all formats; tests compare totals across the three outputs. Export audited. Spanish labels via the i18n resources of the API (or label dictionaries), numbers formatted for `es-DO`.
3. `notifications`: FCM sender behind a port, `push_devices` registration, token cleanup on invalid-token responses, notification policy from the master prompt (critical alerts, reviews pending beyond a threshold, processing failures), with deduplication and quiet batching. Include deep links. A `FakePushSender` for tests.
4. `retention` module using `MaintenancePrismaService`: nightly repeatable job (03:00 America/Santo_Domingo) implementing the policy in section 13 (images, raw OCR JSON, cancelled tenants); idempotent, logged, dry-run mode, and tests with a fixed clock.
5. `usage` endpoint: receipts this month versus plan limit, informational.
6. `health` endpoints with readiness checks for PostgreSQL, Redis, storage, and OCR service.
7. **Support** (UC-SET-06, UC-OPS-01, UC-OPS-02, UC-AUTH-05): `support_grants` CRUD for Owners (1, 24, or 72 hours), the `SECURITY DEFINER` support functions and the `support` role as specified in AGENTS.md sections 4.5 and 9.9, the support console API, operator-issued password-reset links (15 minutes, shown once), the seed or script that creates Support users, audit with actor `SUPPORT`, and the email notice to the Owner. Support never reads tenant tables directly and never exposes images.

**Acceptance criteria**

- The three export formats agree to the cent on a seeded dataset.
- Retention job tests prove: images beyond the tenant window are removed while hashes and extracted data remain; a tenant's own setting is respected; audit rows are deleted only through the maintenance path.
- Notifications are not sent twice for the same alert (test).
- The acceptance tests of UC-SET-06, UC-OPS-01, UC-OPS-02, and UC-AUTH-05 pass, including: no grant means no data, expiry and revocation stop access immediately, writes always fail, and the operator token works once.

---

## Phase 7 — Frontend PWA

**Goal:** a fast, accessible, mobile-first interface that makes the one-tap path real. `docs/design/UX_SPEC.md` is authoritative for UI and copy; read it in full before starting.

**Use cases in scope** (`docs/product/USE_CASES.md`): The UI of every use case with status `MVP` in `docs/product/USE_CASES.md`, including the new flows (UC-AUTH-04 to 08, UC-DATA-02, UC-RCP-05, UC-SET-06, UC-OPS-01, UC-OPS-02).

**Tasks**

1. App shell: auth screens, tenant selection, role-aware navigation, TanStack Router with route-level code splitting, TanStack Query client with error mapping from problem-details to i18n keys, and a typed API client generated from or aligned with the shared Zod schemas.
2. Build **every route listed in section 12.2** (access and first run, receipts, charges, customers, accounts, alerts, reconciliation, audit, settings, usage, profile, system states) with the UX principles in 12.1: inbox sections with conditional polling; review screen (image zoom and pan, editable fields with per-field confidence, match and alert reasons, one primary action); confirm sheet showing the resulting status and excess before commit; reject and revert dialogs with mandatory reason; receipt history timeline; bulk confirm; undo window; charges (list, create, edit, detail, CSV import with the per-row report); customers with detail and aliases; accounts; alerts; daily reconciliation with export dialog; audit log; members, settings, and usage; profile; welcome, register, login, tenant selection, forced password change, onboarding, and the install and notifications guide. Implement the forced redirects described at the end of 12.2, and the system states (offline, error boundary, 403, 404, empty and loading states) for every list and detail view.
3. PWA: manifest with `share_target`; service worker (Workbox) intercepting the share POST, storing files in Dexie, redirecting to `/upload?source=share`; upload queue with idempotency keys, compression, retry, and visible states; Background Sync with the fallbacks from 12.3; offline shell and clear offline indicators.
4. Push: opt-in flow, FCM token registration and refresh, notification click deep links.
5. i18n: complete `es-DO` and `en`, key parity check in CI, `RD$` and date formatting per locale and tenant time zone.
6. Accessibility pass (axe in tests, keyboard walkthroughs) and a bundle-size budget check in CI.
7. Playwright e2e for: onboarding; upload → review → confirm; critical alert (owner resolves, verifier blocked); offline queue then reconnection; export download. Test the iOS fallback path (no share target) at least at the component level.
8. Build the **component catalogue** from UX spec section 7 and the development-only `/dev/components` route that renders every component in every state. Implement the navigation of UX spec section 4 (bottom tab bar with the raised upload button on mobile, sidebar on desktop, role-filtered "More" sheet, two-pane review on desktop).
9. Implement the `Analytics` port with the first-party `ux_events` implementation and emit the events of UX spec section 15 (no personal or financial data; a test inspects emitted payloads).
10. Write `docs/design/usability-test-kit.md` (tasks T1 to T6, facilitator script, SUS questionnaire in Spanish, issue severity template) and start `docs/design/ux-decisions.md`.
11. Apply the **UX Definition of Done** (UX spec section 17) to every screen and record the checklist result per screen in `docs/design/ux-checklist.md`.
12. Build the screens of the new use cases following UX spec section 8.11: `/forgot-password`, `/reset-password`, `/legal/accept`, `/legal/terms`, `/legal/privacy`, `/profile/sessions`, `/settings/support-access`, the `/support` routes, the unreadable-image and non-receipt handling in the review screen (three actions: complete manually, retake, discard), and the "Remember this sender" toggle in the confirm sheet.

**Acceptance criteria**

- Every route in section 12.2 exists, is role-guarded, and has loading, empty, and error states (route table test). Every screen passes the UX Definition of Done (UX spec section 17); copy matches UX spec section 9 and contains none of the forbidden words (a lint or test scans the locale files). A user with `mustChangePassword` cannot reach any other route.
- Happy path from share to confirmation is two taps after the receipt appears with a strong match (measured in the Playwright test as user actions).
- Lighthouse PWA and accessibility checks pass the thresholds recorded in the repository; initial route bundle within budget.
- No hard-coded user-facing strings (lint rule or test scanning for literals in JSX).
- When the network drops mid-upload, no receipt is lost or duplicated after reconnection.

---

## Phase 8 — Infrastructure, hardening, pilot readiness

**Goal:** a deployable, observable, recoverable system that one person can operate.

**Use cases in scope** (`docs/product/USE_CASES.md`): UC-OPS-04 to 06 (runbooks and rehearsals) and an end-to-end verification that every `MVP` use case has passing acceptance tests.

**Tasks**

1. Production `docker-compose.yml`, Caddyfile, multi-stage Dockerfiles, one-shot migration service with owner credentials, health checks, resource limits, private network, non-root users, read-only filesystems where feasible.
2. `release.yml`: build and push images, deploy over SSH, run migrations, smoke test `/health/ready`, automatic rollback to the previous tag.
3. Backups and restore rehearsal scripts per section 15; run a restore into a scratch database in CI or via a documented command, and record the evidence in `docs/runbooks/backup-restore.md`.
4. Observability: Sentry wiring (optional by env), log redaction audit, uptime check docs, authenticated `bull-board`.
5. Security review pass against section 13 with a checklist in `docs/SECURITY-CHECKLIST.md` (headers, cookies, rate limits, upload validation, RLS suite, dependency audit, secrets scan). Fix findings or log them in `docs/BACKLOG.md` with severity.
6. Runbooks: deploy, rollback, incident (queue stuck, OCR down, database full), onboarding a pilot tenant, adding a bank parser, data export and deletion request, operator-assisted password reset, and granting and using support access (the identity verification checklist itself is defined by the product owner in `docs/runbooks/support-identity-verification.md`; create the file with a clear placeholder).
7. Load smoke test (k6 or similar) for upload and inbox endpoints at pilot volumes; record results.
8. Final documentation pass: `README.md` (quick start), `ARCHITECTURE.md` (diagrams as Mermaid), `PATTERNS.md` complete with the five creational patterns and their tests, `ASSUMPTIONS.md` and `BACKLOG.md` current.

**Acceptance criteria**

- A fresh VPS can be brought to a running system from the README and runbooks only.
- A rollback and a backup restore have each been rehearsed, with evidence recorded.
- The full CI pipeline is green; coverage thresholds from section 14 are met.
- A written list of everything that still requires human input: real bank screenshots per bank, credentials, the legal review (Ley 172-13), DGII formalization, name clearance (ONAPI, domains), pricing validation, and card-gateway selection.

---

## Pilot readiness checklist (human-owned, outside the agent's scope)

- [ ] 10 business interviews done; pricing hypotheses adjusted.
- [ ] 50+ real receipt captures collected with consent, at least 10 per bank in the first wave; per-bank parser configs filled and accepted at ≥ 90% on amount, date, and reference fields.
- [ ] Name "VepayDO" cleared (ONAPI, `.do` and `.com` domains, app stores, social handles).
- [ ] Legal review of retention policy and terms of use; DGII formalization if invoicing.
- [ ] Backup restore rehearsed; rollback rehearsed.
- [ ] Legal texts supplied in `docs/legal/terms.es.md` and `docs/legal/privacy.es.md` after legal review (the agent only ships placeholders).
- [ ] Support identity verification checklist defined in `docs/runbooks/support-identity-verification.md`.
- [ ] SMTP provider chosen and `MAIL_*` configured (password recovery depends on it).
- [ ] Support user accounts created through the operator script.
- [ ] Pilot businesses onboarded with a feedback loop and agreed success metrics (time saved per payment, errors caught, one-tap resolution rate, alert usefulness).