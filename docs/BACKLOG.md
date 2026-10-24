# Backlog

Track work items. No TODOs without linked entries here.

## ASP.NET Core migration (this repository)

- [x] M0 - repository, .NET 10 toolchain, central package versions, analyzers,
      ported `docs/`, ADR 0003/0004/0005, `.env.example`
- [x] M1 - domain kernel (`Money`, `IClock`, `TextNormalizer`, enums, errors) +
      `AppConfig`/`AppConfigValidator` and the layering test suite
- [x] M2 - 11 SQL migrations translated to English, `SqlMigrationRunner`,
      RLS/integrity + schema suites on Testcontainers, `AppDbContext` with the 15
      PostgreSQL enums and all 43 hand-declared foreign keys
- [x] M3 - tenant context (`DbRole`, `TenantContext`, `TenantContextStore`,
      `TxProvider`, `TenantContextAccessor`, `TenantTransactionRunner`) + tests
- [x] M4 - HTTP pipeline and security: problem-details kernel (`ProblemJson`,
      `ProblemCatalog`, `ProblemDetailsExceptionHandler`), `ValidationActionFilter`
      over FluentValidation, `OriginGuard`, JWT bearer with the `sid` revocation
      check, `AccessAuthorizationHandler` + one policy per capability, account
      gates (temporary password, legal acceptance), `TenantContextMiddleware` (one
      transaction per request), `/health/live` + `/health/ready`, route-prefix
      convention, and `VepayDO.Api.Tests` (33 tests) over `WebApplicationFactory`
- [ ] M5 - business modules (auth, tenants, members, accounts, customers,
      charges, receipts, alerts, allocations, audit, reconciliation, settings)
      - [x] M5.1 - tenant settings (get/update) + members (list/create/patch/
            revoke-sessions), temp password + last-active-owner guard
      - [x] M5.2 - business accounts (UC-SET-03) + customers and aliases
            (UC-DATA-01/02) with the last4-only and alias-uniqueness rules
      - [x] M5.3 - charges (UC-DATA-03/04/06): CRUD, tenant-unique code
            generation, void with reason, duplicate via the `ChargeDraft`
            prototype, CSV import with a per-row report, balances read from
            `v_charge_balances`
- [ ] M6 - storage, OCR service, matching engine, report builders
- [ ] M7 - Docker/Caddy, CI/CD, backups, e2e, bundle and i18n checks

### Migration gaps to close

- [x] Port the database bootstrap script (create `vepaydo_owner` /
      `vepaydo_app` / `vepaydo_maintenance` + `ALTER DATABASE ... OWNER`) and its
      runbook. Done: `scripts/bootstrap-db.ps1` (Windows) and
      `scripts/bootstrap-db.sh` (Linux/CI), documented in
      `runbooks/database-bootstrap.md`; `PostgresFixture` still performs the
      same bootstrap for tests.
- [x] Add `Session.TenantId` to the EF entity and the `sessions_tenant_id_fkey`
      relationship (`DeleteBehavior.SetNull`, matching the migration), so sessions
      can be filtered by tenant. Done with M4.
- [ ] Run `SqlMigrationRunner` from the one-shot `migrate` service at deploy time
      (M7): it is registered in DI with the owner connection string but nothing
      executes it at boot, by design (runtime services never receive owner
      credentials).
- [ ] Replace `InMemorySessionRevocationStore` and `InMemoryLoginAttemptStore`
      with Redis-backed implementations before running more than one API
      instance; both are process-local singletons today (M7).
- [ ] Introduce cursor-based pagination for list endpoints (business accounts,
      customers, charges, members) once pilot volumes make a full list
      unrealistic; today every list is returned in full, ordered
      deterministically (M5.2 decision, see `ASSUMPTIONS.md`).

## Phase 0 - Foundation

- [x] Monorepo skeleton with pnpm + Turborepo
- [x] packages/config (tsconfig, eslint with boundaries, prettier)
- [x] packages/shared (enums, schemas, constants)
- [x] apps/api kernel (AppConfig, Clock, Money, errors, filters, logger)
- [x] apps/web (Vite + React + i18n + Tailwind)
- [x] services/ocr (FastAPI skeleton)
- [x] docs skeletons
- [x] CI workflow
- [x] docker-compose.dev.yml
- [x] .env.example
- [x] Design tokens as CSS variables + Tailwind preset; automated contrast and
      token/CSS drift tests
- [x] Self-hosted fonts (Fontsource, Latin) with the two critical weights
      preloaded via a Vite plugin; no external font CDN
- [x] `Logo` component (placeholder mark) and shadcn/ui base components
      (Button/Input/Card/Badge)
- [x] Brand name from a single constant (`BRAND_NAME`): HTML title, PWA manifest
      and i18n copy derive from it
- [ ] Replace the placeholder favicon and logo with owner-provided brand assets
      (SVG full/mark/mono/inverse, PWA icons 192/512 + maskable, favicon, Apple
      touch 180, splash, Open Graph image)

## Phase 1 - Database, RLS and tenant context

- [x] schema.prisma translated to English models over Spanish tables
- [x] Migrations: context/roles, init, integrity constraints, triggers,
      indexes, views, RLS policies, create_tenant + grants + bank seed
