# Architecture

VepayDO is a modular monolith. This document describes the **ASP.NET Core
implementation**, which is a ground-up port of the reference NestJS/Prisma
monorepo kept as a separate reference repository (see `adr/0003`). The reference
is read-only input: where the two disagree, `AGENTS.md` and the ADRs decide.

## Solution layout

```
VePayDO/
├─ src/
│  ├─ VepayDO.Api              HTTP host (still the project template; M4 wires it)
│  ├─ VepayDO.Application      use cases, ports, tenancy contracts, configuration
│  ├─ VepayDO.Contracts        shared DTOs (empty; reserved for M4+)
│  ├─ VepayDO.Domain           enums, value objects, errors, text/time kernel
│  └─ VepayDO.Infrastructure   EF Core, SQL migrations, tenancy runtime
├─ tests/
│  ├─ VepayDO.Domain.Tests            pure kernel (39)
│  ├─ VepayDO.Application.Tests       configuration + tenancy contracts (31)
│  ├─ VepayDO.Infrastructure.Tests    Testcontainers PostgreSQL (34)
│  ├─ VepayDO.Architecture.Tests      layering rules (7)
│  └─ VepayDO.Api.Tests               empty until M4
├─ db/
│  ├─ migrations/             11 English SQL migrations - the schema's only owner
│  └─ reference-migrations-es/  Spanish source, parked, never executed
├─ docs/                      this file, ASSUMPTIONS, ADRs, runbooks, product docs
├─ infra/                     empty until M7
└─ .github/                   empty until M7
```

Package scope is `VepayDO.*`. Everything is `net10.0`, C# `strict` nullable, with
`Directory.Build.props` enforcing `TreatWarningsAsErrors` and
`EnforceCodeStyleInBuild`, and `Directory.Packages.props` as the single package
version authority.

## Layering

Dependencies flow inward only:

```
Api ──> Application ──> Domain
 │           ^             ^
 │           │             │
 └──> Infrastructure ──────┘  (implements Application ports, owns EF)
```

- `Domain` references no other project and no infrastructure library. It is pure:
  no I/O, no EF, no ASP.NET.
- `Application` references only `Domain`. Ports (repository/service interfaces)
  live here; implementations live in `Infrastructure`.
- `Infrastructure` may not reference `Api`.
- `Api` may reference `Application`, `Contracts` and `Infrastructure`.
- `@prisma/client`-equivalent rule: EF types are confined to `Infrastructure`;
  `VepayDO.Domain.Enums` (the shared vocabulary) is the only model vocabulary the
  upper layers see.

`VepayDO.Architecture.Tests` (NetArchTest) turns every one of those statements
into a failing test, plus a `no-restricted-imports` equivalent for
`Microsoft.EntityFrameworkCore` outside `Infrastructure`.

## Key principles

- Multi-tenant: isolation is enforced by PostgreSQL Row Level Security, not by
  application code alone. RLS is the last line of defense; guards come first.
- English code, database objects, comments, commits and docs. UI copy is Spanish
  through i18n (frontend, out of scope here).
- Money is never a `double`. `Money` is a `readonly struct` over `decimal` with a
  bound currency; mixed-currency arithmetic throws.
- Every external boundary (HTTP body, queue payload, OCR response, environment)
  is validated before it reaches the domain; `AppConfigValidator` reports all
  environment problems at once and the host refuses to boot on any.
- Time is read through `IClock` plus the tenant time zone
  (`America/Santo_Domingo`); domain code never calls `DateTime.UtcNow` directly.
- Domain enums use `UPPER_SNAKE_CASE` members so the C# name, the JSON value and
  the PostgreSQL enum label are one string (`CA1707` is disabled for that reason).
- Creational patterns (Factory Method, Abstract Factory, Builder, Prototype,
  Singleton) are introduced only where they remove real variation, documented in
  `PATTERNS.md`, and land with the features that need them (M5/M6). None is used
  yet.

## Database

PostgreSQL 16 with `pg_trgm` and `citext`. **The schema has exactly one owner:
the SQL files under `db/migrations/`.**

| Concern                     | Owner                                                    |
| --------------------------- | -------------------------------------------------------- |
| Tables, columns, enums, keys, plain indexes | `db/migrations/*.sql`   |
| Extensions, `app` schema and helper functions | `db/migrations/*.sql` |
| CHECK constraints, partial/trigram/covering indexes | `db/migrations/*.sql` |
| Triggers (updated_at, allocation validation, verifier rules, audit immutability) | `db/migrations/*.sql` |
| Views (`v_charge_balances`, `v_daily_reconciliation`, `security_invoker`) | `db/migrations/*.sql` |
| RLS policies, `FORCE ROW LEVEL SECURITY`, role grants, `app.create_tenant`, support functions | `db/migrations/*.sql` |
| EF Core model (tables, columns, keys, relationships) | `AppDbContext` (never generates DDL) |

Migration order is lexical by file name: `context_and_roles` → `init` →
`integrity_constraints` → `integrity_triggers` → `indexes` → `views` →
`rls_policies` → `create_tenant_grants_seed` → `users_must_change_password` →
`sessions_tenant_binding` → `support_legal_ux_sessions`.

`SqlMigrationRunner` reads that directory, executes each file as one
multi-statement batch (simple query protocol, because `$$`-quoted bodies do not
survive the extended protocol) inside its own transaction, and records file name
plus checksum in `infra_schema_migrations`. `dotnet ef database update` is never
used; there is no drift check because EF never generates migrations.

### Roles and connections

