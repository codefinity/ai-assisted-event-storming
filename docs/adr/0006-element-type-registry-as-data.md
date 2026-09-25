# 6. The element-type registry is data

- **Status:** accepted
- **Date:** 2026-09-25

## Context

The brief says element types are registry data, not hard-coded, so the notation can grow, or be
tailored, without code changes.

## Decision

- The registry is `server/src/driven-adapters/EventStorming.ElementTypes.Json/element-types.json`. It lists the levels, then the types. Each type has:
  - id, name, category and renderer;
  - colors, icon, default size, the levels it belongs to, and a shortcut;
  - a text limit, whether it can be pivotal, and a layout role (`item`, `lane` or `boundary`);
  - a description, when to use it, a writing rule and examples.
- It is embedded in the API by default. Setting `ElementTypes:Path` (environment variable `ElementTypes__Path`) loads another file instead, with no rebuild.
- It is **validated at startup**: unique kebab-case ids, unique shortcuts, known categories, layout roles and levels, `#RRGGBB` colors, text limits and minimum sizes. A bad file stops the API with a clear message.
- Renderer names are not checked; the web app falls back to a sticky for a name it does not know.
- The core sees the registry through `IElementTypeRegistry`. Validation, auto-layout, the JSON Schema's `enum` and the palettes all derive from it.
- The web app loads it from `GET /api/app/element-types` and draws elements by **renderer name** (`sticky`, `lane`, `area`). An element of an unknown type still renders, with a neutral fallback.

## Consequences

- Adding a type means editing the JSON and restarting the API (see the README): a rebuild for the embedded file, or only a restart with `ElementTypes:Path`. No server or web code changes.
- Swimlanes and boundaries are registry types too, so they share selection, move, resize, undo and sync with stickies.
