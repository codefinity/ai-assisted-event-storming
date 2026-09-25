# 5. Frontend: Redux-centric modules per bounded context

- **Status:** accepted
- **Date:** 2026-09-25

## Context

The brief sets these requirements for the frontend:

- Redux Toolkit for all application state, with RTK Query, entity adapters and a realtime middleware;
- DOM rendering only;
- smooth editing with 500+ elements.

hex-commerce's frontend uses a hexagon of its own, which would duplicate the structure Redux already gives.

## Decision

- `web/src/modules/<context>` mirrors the backend's contexts: identity, teams, board-modelling, collaboration and public-integration.
  - `shared/` holds the API base, UI primitives and helpers.
  - `app/` holds the Next.js routes that compose the modules.
- **One RTK Query API**, with endpoints injected per module. The access token lives only in Redux memory, and a re-auth base query refreshes it once on `401`.
- **State is split into three slices:**
  - **Board content** is two entity adapters (elements, connections) in `board` state, using ADR 4's base-plus-pending model.
  - **Editor state** (viewport, selection, drag, drafts) is its own slice.
  - **History** holds each person's inverse operations for undo.
- **The realtime middleware** owns the SignalR connection through an injected `HubClient`, so tests can use a fake hub.
- **Board Modelling does not import Collaboration.** The board page plugs presence, cursors, editing markers and drag ghosts into the editor's slots. A Vitest architecture test enforces the module rules.
- **Rendering:**
  - A CSS-transformed world layer holds the board.
  - Each id gets one memoised `ElementNode` that reads primitive selectors, so changing one element re-renders only that element. A test checks this with React's Profiler.
  - Id lists keep their identity while only positions change.
  - The world layer gets its own compositor layer only while panning or zooming.

## Consequences

- With 600 elements, pan, zoom and drag hold 60 fps in headless Chrome.
- Everything the user does is an action, which keeps undo, the middleware and tests straightforward. The price is more boilerplate than component state.
