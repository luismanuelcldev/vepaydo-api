# Assumptions and Gaps

Document assumptions made and items that require external input.

> **Note.** Sections from "Current (Phase 0)" downward describe the original
> NestJS/Prisma implementation and are kept as reference while the backend is
> ported to ASP.NET Core. The section below describes the .NET implementation.

## ASP.NET Core migration

### M0 - preparation

- The .NET solution lives in a separate repository from the NestJS reference. The
  database provider was switched from the scaffold's SQL Server to **PostgreSQL 16
  via Npgsql** because Row Level Security is a hard requirement (see
  `adr/0004-postgresql-with-rls.md`).
- The 11 original Prisma SQL migrations were copied under
  `db/reference-migrations-es/` with Spanish identifiers and will be translated to
  English identifiers into `db/migrations/` (see
  `adr/0005-reuse-sql-migrations-translated-to-english.md` and
  `schema-mapping.md`).
- **Analyzer baseline changed.** The scaffold set
  `AnalysisMode=AllEnabledByDefault`. Combined with `TreatWarningsAsErrors=true`
  that made `dotnet build` fail on stylistic/design analyzer rules (CA1032,
  CA1036, CA1062, ...) across ordinary code. `AnalysisMode` is now **`Recommended`**,
  which still runs the recommended analyzer set as errors. This is a deliberate
  deviation from the scaffold; revisit if stricter enforcement is wanted later.
- **`CentralPackageTransitivePinningEnabled=true`** is required so the
  centrally-pinned versions actually flow to transitive dependencies; without it
  the vulnerability-free build is not guaranteed.
- `FluentAssertions` is pinned to **7.2.0** (the last Apache-2.0 release; 8.x is
  under a commercial license).
- All 10 projects report **no vulnerable packages** (`dotnet list package
  --vulnerable --include-transitive`).
- **Project naming normalized to `VepayDO.*`.** The scaffold's Api project was
  named `VePayDO.Api` (capital P) while every other project used `VepayDO`. The
  Api project was renamed to `VepayDO.Api` (folder, `.csproj`, assembly name and
  solution entry) so the whole solution uses one prefix. This was also masked by
  Windows case-insensitive file paths: a reference pointed at
  `src\VePayDO.Api\VePayDO.Api.csproj`, which only resolved because of the
  filesystem's case-insensitivity and would have broken the Linux CI build. All
  `bin`/`obj` were cleaned to drop stale generated `AssemblyInfo`.

### M1 - kernel (domain primitives)

- Domain enums use **UPPER_SNAKE_CASE members** (e.g. `OWNER`, `PENDING_REVIEW`)
  so the C# name, the JSON wire value and the PostgreSQL enum label share one
  representation. `CA1707` (no underscores in identifiers) is disabled in
  `.editorconfig` for this reason.
- `Money` is a `readonly struct` over `decimal` with a bound currency. Cross-currency
  arithmetic and comparison throw `InvalidOperationException`; equality returns
  `false` instead of throwing. Comparison operators (`< <= > >=`) are implemented to
  satisfy CA1036.
- `IClock`/`SystemClock`/`FixedClock` plus `TenantTimeZone` (default
  `America/Santo_Domingo`) and `ClockExtensions.Today(zone)` replace `new Date()`
  in domain code.
- `TextNormalizer.NormalizeName` (accent/case/punctuation-insensitive) ports the
  NestJS `normalizeName`.
- `AppConfig` + `AppConfigValidator` port the Zod environment schema: all problems
  are returned at once so a misconfiguration is fixed in one pass. The API binds
  it from configuration at boot and fails fast.
- The layering dependency rule is enforced by `VepayDO.Architecture.Tests`
  (NetArchTest): Domain depends on no other layer and on no infrastructure library;
  Application depends only on Domain; Contracts on Domain; Infrastructure not on Api.

### M2 - schema translation, migrations, and the EF model

- **The Prisma / hand-written split is gone.** In the NestJS reference, Prisma
  owned tables, columns, enums and relations while hand-written SQL owned RLS,
  triggers, views and constraints. With Prisma dropped there is a single owner:
  the 11 translated SQL files under `db/migrations/`. `AppDbContext` only maps
  tables, columns, keys and column types for runtime use and **never generates
  DDL**; `dotnet ef` is not part of the workflow (`adr/0005`).
