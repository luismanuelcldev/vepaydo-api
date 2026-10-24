# ADR 0002: Use Prisma instead of Drizzle

## Status
Superseded by [ADR 0003](0003-migrate-backend-to-aspnet-core.md) (backend migrated from NestJS + Prisma to ASP.NET Core + EF Core).

## Context
The reference documents mentioned Drizzle ORM. The master prompt (AGENTS.md) overrides this decision.

## Decision
Use Prisma as the ORM (not Drizzle). Prisma's migration workflow with hand-written SQL for RLS/triggers/views is better suited for the multi-tenant security requirements with PostgreSQL.

## Consequences
- Schema defined in `schema.prisma` (declarative)
- Hand-written SQL migrations for RLS, policies, triggers, views
- Prisma Client used only in infrastructure layer
- Need to manage drift for objects not modeled by Prisma (partial indexes, triggers)
