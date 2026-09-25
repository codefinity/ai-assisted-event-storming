# 4. Real-time: server-authoritative last-writer-wins with full post-images and snapshot resync

- **Status:** accepted
- **Date:** 2026-09-25

## Context

People edit the same board at once, from the web app and through the public API. The brief asks for:

- presence, cursors and an editing indicator;
- deterministic conflict resolution;
- clean resync after a dropped connection;
- API changes shown live.

A CRDT would be heavy for sticky notes whose fields are replaced wholesale.

## Decision

- **The server decides.** Every change goes through the same Board Modelling use case, whether it comes from the SignalR hub or the REST API. What the use case commits is announced as a **change set**, which holds:
  - the board revision and the operation id;
  - **full post-images** of the changed elements and connections;
  - the removals.
- **Delivery:** the broadcaster port writes to an in-process feed. One relay reads it and sends to the SignalR group `board:{id}`, so a board's messages leave in order. Presence events are not echoed to their sender.
- **The web client** keeps the server's state (`base`) and the person's **pending operations**. The view is base plus pending.
  - The client applies an image only if the change set's revision is newer than the last one that wrote that entity. Late or duplicated messages therefore never undo newer state.
  - The client's own change comes back with its operation id and replaces the optimistic version.
  - A refusal drops the pending operation, which rolls the view back.
- **Resync:** on (re)connect the client first joins the board, then loads a snapshot.
  - Broadcasts that arrive meanwhile are held, and the newer ones are applied on top.
  - The snapshot reads the board's revision before its content, so nothing is skipped.
  - Unanswered operations are resent after the resync.
- **Presence** (participants, cursors, editing focus, drag previews) is in memory, throttled (the server caps cursors at 30 per second), and never stored.

## Consequences

- The result is deterministic: every client converges to the server's order, whatever the network does.
- If two people edit the same text at the same moment, the later write wins, and the other person sees it replace theirs. The editing indicator makes this rare.
- Presence and the feed live in one process, so the API runs as a single instance. Scaling out needs a SignalR backplane (for example Redis) and a shared feed. They plug in behind the existing ports: `IBoardChangeBroadcaster` and the presence stores.