- Migrations are applied by `SqlMigrationRunner`, which reads the files in
  lexical order, runs each one as a single multi-statement batch (simple query
  protocol, because `DO`/`CREATE FUNCTION` bodies and `$$` quoting do not survive
  the extended protocol) inside its own transaction, and records the file name and
  checksum in `infra_schema_migrations`. Because EF never generates migrations,
  there is no drift check to perform; the reference `prisma-drift` runbook does
  not apply to .NET.
- **The three database roles are still cluster-level objects created outside the
  migrations.** The .NET solution has **no bootstrap script yet** (the NestJS
  `scripts/bootstrap-db.mjs` has not been ported). `PostgresFixture` creates them
  as a superuser for tests; a `scripts/` bootstrap plus runbook is required
  before deployment and is tracked as an M7 gap.
- **PostgreSQL enum labels are `UPPER_SNAKE_CASE` but C# enum names are mapped by
  Npgsql's `NpgsqlNullNameTranslator`**, which translates `Partial` → `PARTIAL`
  while leaving names without underscores untouched (`Owner` → `OWNER`). The
  default translator would emit `partial` and PostgreSQL would reject it. The
  translator is registered on the `NpgsqlDataSource` builder; the
  `NpgsqlDataSource`-level `MapEnum(name, translator)` overload takes a
  `NameTranslator`, while the EF `NpgsqlDbContextOptionsBuilder.MapEnum` overload
  takes only a schema name — so the translator cannot be set on the EF options.
- **`HasPostgresEnum` on a property does not exist for a model-level enum in this
  version.** All 15 enums are registered once inside `AppDataSource.ConfigureEnums`
  (an `Action<NpgsqlDbContextOptionsBuilder>` passed to `UseNpgsql`), which is the
  only place enum types are declared. Tests reuse that same delegate so the model
  is identical with or without a live database.
- The `audit_events.actor` column starts as `TEXT` in the initial migration and
  becomes the `audit_actor` enum in the last one; EF sees only the final state.

### M3 - tenant context and the unit of work

- `TenantContext` is a record of `Guid?`/`DbRole?`; a null component is written to
  PostgreSQL as an **empty string**, which makes `app.current_*()` return NULL and
  every policy fail closed. `DbRole.ToDatabaseValue()` produces lowercase
  `owner|verifier|system|support` (the labels `app.current_member_role()` compares
  against); `FromDatabaseValue` is deliberately case-sensitive and rejects
  anything else.
- The scope lives in an `AsyncLocal<TenantScope>` inside a **singleton**
  `TenantContextStore`. `TenantScope` is internal because it carries the scoped
  `AppDbContext`; the public surface is `ITenantContextAccessor` (read-only) and
  `TxProvider` (throws `MissingTenantContextException` when no scope is active).
  This keeps the fail-closed rule: no repository can reach the root context.
- Each unit of work opens one transaction, then issues one parameterized
  `set_config(..., true)` statement for the three settings, runs the work, and
  commits. The command timeout is set to **20 s** on the context, longer than the
  EF default, because the RLS suites and the seed inserts can be slow under
  Testcontainers. `ActivateAsync` switches the context mid-transaction (used when
  a job touches a second tenant).
- `CreateTenantAsync` calls `SELECT app.create_tenant($1, $2)` and reads the
  returned id; it deliberately runs **outside** the store, so `Accessor.Current`
  stays null afterwards (asserted by a test).
- `DbClient` in the test project opens a raw `NpgsqlConnection` and issues the
  same `set_config` calls; it exists so the RLS probes can be written in SQL
  instead of through EF.
- **EF discovered zero relationships** because the entities carry no navigation
  properties. Without them `SaveChanges` ordered the `alerts` insert before its
  `receipts` insert and the composite foreign key rejected it. All 43 foreign keys
  are now declared by hand in `AppDbContext.ConfigureRelationships`, mirroring the
  columns, principal keys (including the `(tenant_id, id)` alternate keys) and
  referential actions of the schema. `AppDbContextModelTests` compares that
  inventory against an expected list so an added or dropped constraint fails the
  build.
- **`Session.TenantId` added.** The `Session` entity now maps `sessions.tenant_id` (nullable, `DeleteBehavior.SetNull`) and a hand-declared FK was added. The previous gap has been closed. `TenantScopedEntityTypes` still intentionally excludes `sessions` and global tables (no tenant RLS).

### M4 - HTTP pipeline and security