- [x] Database roles + ownership via superuser bootstrap (`db:bootstrap`); migrations run as `vepaydo_owner`
- [x] AppConfig validation, Clock, PrismaService + MaintenancePrismaService singletons
- [x] Tenant context: TenantContextStore, TenantTransactionRunner, runAsUser,
      runAsSystem, createTenant, TxProvider (fail closed)
- [x] Idempotent seed (banks catalog + optional demo tenant)
- [x] RLS + integrity integration suite (Testcontainers): per-table read/write
      isolation, DELETE denied, and a mutation check that drops a policy
- [x] Prisma drift check script + runbook
- [x] DB bootstrap script + runbook (`runbooks/database-bootstrap.md`)
- [x] Singleton identity tests for PrismaService/AppConfig/Clock/PermissionPolicy (`apps/api/test/singleton.spec.ts`)

## Phase 2 - Identity, tenants, members, settings and master data

- [x] Prisma schema additions: `users.must_change_password`, `sessions.tenant_id`
- [x] Auth module: register, login, select-tenant, refresh (rotation + reuse
      detection), logout, me, change-password, locale preference
- [x] HTTP kernel: `ZodValidationPipe` (+ `ZodBody/Param/Query`), `OriginGuard`
      (CSRF), `JwtAuthGuard` honoring `@Public`, `AuthorizationGuard` (capability
      policy) with the `must_change_password` gate, transactional
      `TenantContextInterceptor`
- [x] Security: argon2id `PasswordHasher`, `TokenService` (access JWT + hashed
      rotating refresh token), in-process `LoginAttemptStore`
- [x] Members module (OWNER): create VERIFIER with one-time temporary password,
      list, update role/active with last-active-owner guard
- [x] Business-accounts module (OWNER): list/create/update
- [x] Customers module: list/get/create/update, aliases
- [x] Tenants settings: get/upsert (`Prototype` defaults) + audit
- [x] Global prefix `/api/v1`, error filter with problem details, Swagger (non-prod)
- [x] API integration suite over HTTP (Testcontainers + supertest): register,
      refresh rotation/reuse, origin guard, lockout, owner-only guard, verifier
      must-change-password, two-tenant isolation, select-tenant (multi-membership),
      logout revocation, members update + last-active-owner guard
- [x] Capability routes: `@RequirePermission(Permission.X)` + `AuthorizationGuard`
      over `PermissionPolicy`, re-asserted in use cases (defense in depth);
      replaced `@Roles`/`RolesGuard`
- [x] Response Zod schemas in `packages/shared` + contract test
      (`apps/api/test/contract/shared-schemas.spec.ts`)
- [x] Fixed `@ZodParam(name, schema)` (was validating the whole params object)

### Phase 2 gap closure (recovery, sessions, legal, support schema)

- [x] Schema + migration `support_legal_ux_sessions`: password recovery tokens,
      legal acceptances, `ux_events`, `support_grants` (+ RLS and support
      functions), session columns (`device_label`, `ip`, `last_seen_at`),
      `eventos_auditoria.actor` as `AuditActor`
- [x] `MailSender` port + SMTP (nodemailer) and Fake senders; Mailpit in dev;
      `MAIL_*`/`LEGAL_*` env validation
- [x] `SessionRevocationStore` (Redis when `REDIS_URL` set, in-memory fallback)
      wired into the JWT guard via the `sid` claim
- [x] Password recovery (UC-AUTH-04/05): forgot/reset endpoints, neutral
      responses, per-email/IP limits, single-use hashed tokens, all sessions
      revoked on use, "password changed" email
- [x] Sessions (UC-AUTH-06/07): list/revoke/revoke-all endpoints, owner
      `POST /members/:userId/revoke-sessions`, revocation on deactivation
- [x] Legal acceptance (UC-AUTH-08): `legal` module, guard, placeholder
      documents marked `[PENDING LEGAL REVIEW]`
- [x] Integration tests for recovery, sessions and the legal gate

## Cross-cutting / CI (to add with Phase 7)

- [ ] i18n key-parity check: CI fails when a key exists in one locale but not the
      other (`AGENTS.md` section 12.4)
- [ ] Bundle-size check: CI fails when the initial route JS exceeds 200 KB gzipped
      (`AGENTS.md` section 12.3)

## Known limitations / future optimizations

- [ ] **Multi-instance auth state**: `LoginAttemptStore` is in-process. Move both
      the login lockout and (already optional) session revocation to Redis for any
      deployment with more than one API/worker instance (`AGENTS.md` section 9.9).
- [ ] **Legal acceptance check per request**: the guard reads `legal_acceptances`
      on every authenticated request. Cache the accepted-version set (access-token
      claim or short TTL cache) to avoid the extra query.
- [ ] **Support console API**: only schema, functions and RLS tests shipped in
      Phase 1/2. Implement the endpoints in Phase 6.
- [ ] **`@vepaydo/shared` ESM build**: `dist` emits extensionless ESM specifiers
      without `"type": "module"`, so Node 24 cannot import it directly. Fix before
      any Node consumer imports the built package at runtime.
