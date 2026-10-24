# Design Patterns

Creational patterns from refactoring.guru (used where they fit). Each usage is documented with problem, participants, rationale, and test evidence.

> **Note.** Table rows and sections referencing `PrismaService`, Nest modules and
> `apps/api` paths describe the original NestJS implementation and are kept as
> reference. The `.NET implementation` section below covers the ASP.NET Core port.

## .NET implementation

| Pattern          | Status      | Usage                                                                 |
| ---------------- | ----------- | --------------------------------------------------------------------- |
| Factory Method   | planned     | Receipt parsers per bank                                              |
| Abstract Factory | planned     | Storage/extraction toolkits                                           |
| Builder          | implemented | AuditEvent (`VepayDO.Application.Audit`); ParsedReceipt/Alert/Report planned |
| Prototype        | implemented | `TenantSettingsValues.FromPrototype` (M5.1); `ChargeDraft` (M5.3); `ReceiptSnapshot` later |
| Singleton        | implemented | `IClock` -> `SystemClock`, `AppConfig` (via DI container)             |

- **Singleton (DI only).** `SystemClock` and the validated `AppConfig` are
  registered as DI singletons in the composition root; no `static getInstance()`
  and no mutable module-level globals (AGENTS.md section 8.5).
- **Prototype.** `ICloneable<out T>` (`VepayDO.Domain.Common.ICloneable`) is the
  contract; implementations must return a deep, independent copy.
  `TenantSettingsValues.Default` is the prototype and `FromPrototype()` returns a
  fresh, independent copy (M5.1). `ChargeDraft` (M5.3) is the prototype for the
  duplicate-charge use case (UC-DATA-04); `ReceiptSnapshot` arrives in M5.5.

| Pattern          | Status      | Usage                                                                                                 |
| ---------------- | ----------- | ----------------------------------------------------------------------------------------------------- |
| Factory Method   | planned     | Receipt parsers per bank                                                                              |
| Abstract Factory | planned     | Infrastructure toolkits (storage, extraction)                                                         |
| Builder          | implemented | AuditEvent (audit module); ParsedReceipt/Alert/Report builders planned                                |
| Prototype        | implemented | TenantSettings (tenants/settings module)                                                              |
| Singleton        | implemented | PrismaService, MaintenancePrismaService, AppConfig, CLOCK, TokenService, PasswordHasher, SessionRevocationStore, MailSender (via Nest DI) |

## Singleton (phase 1 + 2)

- `AppConfig`: validated frozen env and frozen instance (section 8.5)
- `PrismaService`: single `PrismaClient` for `vepaydo_app`
- `MaintenancePrismaService`: restricted client for `retention` module (lint-restricted)
- `CLOCK`: injected Clock
- `TokenService`: JWT + refresh token utilities (single instance)
- `PasswordHasher` (token `PASSWORD_HASHER`): argon2id hasher
- `SessionRevocationStore` (token `SESSION_REVOCATION`): one revocation store per
  process (`RedisSessionRevocationStore` when `REDIS_URL` is set, otherwise
  `InMemorySessionRevocationStore`)
- `MailSender` (token `MAIL_SENDER`): one sender per process, selected from
  `MAIL_TRANSPORT` by the `@Global()` `MailModule` factory (`SmtpMailSender` or
  `FakeMailSender`)
- `PermissionPolicy`: executable permissions matrix (section 9.5), provided by the global `PermissionModule`
- All via Nest default singleton scope (no `static getInstance()`)
- Test evidence: `apps/api/test/singleton.spec.ts` resolves each provider twice and asserts identity (AppConfig, PrismaService, CLOCK, PermissionPolicy); also asserts `AppConfig` is frozen. The revocation store's Redis/in-memory selection is exercised through the API integration suite (an empty `REDIS_URL` selects the in-memory store).

## Prototype (phase 2 / M5.1)

`TenantSettings` exposes `static DEFAULTS` and `static fromPrototype()` producing a deep, independent copy (no shared mutable references). Tests assert mutating the copy does not affect the prototype.

**.NET port (M5.1).** `TenantSettingsValues` (`VepayDO.Domain.Tenants`) is a
`record` implementing `ICloneable<TenantSettingsValues>` explicitly (the
compiler-synthesized record copy would mask the contract; the explicit
`Clone()` keeps the intent visible). `TenantSettingsValues.Default` is the
prototype (7 / 0.00 / null / 24) and `FromPrototype()` returns a fresh,
independent copy plus `Validate()` enforces the ranges. Test evidence:
`tests/VepayDO.Domain.Tests/Tenants/TenantSettingsValuesTests.cs` asserts that
mutating a `FromPrototype()` copy leaves `Default` unchanged.

**Charge duplicate (M5.3).** `ChargeDraft` (`VepayDO.Domain.Charges`) is a
`record` implementing `ICloneable<ChargeDraft>` explicitly, for the same reason as
`TenantSettingsValues`: the compiler-synthesized record copy would otherwise mask
the Prototype contract. `Clone()` returns a draft with a **cleared identity** (no
`Id`), the **cleared status** (`OPEN`) and **no allocations**, while the
descriptive fields (code, description, amount, dates, customer) are copied into
an independent instance. The clone is the input of the duplicate-charge use case
(UC-DATA-04); the caller assigns the new unique code. Test evidence:
`tests/VepayDO.Domain.Tests/Charges/ChargeDraftTests.cs` asserts that the clone
has no identity, no allocations, an `OPEN` status, and that mutating the copy
leaves the original untouched.

## Builder (phase 2)

`AuditEventBuilder` (domain builder) builds immutable audit events step by step; `AuditService` writes them inside the caller's transaction. Rationale: audit construction is explicit and testable; immutability is enforced by the builder. Test evidence: `apps/api/src/modules/audit/domain/audit-event.spec.ts`.

## Authorization policy (phase 2)

`PermissionPolicy` is the single executable form of the permissions matrix (section 9.5). Routes declare the required capability with `@RequirePermission(Permission.X)` and `AuthorizationGuard` enforces it; use cases re-assert through the same policy (defense in depth). Adding a capability is a change in one file, not in scattered role checks. `@Roles`/`RolesGuard` were removed in favor of this.

## Factory Method / Abstract Factory / Builder (planned)

- Factory Method: parser creators per bank (phase 4)
- Abstract Factory: storage/extraction toolkits (phase 3/4)
- Builder: ParsedReceipt, Alert, Report builders (phase 4-6)