- **Validation is one filter, not one pipeline per route.** `FluentValidation` was
  pinned from M0 but no project referenced it until M4; the API now references
  `FluentValidation` and `FluentValidation.DependencyInjectionExtensions`, while
  `Application`/`Domain` stay package-free for the layering tests.
  `FluentValidation.AspNetCore` is deprecated, so validation is a single
  `ValidationActionFilter` (`IAsyncActionFilter`) that checks model state first and
  then resolves `IValidator<T>` for every action argument; validators are
  discovered with `AddValidatorsFromAssemblyContaining<Program>()`. In
  `Api.Tests` the probe controller comes from `AddApplicationPart`, so its
  validator is registered by hand.
- **`Microsoft.AspNetCore.Authentication.JwtBearer` is pinned at 10.0.13**, newer
  than the 14-day publication window of section 13, to match the installed runtime
  (10.0.13) and the other ASP.NET Core pins in the solution. Deliberate deviation.
- **`UseHttpsRedirection` is deliberately not registered.** TLS terminates at
  Caddy in front of the app (section 15); redirecting inside the private network
  would break the container health checks.
- **Issuer and audience are `APP_ORIGIN`**, as in the reference `TokenService`
  (an origin, not a URN), so a token minted for one deployment is rejected by any
  other. The two values move together with the origin.
- **One producer per error condition.** `OnChallenge`/`OnForbidden` write the same
  `code`/`i18nKey` as the corresponding exceptions — they reference
  `UnauthorizedException.ErrorCode`/`MessageKey` and
  `ForbiddenException.ErrorCode`/`MessageKey` — so a missing token, a malformed
  token and a revoked session all read as `DOMAIN.UNAUTHORIZED` /
  `error.unauthorized`, and a missing capability reads as `DOMAIN.FORBIDDEN` /
  `error.forbidden`. Framework-generated bare statuses (404 for an unmatched
  route, 405 for a wrong verb) are turned into documents by
  `UseStatusCodePages` with the `HTTP.*` codes of `ProblemCatalog`.
- **An unauthenticated request to an unmatched route answers 401, not 404.** The
  authorization fallback policy also covers the "no endpoint matched" case, so the
  API never reveals to an anonymous caller whether a route exists. An authenticated
  caller gets the real 404/405 document (asserted by tests). This differs from the
  NestJS reference, where guards are per-route and routing answers 404 first.
- **`[Public]` implements `IAllowAnonymous` and `IAllowAnonymousFilter`.** The
  middleware-based fallback policy understands the first; the second makes the
  attribute survive an MVC-level global `AuthorizeFilter` if one is ever added.
  No global `AuthorizeFilter` is registered today.
- **`VepayDO.Api.Tests` do not need PostgreSQL.** They host the real pipeline
  through `WebApplicationFactory`, replacing only `ITenantTransactionRunner` and
  `ILegalService` with recording fakes, and point the connection string at a
  **closed port (127.0.0.1:59999)** so `/health/ready` fails deterministically even
  when a local database is listening on 5432. Consequence: the readiness failure
  path is covered, the success path is not.
- **`sessions` is bound to `tenants` in the EF model** with
  `DeleteBehavior.SetNull`, matching `sessions_tenant_binding` (nullable column,
  `ON DELETE SET NULL`); deleting a tenant unbinds the login instead of removing
  it. `Session.TenantId` closes the M3 gap, and the hand-declared foreign-key
  inventory is 43.
- **`SqlMigrationRunner` is resolvable again.** Its constructor needs the owner
  connection string, which DI could not produce; it is now registered from
  `DIRECT_URL` (falling back to `DATABASE_URL`). Nothing runs it at boot: the
  one-shot `migrate` service does at deploy time (M7). Before this fix the whole
  host failed to start, which the API tests surfaced immediately.
- **Token claim `pwd` uses the value `true`** (`TokenConstants.PasswordChangeTrue`)
  and `ClaimsMapper` compares it case-insensitively. M3 emitted `true` while the
  mapper checked for `1`, so the temporary-password gate silently never fired;
  `AccountGateTests` now pins it.
