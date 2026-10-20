# Documentation index

| Path                             | Content                                                                                    |
| -------------------------------- | ------------------------------------------------------------------------------------------ |
| `prompts/PHASES.md`              | Execution prompts, Phases 0 to 8                                                           |
| `reference/product/`             | Product and architecture document (Markdown and Word)                                      |
| `reference/schema/`              | Reference SQL schema (Spanish identifiers; translated to English Prisma models in Phase 1) |
| `reference/real-receipts/`       | Real receipt captures (git-ignored, never commit)                                          |
| `adr/`                           | Architecture decision records                                                              |
| `runbooks/`                      | Operational runbooks (see below)                                                           |
| `runbooks/prisma-drift.md`       | Keeping Prisma from dropping hand-written SQL objects                                      |
| `runbooks/boundary-check.md`     | Verifying the layering (domain must not import Prisma)                                     |
| `runbooks/database-bootstrap.md` | Creating the DB roles and `vepaydo_owner` database ownership before migrations             |
| `schema-mapping.md`              | Reference-schema (Spanish) to target (English) mapping                                     |

`runbooks/prisma-drift.md` and `runbooks/boundary-check.md` document the NestJS
reference implementation; in .NET the equivalents are "EF never generates
migrations" and the `VepayDO.Architecture.Tests` project. `ARCHITECTURE.md`
describes the .NET solution; `ASSUMPTIONS.md` and `BACKLOG.md` carry both the
reference phases and the `.NET migration` milestones.

Files created by the agent during the phases: `ARCHITECTURE.md`, `PATTERNS.md`, `ASSUMPTIONS.md`, `BACKLOG.md`, `schema-mapping.md`, `SECURITY-CHECKLIST.md`.
