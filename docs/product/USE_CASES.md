# VepayDO — Use Case Catalogue

**Purpose:** the single list of what the product does, for whom, and in which phase it is built. It prevents two failures: building something nobody approved, and forgetting something the business depends on.
**Authority:** `AGENTS.md` defines how to build; this file defines **what** is in scope. If a behavior is not listed here as `MVP`, do not build it. Reference use case IDs (`UC-RCP-07`) in branch names, pull requests, tests, and ADRs.
**Language:** English; user-facing copy is Spanish (es-DO) per `docs/design/UX_SPEC.md`.

## Status legend

| Status | Meaning |
|---|---|
| `MVP` | Build in the phases listed |
| `V1.1` | Planned after the pilot; do not build now |
| `OUT` | Explicitly not in scope |
| `DEFERRED` | Needs a product decision after the pilot interviews; do not build, but document the current MVP behavior |

## Actors

| Actor | Description |
|---|---|
| **Owner** | Business owner or manager. Configures the business, resolves critical alerts, sees everything |
| **Verifier** | Cashier or assistant. Uploads, reviews, and confirms within limits |
| **Customer** | External person who pays and sends the receipt. Has no login in the MVP |
| **Accountant** | External consumer of the exported reconciliation. Has no login in the MVP |
| **Support** | Platform operator (the product team). Separate from tenants; minimal, audited, owner-granted access |
| **System** | Worker, scheduler, and notification services |

---

## 1. Catalogue

IDs are stable. "Phase" refers to `docs/prompts/PHASES.md` (the UI of every use case is built in Phase 7).

### A. Access and account security (`AUTH`)

| ID | Use case | Actor | Status | Phase | Summary |
|---|---|---|---|---|---|
| UC-AUTH-01 | Register a business and its Owner | Owner | MVP | 2 | Create user and tenant (`app.create_tenant`), accept terms, land on onboarding |
| UC-AUTH-02 | Log in and choose a business | Owner, Verifier | MVP | 2 | Email and password; business selector when the user has several memberships |
| UC-AUTH-03 | Change a temporary password | Verifier | MVP | 2 | Forced on first login after an Owner created the account; blocks every other route |
| UC-AUTH-04 | Recover a forgotten password (self-service) | Owner, Verifier | **MVP (new)** | 2 | Email reset link, single use, 30 minutes. Detailed below |
| UC-AUTH-05 | Operator-assisted password reset | Support | **MVP (new)** | 6 | Support issues a one-time reset link after identity verification. Detailed below |
| UC-AUTH-06 | View and revoke my own sessions | Owner, Verifier | **MVP (new)** | 2 | List devices; revoke one or all others. Detailed below |
| UC-AUTH-07 | Revoke a member's sessions or deactivate a member | Owner | **MVP (new)** | 2 | Takes effect immediately (lost phone, departing employee) |
| UC-AUTH-08 | Accept terms and privacy policy | Owner, Verifier | **MVP (new)** | 2 | At registration and whenever the published version changes. Detailed below |
| UC-AUTH-09 | Log out | All | MVP | 2 | Revokes the current session |

### B. Business setup (`SET`)

| ID | Use case | Actor | Status | Phase | Summary |
|---|---|---|---|---|---|
| UC-SET-01 | Complete onboarding | Owner | MVP | 2, 7 | Business details, main bank account, optional sample receipt |
| UC-SET-02 | Manage business bank accounts | Owner | MVP | 2 | Bank, last 4 digits, currency, active flag |
| UC-SET-03 | Manage members | Owner | MVP | 2 | Create a Verifier with a one-time temporary password; change role; activate or deactivate; cannot remove the last Owner |
| UC-SET-04 | Configure business settings | Owner | MVP | 2 | Date window, amount tolerance, verifier cap, image retention |
| UC-SET-05 | View plan usage | Owner | MVP | 6 | Receipts this month versus plan limit (informational, no billing) |
| UC-SET-06 | Grant and revoke support access | Owner | **MVP (new)** | 6 | Time-boxed, read-only, audited. Detailed below |
| UC-SET-07 | Install the PWA and enable notifications | Owner, Verifier | MVP | 7 | Contextual prompts after the first confirmation; iOS instructions |