Roles are cluster-level objects and are **not** created by migrations. Tests
create them as a superuser in `PostgresFixture`, mirroring the production
bootstrap. **The .NET bootstrap script has not been ported yet** (M7 gap; the
reference is `scripts/bootstrap-db.mjs`).

| Role                  | Connection             | Used by                                    |
| --------------------- | ---------------------- | ------------------------------------------ |
| `vepaydo_owner`       | owner connection string | `SqlMigrationRunner` only; owns all objects |
| `vepaydo_app`         | `ConnectionStrings:DefaultConnection` | API + worker (`NOSUPERUSER NOBYPASSRLS`) |
| `vepaydo_maintenance` | maintenance connection  | retention module only (`BYPASSRLS`)        |
| (superuser)           | admin connection       | role/database creation only, never runtime |

## Tenant context and the unit of work

The whole fail-closed design is one invariant: **outside a tenant transaction,
`app.current_tenant_id()` returns NULL, and every tenant policy therefore matches
zero rows.**

```
request/job
  └─ TenantContextStore.RunAsync(scope, work)      singleton, AsyncLocal<TenantScope>
       ├─ AppDbContext.Database.BeginTransactionAsync()
       ├─ SELECT set_config('app.tenant_id', $1, true),   -- parameterized
       │        set_config('app.user_id',   $2, true),
       │        set_config('app.role',      $3, true)
       ├─ work(...)   repositories resolve through TxProvider
       └─ COMMIT
```

- `TenantContext` (`Application`) is `(Guid? TenantId, Guid? UserId, DbRole? Role)`.
  A null component is written as an **empty string**. `DbRole.ToDatabaseValue()`
  yields `owner|verifier|system|support`, the labels
  `app.current_member_role()` compares against.
- `TenantContextStore` (`Infrastructure`, singleton) holds the scope in an
  `AsyncLocal`. It is internal to the assembly: the public surface is
  `ITenantContextAccessor` (read the current context) and `TxProvider`
  (resolve the context-bound `AppDbContext`).
- `TxProvider` throws `MissingTenantContextException` when no scope is active, so
  no repository can fall back to a tenant-less connection.
- `TenantTransactionRunner` implements `ITenantTransactionRunner` with
  `RunAsync`, `RunAsUserAsync` (pre-auth flows: login, tenant selection),
  `RunAsSystemAsync` (background re-evaluation), `ActivateAsync` (switch tenant
  inside a running transaction) and `CreateTenantAsync`
  (`SELECT app.create_tenant($1, $2)`, run outside the store).
- Roles exposed as `app.role`: `owner`, `verifier`, `system`, `support`.
  Database triggers enforce the permission matrix (owner-only revert, verifier
  amount cap, no confirmation with an open critical alert) as a second line of
  defense.

One HTTP request will be exactly one such transaction, wired by the M4
`TenantContextInterceptor` equivalent; the worker will use the same runner per
job.

## EF Core model

`AppDbContext` maps every table to a hand-written entity in
`Persistence\Entities`. Entities deliberately have **no navigation properties**,
which has two consequences that are guarded by tests:

1. **All 43 foreign keys are declared by hand** in
   `AppDbContext.ConfigureRelationships`, mirroring the schema's columns,
   principal keys (including the composite `(tenant_id, id)` alternate keys) and
   referential actions. Without them `SaveChanges` would order inserts
   arbitrarily — a child could be written before its parent. `AppDbContextModelTests`
   compares the model against the full expected inventory.
2. No `Include`/navigation fix-up; reads project to explicit column selections.

PostgreSQL enums are registered once in `AppDataSource.ConfigureEnums` (passed to
`UseNpgsql`) with `MapEnum<T>("pg_type_name")`, and the `NpgsqlDataSource` is
built with `NpgsqlNullNameTranslator` so `Partial` maps to the `PARTIAL` label.
See `ASSUMPTIONS.md` (M2) for why the translator cannot be set on the EF options.

## Testing

| Project                    | Scope                                                        | Tooling |
| -------------------------- | ------------------------------------------------------------ | ------- |
| `VepayDO.Domain.Tests`     | `Money`, normalizers, clock/time, errors, enums              | xunit   |
| `VepayDO.Application.Tests`| `AppConfigValidator`, `TenantContext`/`DbRole` contracts     | xunit + FluentAssertions |
| `VepayDO.Infrastructure.Tests` | migrations, RLS/integrity, EF model, tenant runtime      | xunit + Testcontainers (`postgres:16-alpine`) + Npgsql + EF Core |
| `VepayDO.Architecture.Tests` | layering and import rules                                  | xunit + NetArchTest |
| `VepayDO.Api.Tests`        | HTTP pipeline (M4)                                           | xunit + `WebApplicationFactory` |

`PostgresFixture` creates the cluster-level roles as a superuser, creates the
database owned by `vepaydo_owner`, applies every migration as the owner, and
exposes three connection strings (owner / app / maintenance). Integration tests
run as `vepaydo_app`, so RLS is active and the assertions prove isolation rather
than trust it.

Run everything with `dotnet test VePayDO.slnx` (one project per `dotnet test`
invocation is not supported by the CLI).

## Status

| Milestone | Scope                              | State  |
| --------- | ---------------------------------- | ------ |
| M0        | repo, toolchain, package pins      | done   |
| M1        | domain + application kernel        | done   |
| M2        | schema translation, runner, RLS suite, EF model | done |
| M3        | tenant context and unit of work    | done   |
| M4        | HTTP pipeline and security         | done   |
| M5        | business modules                   | pending |
| M6        | storage, OCR, matching, reconciliation | pending |
| M7        | infra, CI/CD, e2e, docs            | pending |
