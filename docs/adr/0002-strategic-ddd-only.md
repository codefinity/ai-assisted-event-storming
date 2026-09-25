# 2. Strategic DDD only; no tactical patterns

- **Status:** accepted
- **Date:** 2026-09-25

## Context

The brief asks for strategic DDD: bounded contexts, a context map, ubiquitous language, ACLs and a published
language. It explicitly rules out tactical DDD: aggregates, entities with behaviour, value objects, domain
events and repositories.

## Decision

- The model is **plain immutable records** (`Board`, `Element`, `Team`, …). Rules live in handlers, validators and small pure modules: `BoardRules`, `Drafting/DraftPlanner` and `Layout/TimelineLayout`.
- Persistence ports are **per-slice stores**, not repositories per aggregate.
- Contexts communicate through **the context map's patterns**:
  - Conformist: Identity's claims.
  - Customer–Supplier with an ACL: Teams → Board Modelling.
  - Published Language: board change sets and the Board Document.
  - Open Host Service: `/api/v1`.
- The glossary (`docs/glossary.md`) is the ubiquitous language: code, UI and docs use the same words. The board structure for a bounded context is called `boundary`, so it is never confused with the tool's own contexts.

## Consequences

- `NoTacticalPatternsTests` fails the build if types named or shaped like aggregates, value objects, domain events or repositories appear in the cores.
- Invariants that span documents, such as element counts and revision bumps, are enforced in the store adapters inside MongoDB transactions, not by an aggregate root.