### C. Master data (`DATA`)

| ID | Use case | Actor | Status | Phase | Summary |
|---|---|---|---|---|---|
| UC-DATA-01 | Manage customers and their aliases | Owner, Verifier | MVP | 2 | CRUD with normalized names; aliases are names under which a customer's transfers appear |
| UC-DATA-02 | Associate a third-party payer with a customer | Owner, Verifier | **MVP (new)** | 5 | "Remember this sender" when someone else pays for a customer. Detailed below |
| UC-DATA-03 | Create and edit a charge | Owner, Verifier | MVP | 3 | Amount, currency, customer (optional), description, due date, generated short code |
| UC-DATA-04 | Import charges from CSV | Owner, Verifier | MVP | 3 | Up to 1,000 rows, preview, per-row error report, downloadable error file |
| UC-DATA-05 | Duplicate a charge | Owner, Verifier | MVP | 3 | Prototype clone: new identity, cleared status and allocations |
| UC-DATA-06 | Void a charge | Owner, Verifier | MVP | 3 | Reason required; blocked if it has active allocations unless they are voided first |

### D. Receipts (`RCP`)

| ID | Use case | Actor | Status | Phase | Summary |
|---|---|---|---|---|---|
| UC-RCP-01 | Upload receipts (camera, gallery, file) | Owner, Verifier | MVP | 3 | Up to 20 files, 10 MB each, idempotent by client key |
| UC-RCP-02 | Share a receipt from WhatsApp to the app | Verifier | MVP | 3, 7 | Android Share Target; files stored locally then uploaded; iOS falls back to gallery |
| UC-RCP-03 | Upload while offline and sync later | Owner, Verifier | MVP | 3, 7 | Queue in IndexedDB with visible state; no loss, no duplicates |
| UC-RCP-04 | Process a receipt automatically | System | MVP | 4 | OCR, parse, normalize, store extraction, evaluate matching and alerts |
| UC-RCP-05 | Handle an unreadable image or a non-receipt | Verifier, System | **MVP (new)** | 4, 5, 7 | Classify failure, guide the person, never lose the file. Detailed below |
| UC-RCP-06 | Review and correct extracted fields | Owner, Verifier | MVP | 4, 7 | Per-field confidence, manual entry path, original OCR value preserved, audit before and after |
| UC-RCP-07 | Confirm a receipt against one charge (exact) | Owner, Verifier | MVP | 5 | Status `CONFIRMED` |
| UC-RCP-08 | Confirm a partial payment | Owner, Verifier | MVP | 5 | Status `PARTIAL`; charge remains `PARTIAL` with balance |
| UC-RCP-09 | Confirm an overpayment | Owner, Verifier | MVP | 5 | Status `OVERPAID`; excess recorded in the decision note |
| UC-RCP-10 | Confirm one receipt across several charges | Owner, Verifier | MVP | 5 | Ordered allocation until the amount is exhausted |
| UC-RCP-11 | Confirm several receipts for one charge | Owner, Verifier | MVP | 5 | Cumulative allocations until the charge is `PAID` |
| UC-RCP-12 | Confirm as free income or create a charge from the receipt | Owner, Verifier | MVP | 5 | Cash-register style income without a prior charge |
| UC-RCP-13 | Bulk-confirm strong matches | Owner, Verifier | MVP | 5, 7 | Only receipts with strong match and no open alerts; caps respected |
| UC-RCP-14 | Undo a confirmation within 5 seconds | Owner, Verifier | MVP | 7 | Optimistic UI with rollback; mechanism decided in an ADR |
| UC-RCP-15 | Reject a receipt | Owner, Verifier | MVP | 5 | Reason code and note required |
| UC-RCP-16 | Revert a final status | Owner | MVP | 5 | Reason required; active allocations are voided |
| UC-RCP-17 | Search and filter receipts | Owner, Verifier | MVP | 3, 7 | By status, risk, date, bank, sender, reference; answers "did this customer pay?" |
| UC-RCP-18 | View a receipt's history | Owner | MVP | 5, 7 | Timeline from the audit log, including image accesses |

