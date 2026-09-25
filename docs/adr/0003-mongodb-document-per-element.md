# 3. MongoDB: one document per element, a revision per board, transactions on a replica set

- **Status:** accepted
- **Date:** 2026-09-25

## Context

Boards hold up to 5,000 elements and are edited by several people at once. Storing a board as one
document would make every edit rewrite it, and would turn concurrent edits into conflicts on the whole board.

## Decision

- Collections: `boards`, `elements` and `connections`, plus identity, teams, API keys and idempotency records. There is one document per element and per connection, each with a `version` that every change raises.
- Each board keeps a **revision**, raised by every change set. It is the clients' ordering and resync watermark (see ADR 4).
- Writes that touch several documents run in **multi-document transactions**: content plus revision plus element count, board import, and duplication.
- Transactions need a replica set, so MongoDB runs as a **single-node replica set** in Docker Compose (`--replSet rs0`, initiated by the health check).
- Elements are hard-deleted. The removal's version and the board revision go out in the change set.
- A hosted service ensures indexes at startup. Idempotency records expire through a TTL index.
- The Mongo adapter maps BSON conventions: camelCase, enums as strings, standard GUIDs.

## Consequences

- Concurrent edits to different elements never conflict. Edits to the same element are last-writer-wins, with an optional `expectedVersion` check in the public API.
- Local development needs Docker (or another replica set). Compose publishes MongoDB on host port 27018, because a local installation often takes 27017.
