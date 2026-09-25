# 8. The public API is an Open Host Service with a Published Language

- **Status:** accepted
- **Date:** 2026-09-25

## Context

Other systems and LLM agents must be able to read and build boards. The brief asks for:

- versioning, API keys and scopes;
- a declarative bulk endpoint with auto-layout and client keys;
- idempotency keys;
- RFC 9457 errors that name the field and the fix;
- per-key rate limits and pagination;
- readiness for an MCP server.

## Decision

- **`/api/v1`**, versioned in the path. Changes within v1 are additive only.
  - Its published language is the resources and the **Board Document** (ADR 9).
  - The Board Document's JSON Schema is served at `/api/v1/schemas/board-document.json` and checked in under `docs/schemas`; a test keeps the two identical.
- It calls **the same use cases** as the web app. Nothing is reimplemented for the API.
- **Errors:** one mapping from `Failure` to Problem Details, with `code`, `fix`, and an `errors[]` entry per field holding a JSON Pointer.
  - All field problems are reported at once.
  - A mistyped key gets a "Did you mean …?" suggestion.
  - Request bodies **reject unknown fields**.
- **Idempotency-Key** on writes: the first request reserves the key, and its response is replayed (`Idempotent-Replayed: true`) for 24 hours. Reusing the key with a different body is refused.
- **Rate limits** per API key: a fixed window, 300 requests per minute by default, with `Retry-After`. The planned sliding window was replaced by a fixed one, because the sliding limiter gives no retry-after metadata.
- **CORS** is closed for the public API by default (`Cors:PublicApiOrigins`). The API is for servers and agents.
- **LLM readiness:** `GET /api/v1` points to the docs, the schema and `/docs/llm-guide.md`, which the API serves itself. Every error says how to fix it.

## Consequences

- An MCP server can be another thin driving adapter over the same use cases, reusing the Board Document and the problem catalogue. No core change is needed.
- Changes made through the API reach open boards live, because they go through the same broadcaster.
