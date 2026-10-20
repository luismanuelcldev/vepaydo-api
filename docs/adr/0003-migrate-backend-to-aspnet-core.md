# ADR 0003: Migrate the backend from NestJS + Prisma to ASP.NET Core + EF Core

## Status
Accepted

## Context
The initial implementation of the VepayDO backend was a NestJS + Prisma monorepo
(`C:\Mis proyectos\VePayDO`). Only Phases 0–2 were built there (shared kernel,
PostgreSQL schema with RLS, and the modules `auth`, `members`, `tenants/settings`,
`business-accounts`, `customers`, `audit`, `mail`, `legal`). Phases 3–8 were never built.

The team decided to continue the product on ASP.NET Core (.NET 10) instead of NestJS.

## Decision
- The ASP.NET Core solution in this repository becomes the canonical backend.
- The NestJS monorepo is kept as the **behavioural reference** (use cases, tests,
  SQL schema, product/UX docs) but is no longer developed.
- The migration ports the existing Phases 0–2 behaviour, then builds Phases 3–8
  directly in .NET.
- ORM: **EF Core 10** for the runtime model; RLS, triggers, functions, views, roles,
  and constraints remain hand-written SQL applied by a small idempotent runner (see ADR 0005).

## Consequences
- All stack-specific documentation switches to .NET (see `AGENTS.md`).
- Domain rules, enums, security model, and scope are unchanged from the master prompt.
- The `.NET` scaffold's original SQL Server provider is replaced (see ADR 0004).
- ADR 0002 (Prisma instead of Drizzle) is superseded.
