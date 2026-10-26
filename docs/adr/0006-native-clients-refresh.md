# ADR 0006: Native client support for refresh and select-tenant

**Date:** October 2026
**Status:** Accepted
**Supersedes:** none
**Relates to:** ADR 0003 (backend on ASP.NET Core), ADR 0004 (PostgreSQL with RLS), `docs/frontend/INTEGRATION.md` §2.3 of the Flutter client repository

## Context

The auth contract was designed for a browser PWA: the refresh token lives in an
`httpOnly` `SameSite=Lax` cookie scoped to `Path=AuthConstants.RefreshCookiePath`,
and `POST /auth/refresh` plus `POST /auth/select-tenant` are protected by the
`OriginGuardMiddleware` (`[CheckOrigin]`), which rejects requests without a
matching `Origin`/`Referer`. That is CSRF defence: it prevents a hostile website
from making the user's browser reuse the ambient cookie it cannot read.

The frontend is now a **Flutter mobile app** (the PWA was abandoned). A native
client:

- has no browsable `Origin` or `Referer`, so `[CheckOrigin]` rejects every
  request it legitimately makes;
- does not have a browser cookie jar; per the previous integration workaround
  it had to parse the `Set-Cookie` header of login/refresh, store
  `vepaydo_refresh` in `flutter_secure_storage`, and forge `Cookie` headers and
  a fake `Origin` on every refresh — fragile and hard to keep correct across
  HTTP client versions, retries, and offline queues.

Forcing the mobile app to impersonate a browser (fake `Origin`, manual cookie
headers) is a security smell and the documented workaround was explicitly
marked as temporary in the client contract.

## Decision

1. **Client identification.** Native clients send the header
   `X-Client-Type: native` on the two affected endpoints. The constant lives in
   `VepayDO.Api.Kernel.Http.ClientTypeHeaders` (Api layer only; authentication
   policy stays out of Application/Domain).

2. **`OriginGuardMiddleware` bypass for native clients.** When the request
   carries `X-Client-Type: native`, the middleware skips the origin check.
   Browser behaviour is unchanged: without the header the guard still demands a
   valid `Origin`/`Referer`.

3. **Refresh token in the body.** Requests and responses that carry refresh
   tokens gain an optional body field. `RefreshRequest` gains `RefreshToken`;
   `RefreshResponse`, `AuthResponse` (register/login) and the refresh response
   gain a nullable `RefreshToken` member (serialised only when non-null via
   `JsonIgnoreCondition.WhenWritingNull`).
   - Native clients send the current refresh token in the request body and
     receive the **rotated** one in the response body.
   - `register` and `login` return the issued refresh token in the body for
     native clients, so a native client never has to parse `Set-Cookie` to
     bootstrap its first token.
   - Browser clients keep the cookie flow: `refreshToken` is never present in
     the JSON they receive.
   - The `Set-Cookie` header stays written for both paths (harmless for native
     clients, contract-compatible for browsers).

4. **Body tokens on `select-tenant` and `logout`.** `SelectTenantRequest` and a
   new `LogoutRequest` gain an optional `RefreshToken` field:
   - `select-tenant` sends the current token so the session is bound to the
     chosen tenant and the new access token embeds the `sid` claim (fallback
     order: body token, then cookie).
   - `logout` sends the current token so only the matching session is revoked;
     without any token the service (unchanged) revokes all of the user's
     sessions, so native clients must send the token for a scoped logout.

5. **Fail-closed refresh.** The controller falls back to the cookie:
   `body token (native only) → cookie → empty`. An empty token is already
   rejected by `AuthService.RefreshAsync` with `401 Unauthorized`; nothing in
   this change weakens that.

## Security analysis

- **Why removing the origin check does not re-enable CSRF.** CSRF matters only
  when an ambient credential (the cookie) is attached automatically by the
  browser. `SameSite=Lax` cookies are not sent on cross-site `POST`, and a
  cross-site request cannot read the response or the token. A hostile page can
  *try* to send `X-Client-Type: native`, but:
  - a native request without a body token falls back to the cookie, which it
    does not have (Lax blocks it), so it fails closed with `401`;
  - forging the body token requires already possessing the token, at which
    point CSRF is moot.
- **Token exposure in the response body.** Only requests explicitly marked
  `native` receive tokens in the body (register, login, refresh, logout,
  select-tenant). Native clients are expected to store them in a secure store
  (`flutter_secure_storage`); browsers, whose storage is attacker-reachable
  (`XSS`), keep the `httpOnly`-cookie path. A browser can never be steered into
  receiving a token in the body without first possessing one in its cookie, and
  the origin guard still protects it in that scenario.
- **Reuse detection unchanged.** Rotation, family burn on reuse, and session
  revocation behave exactly as before (`AuthService.RefreshAsync` untouched).
- The same preconditions (`sid`, rate limits, `AllowPendingLegal`/
  `AllowPasswordChange` gates, audit records) apply to both client types.

## Consequences

- The Flutter app no longer needs to fake an `Origin` or parse `Set-Cookie`
  for the request side; it stores and sends refresh tokens explicitly.
- Browser clients and the OpenAPI contract change compatibly: both request
  fields are optional and the response field is omitted when null.
- The duplicate request/response records in `VepayDO.Contracts` and the Api
  controller layer were both extended to keep them in sync.
- `OriginGuardTests` gain native-path coverage; `AuthEndpointsTests.Part7`
  covers the native refresh/select-tenant contract, including the guarantee
  that browser responses never contain `refreshToken`.

## Alternatives considered

- **Keep the cookie workaround on the client** (parse `Set-Cookie`, forge
  headers): rejected — it emulates a browser from a non-browser, which is
  brittle and obscures the real threat model.
- **Drop `OriginGuard` entirely**: rejected — CSRF defence for a future web
  surface would have to be rebuilt, and the hardening is still valid today.
- **Separate `/auth/native/*` endpoints**: rejected — two divergent surfaces
  for the same security semantics; one endpoint with an explicit, tested
  client-type branch is smaller and auditable.

## References

- `src/VepayDO.Api/Kernel/Http/ClientTypeHeaders.cs`
- `src/VepayDO.Api/Kernel/Http/OriginGuardMiddleware.cs`
- `src/VepayDO.Api/Controllers/Auth/AuthController.cs` (`register`, `login`, `refresh`, `logout`, `select-tenant`)
- `src/VepayDO.Contracts/Auth/AuthContracts.cs`
- Flutter client contract: `VePayDOAPPMovil/docs/frontend/INTEGRATION.md` §2.3, §7