### E. Alerts (`ALR`)

| ID | Use case | Actor | Status | Phase | Summary |
|---|---|---|---|---|---|
| UC-ALR-01 | Detect a duplicate reference or identical image | System | MVP | 5 | `CRITICAL`; receipt status `DUPLICATE` |
| UC-ALR-02 | Detect a very similar image | System | MVP | 5 | `WARNING`; perceptual hash distance ≤ 6 within 90 days |
| UC-ALR-03 | Detect a foreign destination account | System | MVP | 5 | `CRITICAL`; last 4 digits not among the business's active accounts for that bank |
| UC-ALR-04 | Detect date anomalies | System | MVP | 5 | Future date, date before the charge, stale date (`WARNING`) |
| UC-ALR-05 | Detect amount mismatch, sender mismatch, low confidence | System | MVP | 5 | `INFO` alerts; aliases suppress sender mismatch |
| UC-ALR-06 | Resolve an alert | Owner, Verifier | MVP | 5 | Dismiss (reason), confirm anyway (Owner, reason), or reject the receipt |
| UC-ALR-07 | Verifier blocked by a critical alert | Verifier | MVP | 5, 7 | Confirm disabled with the reason shown; reject remains available |

### F. Reconciliation and reporting (`REC`)

| ID | Use case | Actor | Status | Phase | Summary |
|---|---|---|---|---|---|
| UC-REC-01 | View the daily reconciliation | Owner | MVP | 6 | By payment date, status, bank, account, plus open charges |
| UC-REC-02 | Export the reconciliation | Owner | MVP | 6 | XLSX, CSV, PDF from one data model; audited |
| UC-REC-03 | View today's reconciliation | Verifier | MVP | 6 | Current day only |
| UC-REC-04 | View the audit log | Owner | MVP | 6 | Filter by entity, user, date |

### G. Notifications (`NTF`)

| ID | Use case | Actor | Status | Phase | Summary |
|---|---|---|---|---|---|
| UC-NTF-01 | Receive a critical alert notification | Owner, Verifier | MVP | 6 | One per alert, deep link to the receipt |
| UC-NTF-02 | Receive a pending-review reminder | Owner, Verifier | MVP | 6 | Batched, never for routine confirmations |
| UC-NTF-03 | Receive a processing-failure notification | Owner, Verifier | MVP | 6 | Includes the failure reason |

### H. Platform and operations (`OPS`)

| ID | Use case | Actor | Status | Phase | Summary |
|---|---|---|---|---|---|
| UC-OPS-01 | View tenant health and metadata (support console) | Support | **MVP (new)** | 6, 7 | No business data. Detailed below |
| UC-OPS-02 | Read-only access during an active grant | Support | **MVP (new)** | 6, 7 | Audit events, receipts metadata, alerts; never images. Detailed below |
| UC-OPS-03 | Purge expired data | System | MVP | 6 | Nightly retention job with the maintenance role |
| UC-OPS-04 | Cancel an account: export then delete | Owner, Support | MVP | 6, 8 | 30 days to export, hard deletion at 90 days, runbook-driven |
| UC-OPS-05 | Delete a specific receipt on request | Owner, Support | MVP | 6, 8 | Image purged, audit event kept |
| UC-OPS-06 | Back up and restore | Operator | MVP | 8 | Encrypted nightly dump, rehearsed restore |

### I. Out of scope (`OUT`)

