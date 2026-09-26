# Public API (v1)

Build and read EventStorming boards from scripts, other systems and LLM agents. Everything the web
app can do to a board's content is available here, and every change made through the API appears
live on the boards people have open.

- **Base URL:** `http://localhost:5080/api/v1` in the local stack.
- **Reference:** the interactive OpenAPI reference is at [`/docs`](http://localhost:5080/docs). The raw documents are at `/openapi/v1.json`.
- **For LLM agents:** [`llm-guide.md`](llm-guide.md) (also served at `/docs/llm-guide.md`) explains the notation and the Board Document with a worked example.
- **Board Document schema:** [`schemas/board-document.v1.schema.json`](schemas/board-document.v1.schema.json) (also served at `/api/v1/schemas/board-document.json`).

## Contents

1. [Authentication](#authentication)
2. [Conventions](#conventions)
3. [Errors](#errors)
4. [Retrying safely: idempotency keys](#retrying-safely-idempotency-keys)
5. [Rate limits](#rate-limits)
6. [Endpoints](#endpoints)
7. [The Board Document and auto-layout](#the-board-document-and-auto-layout)
8. [Versioning](#versioning)

## Authentication

Every request except the few marked *anonymous* needs a team **API key**:

```
Authorization: Bearer es_<key id>_<secret>
```

A team **Owner** creates keys in the web app: open the team, choose **Members & API keys**, and use
**API keys**. The full key is shown once; only a hash is stored. A key:

- belongs to **one team**, and sees and changes only that team's boards;
- has **scopes**: `read` (list and read), or `read` + `write` (also create, change and delete); `write` always includes `read`;
- can have an **expiry date**, and can be **revoked** at any time; revoked or expired keys get `401`.

A missing or unusable key gets `401 unauthenticated`. A key without the `write` scope gets
`403 forbidden` (code `insufficient-scope`) on any write.

## Conventions

- **JSON** in and out, `Content-Type: application/json`, field names in `camelCase`.
- **Ids** are UUIDs. **Timestamps** are ISO 8601 with an offset, e.g. `2026-09-25T21:40:08.145+00:00`.
- **Numbers are plain JSON numbers.** Coordinates are board pixels; x grows to the right (later in time) and y grows downwards.
- **Unknown fields are rejected** with `400` and a JSON pointer to the field, so a typo such as `"txt"` or `"colour"` is reported, not silently ignored.
- **Lists are paged** with a cursor: pass `limit` (default 50, at most 200) and, for the next page, the `nextCursor` from the previous response as `cursor`. `nextCursor` is `null` on the last page.

  ```json
  { "items": [ … ], "nextCursor": "…an opaque string…" }
  ```

- **Who changed what:** resources carry `createdBy` / `updatedBy` as `{ "kind": "account" | "api-key", "id", "name" }`. Changes made with a key are attributed to the key's name.

## Errors

Every error is an [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) Problem Details document
(`application/problem+json`). Besides the standard fields, it carries:

| Field | Meaning |
|---|---|
| `code` | A stable, machine-readable reason, e.g. `unknown-element-type`, `version-conflict`, `board-archived`. |
| `fix` | What to change so the request succeeds, when there is one thing to say. |
| `errors` | For field problems: every offending field at once, each with `pointer` (JSON Pointer), `field` (camelCase path), `code`, `detail` and `fix`. |
| `traceId` | Quote it when reporting a problem. |

A real response for a document with a mistyped type, a misspelt key and a dangling arrow:

```json
{
  "type": "/api/v1/problems/validation-failed",
  "title": "The request has invalid fields.",
  "status": 422,
  "detail": "3 fields need attention.",
  "instance": "/api/v1/boards/import",
  "traceId": "00-cb08148cd5ec437bdfb5585c97c3ba75-3baf9cb07f852e7a-00",
  "code": "unknown-element-type",
  "errors": [
    {
      "pointer": "#/elements/1/type",
      "field": "elements[1].type",
      "code": "unknown-element-type",
      "detail": "'hotspot' is not an element type.",
      "fix": "Use one of: domain-event, command, actor, policy, read-model, external-system, aggregate, hot-spot, opportunity, swimlane, boundary. See GET /api/v1/element-types."
    },
    {
      "pointer": "#/elements/1/anchor",
      "field": "elements[1].anchor",
      "code": "unknown-reference",
      "detail": "No element in this request has key 'plced', and no element on the board has that id.",
      "fix": "Did you mean 'placed'? Use the key of an element defined in this request, or the id of an existing element."
    },
    {
      "pointer": "#/connections/0/to",
      "field": "connections[0].to",
      "code": "unknown-reference",
      "detail": "No element in this request has key or id 'paid', and no element on the board has that id.",
      "fix": "Use a key defined in 'elements', or an existing element id."
    }
  ]
}
```

### Problem types

The `type` is a relative URL; `GET` it (anonymous) for the same description as JSON. `GET /api/v1/problems` lists them all.

| `type` | Status | When | What to do |
|---|---|---|---|
| `/api/v1/problems/malformed-request` | 400 | The body is not JSON, a value has the wrong JSON type, or a field is unknown. `pointer` names the spot. | Fix the JSON at the pointer. |
| `/api/v1/problems/unauthenticated` | 401 | No key, or a malformed, unknown, revoked or expired one. | Send a valid `Authorization: Bearer es_…`. |
| `/api/v1/problems/forbidden` | 403 | The key lacks the `write` scope. | Use a key with `write`. |
| `/api/v1/problems/not-found` | 404 | No such board, element or connection, or it belongs to another team (the two are indistinguishable on purpose). | Check the id; list boards with `GET /boards`. |
| `/api/v1/problems/conflict` | 409 | The state changed or a rule about the current state blocks it. `code` says which: `version-conflict`, `board-archived`, `connection-exists`, `idempotency-in-progress`. | Re-read and retry; for `version-conflict` resend with the current `version`. |
| `/api/v1/problems/payload-too-large` | 413 | A body over 2 MB. | Split into smaller requests. |
| `/api/v1/problems/validation-failed` | 422 | Readable but breaks rules; every field is listed in `errors`. | Apply each `fix` and resend. |
| `/api/v1/problems/limit-exceeded` | 422 | The change would take a board past 5,000 elements or 10,000 connections (`board-limit-exceeded`). | Remove content or split the model across boards. |
| `/api/v1/problems/rate-limited` | 429 | The key used up its allowance for the minute. | Wait `Retry-After` seconds. |
| `/api/v1/problems/internal-error` | 500 | Our fault. | Retry later; report the `traceId` if it persists. |

## Retrying safely: idempotency keys

Networks fail after the server has done the work. To make a write safe to retry, send an
`Idempotency-Key` header: any unique string of 1–255 visible ASCII characters (a UUID is ideal).

```
POST /api/v1/boards/{boardId}/elements/bulk
Idempotency-Key: 5a0f3d0e-6c3b-4f51-9d3e-2f9a1b7c8e11
```

- The first request runs; its response is kept for **24 hours** per API key.
- Repeating it with **the same key and the same body** returns the stored response, with the header `Idempotent-Replayed: true`, and changes nothing.
- The same key with **a different body** gets `422` (`idempotency-key-reused`). Use a new key for a new request.
- If the first request is **still running**, a repeat gets `409` (`idempotency-in-progress`); retry shortly. A request abandoned for 2 minutes can be run again.
- Server errors (5xx) are not stored, so a retry after one runs again.

It applies to `POST`, `PUT`, `PATCH` and `DELETE`. Without the header, writes are not deduplicated.
Element and connection creation are also idempotent by id when you choose the ids yourself.

## Rate limits

Each API key may make **300 requests per minute** (a fixed one-minute window). Responses carry
`RateLimit-Policy: 300;w=60`. Past the limit, requests get `429` with a `Retry-After` header in
seconds. Prefer the bulk endpoint and Board Document import to many single-element calls.

Browsers cannot call the public API unless its origin is listed in the host setting
`Cors:PublicApiOrigins` (none by default). It is meant for servers, scripts and agents.

## Endpoints

All paths are relative to `/api/v1`. In the examples, `$KEY` holds an API key and `$API` is
`http://localhost:5080/api/v1`.

### About (anonymous)

| Method | Path | Returns |
|---|---|---|
| `GET` | `/` | Where to start: links to the docs, the LLM guide, the schema, element types and boards. |
| `GET` | `/schemas/board-document.json` | The Board Document's JSON Schema (2020-12), with the current element types as an `enum`. |
| `GET` | `/problems`, `/problems/{slug}` | The problem types above. |

### Element types (`read`)

| Method | Path | Returns |
|---|---|---|
| `GET` | `/element-types` | `{ types, levels }`: every type (id, name, color, default size, levels, shortcut, whether it can be pivotal, description, when to use it, writing rule, examples) and each level's palette. |

```sh
curl -s $API/element-types -H "Authorization: Bearer $KEY"
```

### Boards

| Method | Path | Scope | Body | Returns |
|---|---|---|---|---|
| `GET` | `/boards?includeArchived=&cursor=&limit=` | read | — | `200` page of boards, most recently changed first |
| `POST` | `/boards` | write | `{ "name", "level" }` | `201` the board |
| `POST` | `/boards/import` | write | a Board Document | `201` `{ board, keys, elementCount, connectionCount }` |
| `GET` | `/boards/{boardId}` | read | — | `200` the board |
| `PATCH` | `/boards/{boardId}` | write | `{ "name" }` | `200` the board |
| `DELETE` | `/boards/{boardId}` | write | — | `200` the board, now **archived** (read-only, hidden from lists) |
| `POST` | `/boards/{boardId}/restore` | write | — | `200` the board, restored |
| `GET` | `/boards/{boardId}/document` | read | — | `200` the board as a Board Document, every position filled in, ids as keys |
| `PUT` | `/boards/{boardId}/document` | write | a Board Document | `200` `{ board, keys, elementCount, connectionCount }`: **replaces all content** |

A board: `{ id, name, level, elementCount, revision, createdAt, updatedAt, updatedBy, archivedAt }`.
`level` is `big-picture`, `process-modelling` or `software-design` and cannot change. Archived boards
refuse changes with `409 board-archived` until restored.

```sh
# Create a board from a whole document, laid out automatically
curl -s -X POST $API/boards/import \
  -H "Authorization: Bearer $KEY" -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  --data @docs/examples/food-ordering.board.json

# Export it back
curl -s $API/boards/$BOARD/document -H "Authorization: Bearer $KEY" > ordering.board.json
```

### Elements

| Method | Path | Scope | Body | Returns |
|---|---|---|---|---|
| `GET` | `/boards/{boardId}/elements?type=&cursor=&limit=` | read | — | `200` page of elements, optionally of one type |
| `POST` | `/boards/{boardId}/elements` | write | one element (below) | `201` the element |
| `POST` | `/boards/{boardId}/elements/bulk` | write | `{ "elements": [...], "connections": [...] }` | `201` `{ elements, connections, keys, resized }` |
| `GET` | `/boards/{boardId}/elements/{elementId}` | read | — | `200` the element |
| `PATCH` | `/boards/{boardId}/elements/{elementId}` | write | the fields to change | `200` the element |
| `DELETE` | `/boards/{boardId}/elements/{elementId}` | write | — | `204`; its arrows are deleted too |

An element: `{ id, type, text, position: { x, y }, size: { width, height }, pivotal, color, version, createdAt, createdBy, updatedAt, updatedBy }`.

**Creating one element**: `type` is required, everything else optional.

```json
{ "type": "domain-event", "text": "Order Placed", "position": { "x": 400, "y": 40 }, "pivotal": true }
```

Leave `position` out and the element continues the timeline to the right of the existing content.
`swimlane` (the id of a swimlane element) places it in that lane, and `anchor` (the id of an element)
stacks it below that element.

**Bulk** takes Board Document elements and connections: up to 500 elements in one request, laid out
together, with `key`s so elements and arrows in the same request can refer to each other. The
response's `keys` maps each key to the new element's id:

```sh
curl -s -X POST $API/boards/$BOARD/elements/bulk \
  -H "Authorization: Bearer $KEY" -H "Content-Type: application/json" \
  -H "Idempotency-Key: $(uuidgen)" \
  -d '{
    "elements": [
      { "key": "customer", "type": "actor",        "text": "Customer" },
      { "key": "place",    "type": "command",      "text": "Place Order" },
      { "key": "placed",   "type": "domain-event", "text": "Order Placed" }
    ],
    "connections": [ { "from": "place", "to": "placed" } ]
  }'
```

```json
{
  "elements": [ { "id": "01a0da82-eb53-74a4-8df0-2254917115d6", "type": "command", "text": "Place Order", "position": { "x": 168, "y": 40 }, "…": "…" } ],
  "connections": [ { "id": "01a0da82-eb62-7d89-83ad-81410043382d", "from": "01a0da82-eb53-74a4-8df0-2254917115d6", "to": "01a0da82-eb53-7825-8d07-65a9ad16edd7", "label": null, "version": 1 } ],
  "keys": { "customer": "01a0da82-eb53-78db-aa1a-0e07422d0ba3", "place": "01a0da82-eb53-74a4-8df0-2254917115d6", "placed": "01a0da82-eb53-7825-8d07-65a9ad16edd7" },
  "resized": []
}
```

Look elements up through `keys`. New elements continue after the last sticky on the board, inside the
swimlanes and boundaries they name. A swimlane or boundary that is too small grows to hold them, and
comes back in `resized`.

**Updating** sends only what changes: `text`, `type`, `position`, `size`, `pivotal`, and `color`
(`#RRGGBB`, or `""` to return to the type's color). Add `expectedVersion` to update only if nobody
changed the element since you read it; otherwise you get `409 version-conflict`. Without it, the
last write wins. Changing to a type that cannot be pivotal clears `pivotal`.

```sh
curl -s -X PATCH $API/boards/$BOARD/elements/$ELEMENT \
  -H "Authorization: Bearer $KEY" -H "Content-Type: application/json" \
  -d '{ "text": "Order Submitted", "expectedVersion": 3 }'
```

### Connections

| Method | Path | Scope | Body | Returns |
|---|---|---|---|---|
| `GET` | `/boards/{boardId}/connections?cursor=&limit=` | read | — | `200` page of connections |
| `POST` | `/boards/{boardId}/connections` | write | `{ "from", "to", "label" }` (element ids) | `201` the connection |
| `DELETE` | `/boards/{boardId}/connections/{connectionId}` | write | — | `204` |

A connection: `{ id, from, to, label, version, createdAt }`. One arrow per ordered pair: a second
`from → to` gets `409 connection-exists`. An element cannot connect to itself.

## The Board Document and auto-layout

The Board Document (v1) is the whole board in one JSON document: what export produces, and what
import, replace and bulk accept. Its schema is
[`schemas/board-document.v1.schema.json`](schemas/board-document.v1.schema.json).

```json
{
  "version": 1,
  "board": { "name": "Online food ordering", "level": "big-picture" },
  "elements": [
    { "key": "customer", "type": "swimlane", "text": "Customer" },
    { "key": "ordering", "type": "boundary", "text": "Ordering" },
    { "key": "placed", "type": "domain-event", "text": "Order Placed", "swimlane": "customer", "boundary": "ordering", "pivotal": true },
    { "key": "paid", "type": "domain-event", "text": "Payment Authorised", "swimlane": "customer", "boundary": "ordering" },
    { "key": "late", "type": "hot-spot", "text": "What if the payment is declined?", "anchor": "paid" }
  ],
  "connections": [ { "from": "placed", "to": "paid" } ]
}
```

Anything without a `position` is laid out for you:

- elements are placed **left to right in array order**: the array is the timeline;
- an element with an `anchor` is **stacked below** that element, in its column;
- each **swimlane** is a horizontal band; elements naming it are placed inside it, and lanes stack top to bottom;
- each **boundary** becomes a box drawn around the elements that name it;
- a **pivotal** event gets extra space before it, so phases read apart;
- elements you give a `position` keep it, and the rest are placed around them.

When adding to an existing board, new content continues after the last sticky already there, and
existing swimlanes and boundaries grow to hold new elements that name them. The
[LLM guide](llm-guide.md) walks through a complete example.

On `PUT /boards/{id}/document` the whole content is replaced in one transaction; open boards reload
at once. `board.level`, if given, must match the board's level (`422 level-immutable`). On import,
`board.name` and `board.level` are required.

## Versioning

- The version is in the path: `/api/v1`. Within v1, changes are additive only: new endpoints, new optional request fields, new response fields. Clients should ignore response fields they do not know.
- Request bodies reject unknown fields, so a new optional field is only sent by clients that know it.
- The Board Document carries `"version": 1`; documents of another version are refused (`unsupported-version`).
- Anything that would break a v1 client becomes `/api/v2`, with v1 kept running alongside it.
- New element types are data, not an API change: read `GET /element-types` (or the schema's `enum`) instead of hard-coding the list.
