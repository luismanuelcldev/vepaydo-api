# Prisma / PostgreSQL drift

Prisma models tables, columns, enums, relations and (simple/composite) indexes.
It does **not** model extensions, RLS policies, `FORCE ROW LEVEL SECURITY`,
CHECK constraints, functions, triggers, views, partial/trigram/covering indexes,
roles, grants, or the `app` schema. Those objects are owned by hand-written SQL
migrations (see section 7 of `AGENTS.md`).

The risk: a future `prisma migrate dev` may generate a migration that silently
drops or rewrites one of those objects because Prisma cannot see it.

## The protected list

`apps/api/scripts/protected-db-objects.json` lists every hand-written object that
must never be dropped by a generated migration. Keep it in sync whenever a new
custom index, constraint, trigger, function, view or extension is added.

## The check

`pnpm --filter @vepaydo/api db:drift-check` runs:

```
prisma migrate diff --from-schema-datasource prisma/schema.prisma \
                    --to-schema-datamodel  prisma/schema.prisma --script
```

and fails (exit 1) if the diff proposes a `DROP` on anything that is not in the
protected list. With the current schema it reports 9 `DROP INDEX` statements
(the custom indexes Prisma cannot model) and nothing else, and exits 0:

```
[drift-check] OK: 9 DROP statement(s), all on protected objects.
```

Run it in CI after `prisma migrate deploy` and after any change to
`schema.prisma`.

## Working rules

1. Never edit a migration that has already been applied. Add a new one.
2. Create SQL-only migrations with `prisma migrate dev --create-only` and edit
   the generated file, or add a timestamped folder by hand.
3. Put extensions, RLS, triggers, functions, views and grants in dedicated
   migrations named `*_rls_policies`, `*_integrity_triggers`, `*_views`,
   `*_grants`, etc.
4. When Prisma cannot model an object (partial index, trigram index, covering
   index), add it to the protected list and document it here.
5. If `db:drift-check` fails, do **not** add the object to the allowlist to make
   it pass: inspect the generated SQL, and if the drop is unintended, isolate
   the object (see below).

## If Prisma insists on dropping a protected object

Prisma drops objects it cannot model when a generated migration reconciles the
shadow database. To isolate:

- Keep the object out of the Prisma-managed migration by recreating it in a
  follow-up SQL-only migration, **and**
- add a CI step (`.github/workflows/ci.yml`) that fails when a generated
  migration contains `DROP` of a protected name.

`db:drift-check` already implements that CI step locally.