| ID | Item | Note |
|---|---|---|
| OOS-01 | Cash deposit slips (ventanilla, cajero automático) | Different document type; revisit with pilot data |
| OOS-02 | Cheques | Not a transfer |
| OOS-03 | Card payments (Azul, CardNET and similar) | Different settlement and evidence |
| OOS-04 | Digital wallets | Different receipts |
| OOS-05 | Bank notifications by SMS or email as receipts | Text-based; V1.1 candidate |
| OOS-06 | WhatsApp Business bot (customer-facing automatic replies) | Future |
| OOS-07 | Bank statement import and bank APIs | V1.1; the strongest verification feature |
| OOS-08 | Electronic invoicing (DGII) | Not part of the product |

### J. Deferred decisions (`DEFERRED`)

These need a decision after the pilot interviews. **Do not build them.** The last column documents what the MVP does if the situation occurs, so the behavior is predictable.

| ID | Scenario | Current MVP behavior |
|---|---|---|
| DEF-01 | Transfers where the instruction date differs from the settlement date, or that are scheduled for the future | A future date raises `FUTURE_DATE` (warning); the person can dismiss it with a reason |
| DEF-02 | Payments reduced by bank fees | Amount differs from the balance: `AMOUNT_MISMATCH` (info) and the payment resolves as `PARTIAL` unless inside the tolerance setting |
| DEF-03 | Refunds and reversed transfers | Not supported. Use "Revert" (Owner) on the original receipt with a reason note |
| DEF-04 | Recurring charges (tuition, rent) and credit terms with statements per customer | Use "Duplicate charge" manually; due dates exist; no recurring engine and no statements |
| DEF-05 | External accountant with read-only access to several businesses | The Owner shares the exported files |
| DEF-06 | Customer self-service link to upload a receipt | Not available; the business uploads on the customer's behalf |
| DEF-07 | USD charges versus DOP payments (or the reverse) | Currencies must match; allocation across currencies is rejected |
| DEF-08 | Verify the account holder name on the destination, not only the last 4 digits | Only the last 4 digits and bank are checked |

---

## 2. Detailed specifications for the new use cases

Each specification lists preconditions, the main flow, alternates, business rules, and acceptance tests (Given / When / Then). Acceptance tests become automated tests in the indicated phase.

### UC-AUTH-04 — Recover a forgotten password (self-service)

**Actor:** any user. **Preconditions:** an SMTP provider is configured (`MAIL_*`; Mailpit in development).

**Main flow**
1. On `/login` the user taps "¿Olvidaste tu contraseña?" and reaches `/forgot-password`; enters their email.
2. The system **always** answers with the same neutral message, whether or not the account exists.
3. If an active account exists, the system creates a single-use token (256 bits of randomness), stores only its SHA-256 hash with a 30-minute expiry, and emails a link to `{APP_ORIGIN}/reset-password?token=...`.
4. The user opens the link, chooses a new password (policy from the auth module), and submits.
5. The system validates the token (unused, unexpired), updates the password hash, marks the token used, **revokes all sessions** of the user, clears `must_change_password`, writes an audit event per business membership, and emails a "your password was changed" notice.
6. The user is redirected to `/login`.

**Alternates:** expired or used token shows a clear message and a way to request a new link; resend allowed after 60 seconds; inactive accounts get the same neutral answer and no email.

**Rules:** no account enumeration (same response and similar timing); rate limit 5 requests per hour per email and per IP; tokens never logged; the link host comes only from `APP_ORIGIN`; the reset page sets `Referrer-Policy: no-referrer`; a new request invalidates earlier unused tokens.

**Acceptance (Phase 2)**
- Given an unknown email, when requesting a reset, then the response is identical to a known email and no email is sent.
- Given a valid token, when the password is reset, then all existing refresh tokens stop working and the token cannot be reused.
- Given an expired token, when submitting, then the request fails with a stable error code and no password change.

### UC-AUTH-05 — Operator-assisted password reset

**Actor:** Support. **Preconditions:** the person cannot receive email; the Support user has `is_platform_support`.

