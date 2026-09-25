# 7. Sessions with an in-memory access token and a rotating refresh cookie; hashed API keys

- **Status:** accepted
- **Date:** 2026-09-25

## Context

The web app needs a sign-in that survives reloads without exposing long-lived tokens to scripts. The
public API needs credentials that scripts and agents can hold.

## Decision

- **Passwords:** PBKDF2-SHA256 with 600,000 iterations and a per-password salt. Sign-in with an unknown email still computes a hash, so timing does not reveal which accounts exist.
- **Access token:** a 15-minute HMAC-SHA256 JWT carrying `sub`, `name` and `email`.
  - It is kept in Redux memory only and sent as a bearer token.
  - It is also accepted as `access_token` in the query string, but only on the hub's WebSocket path.
- **Refresh token:** a random token, stored only as a hash. It lives in an `HttpOnly`, `SameSite=Strict` cookie scoped to `/api/app/auth`, valid for 30 days.
  - Every refresh **rotates** it.
  - Presenting an already-rotated token revokes the whole token family (theft detection).
  - There is one exception: a 20-second grace window, so two tabs refreshing together do not sign each other out.
  - Session endpoints also require an `X-Requested-With` header, which a cross-site form cannot send.
- **Signing key:** never in the repository. In Development the host generates one into a keys directory (a Docker volume in Compose). In Production it must be configured (`Jwt__SigningKey`).
- **API keys:** formatted `es_<key id>_<secret>`. Only a SHA-256 hash of the secret is stored, and the id prefix makes lookup a single indexed read.
  - Keys belong to one team and have scopes (`read`, `write`).
  - They can expire and be revoked, and they record when they were last used.
- **Authorization on every board operation** happens in the use case, through `IBoardAccess`, never in the adapter alone.

## Consequences

- An XSS bug cannot steal a long-lived credential, and a stolen refresh cookie is detected on reuse.
- A hub connection closes when its token expires, then reconnects with a fresh one.
