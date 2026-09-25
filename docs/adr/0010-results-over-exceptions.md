# 10. Results over exceptions, with structured failures

- **Status:** accepted
- **Date:** 2026-09-25

## Context

Every use case has expected refusals: validation, not found, forbidden and conflicts. The public API
must name the field and the fix for each one.

## Decision

- Handlers return a result (`IUseCaseResult`: `Success`, `Failures`) instead of throwing for expected outcomes.
- A `Failure` carries:
  - a `Kind`: Validation, NotFound, Forbidden, Conflict, Unauthenticated or LimitExceeded;
  - a stable `Code` and a `Message`;
  - the `Field`, as a camelCase JSON path;
  - a `Fix`.
- Validation uses FluentValidation inside the cores. This is a deliberate exception to "no framework in the core".
  - `.WithFix(...)` attaches the fix.
  - Property names become JSON paths.
- Driving adapters map failures in one place: `Problems.From` for REST (RFC 9457) and `FailureWire` for the hub.
- Unexpected exceptions become `500 internal-error` with a trace id, and are logged.
- "Not found" and "not yours" are indistinguishable (both `404`), so the ids of other teams' boards reveal nothing.

## Consequences

- Behaviour specs (Reqnroll) assert codes and fields directly against the handlers, using in-memory fakes.
- Every error a client sees is actionable, and the problem catalogue documents them all.