**Flow:** Support verifies the requester using `docs/runbooks/support-identity-verification.md` (the checklist is defined by the owner of the product, not by the agent) → opens `/support/tenants/:id` → "Generar enlace de restablecimiento" → the system creates the same kind of token (15 minutes) and shows the link **once** to Support, who delivers it through the verified channel → the user completes the reset as in UC-AUTH-04.

**Rules:** Support never sees or sets a password; the action is audited with actor `SUPPORT`, the target user, and the verification note; the tenant Owner is notified by email when the target is an Owner or a member.

**Acceptance (Phase 6):** only users with `is_platform_support` can call the endpoint; the generated token works once and expires; the audit event exists.

### UC-AUTH-06 / UC-AUTH-07 — Sessions and revocation

**Flow (self):** `/profile/sessions` lists the user's sessions (device label from the user agent, approximate last activity, IP) with the current one marked. The user can revoke one session or "Cerrar sesión en los demás dispositivos".
**Flow (Owner):** in `/settings/members` the Owner can "Cerrar sesiones" of a member; deactivating a member revokes their sessions automatically.

**Rules:** the access JWT carries a session id (`sid`); the auth guard rejects a token whose session is revoked, checked against a Redis revocation set (TTL equal to the access token lifetime) so revocation takes effect **immediately**, not after 15 minutes. `sessions` gains `device_label`, `ip`, `last_seen_at`. Revocation writes an audit event.

**Acceptance (Phase 2):** Given a revoked session, when its still-valid access token is used, then the API returns 401 within one request; deactivating a member invalidates all their sessions.

### UC-AUTH-08 — Accept terms and privacy policy

**Flow:** at registration the user must tick "Acepto los Términos y la Política de privacidad" with links to `/legal/terms` and `/legal/privacy`; the system stores a `legal_acceptances` row (user, document type, version, timestamp, IP, user agent). When `LEGAL_TERMS_VERSION` or `LEGAL_PRIVACY_VERSION` changes, the next authenticated request is blocked with `LEGAL_ACCEPTANCE_REQUIRED` and the app redirects to `/legal/accept`. Verifiers created by an Owner accept at their first login.

**Rules:** the **legal text is not authored by the agent.** Documents live in `docs/legal/terms.es.md` and `docs/legal/privacy.es.md`, supplied by the product owner after legal review; until then they contain the marker `[PENDING LEGAL REVIEW]` and the build shows a non-production warning. The terms must state that VepayDO alerts and does not certify (owner to include). Acceptance is per user per version; the API allows only `GET /legal/documents`, `POST /legal/accept`, `GET /auth/me`, and logout while acceptance is pending.

**Acceptance (Phase 2):** a user who has not accepted the current version cannot call business endpoints; bumping the version re-blocks existing users; acceptance rows are immutable.

### UC-SET-06 — Grant and revoke support access

**Actor:** Owner. **Flow:** `/settings/support-access` shows the current state. The Owner chooses a duration (1 hour, 24 hours, or 72 hours), optionally a note, and taps "Permitir acceso de soporte". A `support_grants` row (tenant, granted_by, expires_at, note) is created. While active, a countdown and a "Revocar" button are shown. Every Support action during the grant appears in the business's audit log ("Soporte consultó…").

**Rules:** access is **read-only**, granted only through `SECURITY DEFINER` functions (never direct table reads), and limited to audit events, receipts metadata (the functions never return `storage_key` or images), and alerts; it expires automatically; revoking is immediate; Support cannot extend a grant.