- **Coverage at the M4 close** (line coverage over the four assemblies, union of
  every test project's cobertura report): Domain 90.2%, Application 96.5%,
  Api 84.3%, Infrastructure 77.3%, overall 82% (bars: Domain ≥ 90%, overall ≥ 80%).

### M5 - master data (members M5.1, business accounts + customers M5.2)

- **List endpoints are unpaginated.** Business accounts, customers, charges and
  members are returned in full, ordered deterministically. This matches the pilot
  volumes (hundreds of rows per tenant) and mirrors the members module (M5.1).
  A cursor-based pagination contract is deferred until a list can realistically
  exceed a screenful of data; recorded in `BACKLOG.md`.
- **`TenantSettingsValues.Default` is the settings prototype** (the spec's
  `TenantSettings.Default`). The type was renamed to avoid colliding with the EF
  entity `TenantSettings`; see `PATTERNS.md` (M5.1).
- **`banks` has no treatment as tenant-scoped data.** A new read-only
  `IBankRepository` (Infrastructure) backs the business-account bank check and is
  the only reader of the global catalogue from Application code; it has no RLS
  because the table is global and seeded by migration.
- **Business accounts store only the last four digits.** `AccountLast4.Parse`
  enforces `^[0-9]{4}$` and the schema CHECK mirrors it; the full number is never
  accepted, stored, returned or logged (AGENTS.md sections 7, 13).
- **Currency is a domain value object** (`Currencies`, `DOP`/`USD`) whose values
  match the schema CHECK; the API validator, the service and the database
  constraint share one source of truth.
- **Duplicate detection is a controlled error before the constraint.** Creating a
  business account with the same `(bank, last4, currency)` in a tenant raises a
  `ConflictException` (i.e. a `409` problem document) from the service after an
  explicit existence check, so the raw unique-index violation is never surfaced.
  The unique index remains the backstop.
- **Alias uniqueness and moves (UC-DATA-02).** Adding an alias already owned by
  the same customer is idempotent; adding one owned by a **different** customer
  raises a `ConflictException` and the UI offers the move, which is gated by a
  dedicated OWNER-only permission `customers.aliasMove` (a `VERIFIER` can manage
  customers but cannot reassign aliases). `(tenant_id, alias_normalized)` is the
  unique index; the service checks it first so the constraint never fails a
  request.
- **Name/alias normalization uses `TextNormalizer.NormalizeName`** (accent-, case-
  and punctuation-insensitive) through the `CustomerName` value object, so a
  stored `name_normalized`/`alias_normalized` can never drift from the display
  value and the `SENDER` matching signal (section 9.3) reads one representation.

### M5.3 - charges (UC-DATA-03, UC-DATA-04, UC-DATA-06)

- **Charge codes are a domain value object (`ChargeCode`).** The stored form is
  uppercased, limited to `A-Z`, `0-9` and dashes, and at most 24 characters, so a
  code stays short and readable over the phone. Whitespace inside a code collapses
  to a single dash (`abc 123` -> `ABC-123`).
- **Generated codes use a readable prefix plus a zero-padded random suffix**
  (`SER-000042`). The prefix comes from the first three characters of the
  description; the suffix comes from `RandomNumberGenerator` through the
  `IChargeCodeSource` port, so tests are deterministic. The random source is
  abstracted because the code must not be guessable or predictable.
- **Uniqueness is checked before the insert and enforced by the index.** An
  explicit code that already exists in the tenant raises a `ConflictException`
  (a `409` problem document) instead of surfacing the raw unique-index violation;
  the `(tenant_id, code)` unique index remains the backstop for a concurrent
  insert.
- **The duplicate is a Prototype (`ChargeDraft.Clone()`).** Cloning clears the
  identity, the status (`OPEN`) and the allocations, and copies the descriptive
  fields into a new, independent draft. Because `record` already synthesizes a
  `Clone` member, the Prototype contract is implemented explicitly
  (`ICloneable<ChargeDraft>.Clone()`), as done for `TenantSettingsValues` in M5.1.
  The duplicate is issued with **today's date** in the tenant time zone and no due
  date: a copy of a charge is a new expectation, not a copy of the old timeline.
- **Balances are read from `v_charge_balances`, never computed on the client.** The
  view aggregates the active allocations (`voided_at IS NULL`) per charge. The
  repository reads the `charges` table and the view separately and joins them
  client-side, because EF Core cannot translate a projection that mixes a table
  entity with a view row. The view is mapped as a keyless entity
  (`ChargeBalanceRow`).
- **A voided charge is immutable.** Editing or voiding it twice raises a
  `ConflictException`; the void records the reason, the actor and the timestamp.
- **CSV import is bounded to 1 000 rows** (`ChargeCsvParser.MaxRows`) and expects
  the header `code,description,amount,currency,issued_on,due_on,customer_id`
  with `yyyy-MM-dd` dates. A malformed file is rejected with `422` and the row
  number. Per-row validation errors are reported in the response and never abort
  the valid rows, unless the caller asks for the all-or-nothing mode, in which
  case a single invalid row writes nothing. The upload is capped at 2 MB.
- **The import endpoint is `multipart/form-data`** with the file in the `file`
  part and the `allOrNothing` flag as a query parameter.
- **`PATCH /charges/{id}` cannot clear an optional field.** `null` in the body
  means "not provided", so the customer link and the due date cannot be removed
  through the partial update; a charge keeps its customer and dates once set.
  Clearing them would need an explicit "unset" marker, which is not in the MVP
  contract.
- **The status filter is strict.** An unknown `status` query value is a
  controlled `422` (the API validator and the service both reject it) instead of
  a silently ignored filter.
- **Every mutation writes exactly one audit event**, including an import that
  imports nothing (the event carries the imported/skipped counts), so the audit
  trail never loses an attempted bulk change.

## Current (Phase 0)

- Node 20.18.1 LTS
- pnpm + Turborepo
- Node.js APIs only; Python for OCR service

## Phase 1 (Database, RLS, tenant context)

- Bank-specific parser configuration files are intentionally minimal; the
  generic parser will be filled from real fixtures in a later phase. No real
  bank screen layouts, labels or reference formats were invented.
- Local development on this machine had a native PostgreSQL 18 already bound to
  port 5432, so the dev container was validated on 5433. The repository default
  stays 5432.
- The database roles (`vepaydo_owner`, `vepaydo_app`, `vepaydo_maintenance`) are
  cluster-level objects created **outside** migrations by a superuser bootstrap
  (`scripts/bootstrap-db.mjs`, `pnpm --filter @vepaydo/api db:bootstrap`). The
  bootstrap also runs `ALTER DATABASE ... OWNER TO vepaydo_owner`, so migrations
  run as the least-privilege owner and own every object they create (verified:
  `tableowner = vepaydo_owner`). Passwords come from env and are never committed.
  See `runbooks/database-bootstrap.md`.
- The `context_and_roles` migration only asserts the roles exist and fails fast
  with a clear message if the bootstrap was not run; it creates the extensions
  and the `app` schema.
- `DIRECT_URL` (owner) is required by `prisma migrate deploy` and by
  `prisma db seed`; the runtime `DATABASE_URL` (`vepaydo_app`) has no DDL and no
  write access to the global `banks` catalog.
- `pgcrypto` is not installed; receipt image hashes use `sha256` bytes produced
  in the application, and the test fixtures use SQL `md5()` pairs to build
  32-byte values.
- Prisma cannot model the hand-written objects; `db:drift-check` guards them
  (see `runbooks/prisma-drift.md`).
- `banks`, and the optional demo tenant (`SEED_DEMO_TENANT=true`), are seeded
  idempotently (`prisma/seed.ts` and the migration seed). The demo check runs in
  a `user_id` context because `tenants` has `FORCE ROW LEVEL SECURITY`, so even
  the owner cannot read it without a context (verified: two runs create one
  tenant).

## Phase 2 (Auth, tenants, members, master data)

- **Login lockout is in-process** (`LoginAttemptStore`, a `Map` keyed by the
  lowercased email) with exponential backoff (5 failures → 30 s, doubling up to
  15 min). It is a per-process safeguard; a multi-instance deployment must move
  it to Redis. The end-to-end behaviour is covered by an integration test.
- **Member onboarding uses an OWNER-issued one-time temporary password**; email
  invitations/password-reset emails are out of scope (section 16). The temporary
  password is returned once in the create response and `must_change_password` is
  set, forcing a change before any other screen is reachable.
- **Refresh cookie is set/parsed with small dependency-free helpers**
  (`shared/kernel/http/cookies.ts`) rather than `@fastify/cookie`. The plugin's
  bundled Fastify typings drifted from the app's Fastify version under pnpm's
  duplicate install, and the app only needs to set/clear/parse one opaque token.
- **The audit `SELECT` policy is OWNER-only**, while Prisma's `create` emits
  `INSERT ... RETURNING`, which PostgreSQL validates against the `SELECT` policy.
  The audit repository therefore uses `createMany` (plain INSERT, no RETURNING)
  so a VERIFIER can write audit rows (e.g. `change_password`).
- **CSRF protection on cookie endpoints is an `OriginGuard`** (checks `Origin` /
  `Referer` against `APP_ORIGIN`) on `select-tenant` and `refresh`; access-token
  endpoints rely on the `Authorization` header.

### Phase 2 gap closure (recovery, sessions, legal)

- **Session revocation uses Redis when `REDIS_URL` is set, otherwise an
  in-process `InMemorySessionRevocationStore`.** Redis is optional in the env
  schema (an empty `REDIS_URL` is treated as unset). The in-memory store is
  correct for tests and single-instance development, but a multi-instance
  deployment **must** set `REDIS_URL`, otherwise revocation is not shared across
  processes. Documented here and in `BACKLOG.md`.
- **The `LegalAcceptanceGuard` also exempts routes flagged
  `@AllowPasswordChangeRequired()`** in addition to `@AllowPendingLegal()` and
  public routes. Without this, a user who must change a temporary password *and*
  accept the documents would be locked out of both flows (deadlock).
- **The legal acceptance check reads `legal_acceptances` through the raw
  `PrismaService`, not a tenant transaction.** The table is global (no RLS) and
  the guard runs before the request transaction is opened. It costs one extra
  read per authenticated request; caching the accepted-version set (e.g. a `pwd`
  / acceptance claim in the access token or a short-lived cache) is a future
  optimization recorded in `BACKLOG.md`.
- **`sessions`, `password_reset_tokens`, and `legal_acceptances` are global
  tables without RLS** (like `users`), so recovery and cross-user session
  revocation work before/without a tenant context. `support_grants` and
  `ux_events` *do* have tenant RLS.
- **Legal documents are placeholders** (`docs/legal/terms.es.md`,
  `privacy.es.md`) carrying the `[PENDING LEGAL REVIEW]` marker. Non-production
  responses expose `pendingReview: true` so the UI can warn. The agent never
  authors legal text (section 9.9).
- **Recovery/reset work end to end with `MAIL_TRANSPORT=fake`** in tests: the
  `FakeMailSender` captures the email and the test extracts the reset link. Real
  SMTP is selected by `MAIL_TRANSPORT=smtp` (nodemailer); Mailpit is provided in
  `docker-compose.dev.yml`.
- **The `Support` console API is deferred to Phase 6.** Phase 1/2 shipped only
  the schema (`support_grants`, `users.is_platform_support`), the `SECURITY
  DEFINER` support functions, and the RLS support-access tests.
- **Integration tests neutralize rate limiting at the `ThrottlerStorage`
  provider** rather than by stubbing `ThrottlerGuard`: the app guards are
  registered with `APP_GUARD` + `useClass`, which `overrideGuard` cannot replace
  by class token. The storage stub never blocks, so the real auth guards stay in
  force.

## Pending

- Real bank screenshots/fixtures required for bank-specific parsers
- Credentials (DB, Redis, S3, FCM) - to be provided in environment
- Legal review (Ley 172-13, DGII, terms of use) - human decision
- Name clearance (ONAPI, domains) - human decision
- Retention periods are proposed defaults pending legal review (images 24
  months, raw OCR 12 months, logs 90 days).

## Phase 0 close-out (design tokens, fonts, Logo, shadcn, tests)

- Design tokens are centralized in `packages/config/tailwind/` (tokens.mjs + preset.mjs) and exported as CSS variables; Tailwind is wired via `presets: [preset]`.
- Fonts are self-hosted via `@fontsource/poppins` (600,700) and `@fontsource/plus-jakarta-sans` (400,500,700), Latin subset. The two critical weights (Poppins 700, Plus Jakarta Sans 400) are preloaded via a Vite build plugin that injects `<link rel="preload" as="font">` for the emitted font assets. No external font CDN in web sources.
- Brand name lives in one constant (`BRAND_NAME`, `packages/shared`). The HTML `<title>`, the PWA manifest (`name`/`short_name`) and the i18n copy (`{{brand}}`) derive from it.
- The favicon is a placeholder SVG (`apps/web/public/favicon.svg`) derived from the `Logo` mark; it will be replaced by the owner-provided brand assets before the pilot.
- `<Logo>` component (`apps/web/src/components/brand/Logo.tsx`) enforces variants `full|mark|mono|inverse` with size minimums; until owner assets exist, a placeholder mark + wordmark are used.
- Automated WCAG contrast test (`apps/web/src/__tests__/tokens.spec.ts`) validates the UX �3.1 pairs; a token/CSS drift test ensures `tokens.css` is in sync with `tokens.mjs`.
- shadcn/ui foundation is installed (CVA, tailwindcss-animate, lucide-react, @radix-ui/react-slot); base UI components `Button/Input/Card/Badge` are styled with design tokens under `components/ui`.
- `Money.round(decimalPlaces = 2, mode?)` was added and tested in `apps/api/src/shared/kernel/money/money.spec.ts` (covers rounding and preserves currency).
