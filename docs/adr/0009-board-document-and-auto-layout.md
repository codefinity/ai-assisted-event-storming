# 9. A declarative Board Document with automatic timeline layout

- **Status:** accepted
- **Date:** 2026-09-25

## Context

An agent, or a person with a text editor, should describe a model rather than compute coordinates.
Export and import should use the same format, so a board can round-trip.

## Decision

- **The Board Document v1:** `{ version, board: { name, level }, elements: [...], connections: [...] }`.
  - Each element has a `type`, plus optional `key`, `text`, `position`, `size`, `swimlane`, `boundary`, `anchor`, `pivotal` and `color`.
  - Connections go `from` → `to`. Each end names an element by its key, by the id of an existing element, or by an id given in the same request.
- **Layout** (`TimelineLayout`, a pure module) places everything that has no position:
  - Array order is the timeline, with one column per element across all lanes.
  - Anchored elements stack below their anchor.
  - Lanes are bands, stacked top to bottom.
  - Boundaries become boxes around their members.
  - Pivotal events get extra space.
  - Explicit positions are kept.
  - Content added later starts to the right of what already exists.
- **Export** writes the same shape, with every position filled in and element ids as keys. Membership in lanes and boundaries is worked out from geometry.
- The same drafting code (`DraftPlanner`) validates and lays out import, replace and bulk, so all three share the same rules and errors.

## Consequences

- A whole board is one request and one transaction.
- The layout is deliberately simple and predictable rather than optimal; people rearrange afterwards.
