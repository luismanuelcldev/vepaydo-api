# Boundary Violation Check

This repository enforces strict layering: `@prisma/client` must only be imported from `infrastructure/` or `shared/`. The `domain/` layer must not import Prisma.

## Verification

To verify the boundary rule fails as expected, run ESLint against the intentional violation fixture:

```bash
pnpm --filter @vepaydo/api exec eslint --no-ignore src/__boundary-test__/.domain-import-prisma.ts
```

The fixture is excluded from the normal `pnpm lint` run via `boundaries/ignore`/ESLint
global ignores, so `--no-ignore` is required to lint it on demand.

This should return a non-zero exit code with the `no-restricted-imports` error message:

> Import @prisma/client only from infrastructure/ or shared/. Domain layer must not import Prisma.

(An additional `@typescript-eslint/consistent-type-imports` error also appears on
the fixture; any non-zero exit proves the rule is active.)

The fixture file is intentionally placed under `src/__boundary-test__/` and should never be imported in application code.

CI runs the same command as a `Boundary regression check` step and fails the
build if the fixture unexpectedly passes.
