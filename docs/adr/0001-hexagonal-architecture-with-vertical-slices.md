# 1. Hexagonal architecture with vertical slices, one core assembly per bounded context

- **Status:** accepted
- **Date:** 2026-09-25

## Context

The brief asks for a hexagonal architecture organised as vertical slices, with strategic DDD. The
reference solution (hex-commerce) puts all use cases in one core project. EventStorming has five
bounded contexts that must stay independent: Identity, Teams, Board Modelling, Collaboration and Public Integration. Board Modelling must not know what a team role is, and
Collaboration must not reach into board storage.

## Decision

- **One core assembly per bounded context** (`server/src/application/EventStorming.<Context>`), plus a technical **Shared Kernel** (`Failure`, `Actor`, `IClock`, paging). Core assemblies never reference each other.
- **The folder is the ring:** `application/` (cores), `driving-adapters/` (REST, SignalR, host), `driven-adapters/` (MongoDB, security, SMTP, registry, presence, broadcasting).
- **A slice is two files** in `Slices/<Name>/`:
  - `<Name>.cs`: the command or query, its result, the handler interface and the store port.
  - `<Name>Handler.cs`: the handler and its validator.

  Each slice owns its store port, so persistence is shaped per use case.
- **Cross-context needs are ports owned by the downstream context**, implemented by an adapter that is the anti-corruption layer. For example, `Persistence.MongoDb/Acl/TeamMembershipAcl` turns team roles into `BoardPermission`.
- **Each project exposes exactly one public `*ServiceExtensions` class**; the host is the only composition root.
- The planned `Api.App` and `Api.Public` became **one `Api.Rest` adapter** with two route groups (`/api/app`, `/api/v1`).
  - They share the Problem Details mapping, DTOs and the Board Document.
  - Their policies stay separate: auth scheme, CORS, rate limits and idempotency.

## Consequences

- Architecture tests (`EventStorming.Architecture.Tests`, ArchUnitNET) enforce the rings, the independence of the cores, slice shape and composition rules. A planted violation fails them.
- Adding a use case means adding a folder. Nothing central changes except DI registration in the context's `ServiceExtensions`.
- Some duplication between slices (similar store ports) is accepted in exchange for independent change.