**Acceptance (Phase 6, tested in Phase 1's RLS suite and Phase 6):** without an active grant a `support` session reads zero rows from tenant tables; with a grant it can read only through the three read-only support functions (`app.support_list_audit_events`, `app.support_list_receipts`, `app.support_list_alerts`); after expiry or revocation access stops; any write attempt fails.

### UC-OPS-01 / UC-OPS-02 — Support console and read-only access

**UC-OPS-01 (no grant needed):** `/support` lists tenants with metadata only: name, plan, status, created date, last activity, receipts this month, processing failures in the last 24 hours. Implemented through a `SECURITY DEFINER` function `app.support_tenant_overview()` that checks `app.role = 'support'` and returns aggregates, never business rows.
**UC-OPS-02 (grant required):** inside `/support/tenants/:id`, when a grant is active, Support can read the allowed data through the read-only support functions, with a persistent "Modo soporte (solo lectura)" banner. Without a grant the section shows "El Dueño no ha otorgado acceso".

**Rules:** the Support area is a separate route group guarded by `users.is_platform_support`; Support accounts are created only by a database seed or an operator script, never through the public registration; all Support requests are rate limited and audited with actor `SUPPORT`.

### UC-DATA-02 — Associate a third-party payer with a customer

**Actor:** Owner or Verifier. **Trigger:** the receipt's sender does not match the selected customer, or the sender is unknown to the business.

**Flow**
1. In the review screen or confirm sheet, after a customer or charge is selected, the system shows an inline prompt: "¿{sender} paga por {customer}?" with a toggle **"Recordar este remitente"** (default **off**).
2. If the toggle is on when the payment is confirmed, the system creates a `customer_aliases` row (normalized sender name) for that customer and writes an audit event.
3. In later receipts, the `SENDER` matching signal uses the alias and `SENDER_MISMATCH` is not raised.

**Alternates:** if the normalized sender is already an alias of **another** customer, show "Este remitente ya está asociado a {otherCustomer}" and offer to keep it (default) or, for Owners only, move it; aliases are unique per business.

**Acceptance (Phase 5):** Given an alias, when a new receipt from that sender arrives, then the customer's charges receive the sender signal and no `SENDER_MISMATCH` alert; alias uniqueness is enforced.

### UC-RCP-05 — Handle an unreadable image or a non-receipt

**Trigger:** extraction finishes without enough information, or the OCR service fails.

**Classification (`receipts.failure_reason`, enum `FailureReason`)**

| Value | Condition |
|---|---|
| `NOT_A_RECEIPT` | OCR text exists but there is no amount pattern, or no date and no known bank keyword |
| `UNREADABLE` | Very little text recognized or average confidence below 0.40 |
| `OCR_ERROR` | The OCR service returned an error after retries |
| `TIMEOUT` | The job exceeded its time limit after retries |

All four set `processing_status = FAILED` (the file is stored and never lost) and notify per UC-NTF-03.

**Flow in the UI:** the receipt appears under "Needs your decision" with a message by reason and three actions: **Completar a mano** (opens the manual-entry form with the image beside it), **Volver a tomar la foto** (opens the camera with tips; the new upload is linked in the history as a retake, not merged), and **Descartar** (rejects the receipt with reason code `NOT_A_RECEIPT` or `UNREADABLE`, no free income created).

**Rules:** classification thresholds live in one constants file and are tunable; borderline cases prefer `UNREADABLE` over `NOT_A_RECEIPT` so the person is not told their real receipt "is not a receipt"; discarded images follow the normal retention policy; the copy never blames the person (see UX spec section 9).

**Acceptance (Phase 4, with synthetic images):** a blank image gives `UNREADABLE`; an image with text but no amount gives `NOT_A_RECEIPT`; a receipt-like image proceeds to `EXTRACTED`; the failure reason is persisted and returned by the API.

---

## 3. Traceability rules for the agent

1. Every API endpoint, screen, and test added by a phase cites at least one use case ID (in the test name or a comment).
2. A use case marked `MVP` is not done until its acceptance tests exist and pass.
3. If a requested behavior matches a `DEFERRED`, `OUT`, or `V1.1` item, stop and report it; do not implement it silently.
4. New behavior discovered during implementation is proposed as a new use case in `docs/BACKLOG.md` with a suggested ID, not built.