import { createEntityAdapter, createSlice, isDraft, original, type EntityState, type PayloadAction } from '@reduxjs/toolkit';
import type {
  BoardChangeSet,
  BoardConnection,
  BoardDetails,
  BoardElement,
  BoardPermission,
  BoardReplaced,
  BoardSnapshot,
  BoardSummary,
} from '../model';
import {
  applyToConnection,
  applyToElement,
  sameConnection,
  sameElement,
  touchedBy,
  type ContentOp,
  type PendingOp,
  type Touched,
} from './ops';

// The board's content, kept two ways:
//  - `base` is what the server has committed, as far as this client has heard;
//  - `elements` / `connections` are what the person sees: base with their pending operations on top.
// Server change sets update base (last writer wins, ordered by board revision per entity) and the
// affected entities are recomputed. Undoing a failed operation is just recomputing without it.

export const elementsAdapter = createEntityAdapter<BoardElement>();
export const connectionsAdapter = createEntityAdapter<BoardConnection>();

export type BoardPhase = 'idle' | 'loading' | 'ready' | 'failed';

export interface BoardLoadFailure {
  code: string;
  message: string;
}

export interface BoardState {
  boardId: string | null;
  board: BoardSummary | null;
  permission: BoardPermission | null;
  phase: BoardPhase;
  failure: BoardLoadFailure | null;
  /** While a snapshot is loading, change sets wait here and are applied on top of it. */
  syncing: boolean;
  buffer: BoardChangeSet[];
  /** The snapshot's revision: every change at or below it is already in the snapshot. */
  floor: number;
  /** The highest revision applied. */
  revision: number;
  base: { elements: Record<string, BoardElement>; connections: Record<string, BoardConnection> };
  /** The revision that last wrote each entity (removals included), so an older change never overwrites a newer one. */
  revs: Record<string, number>;
  pending: PendingOp[];
  elements: EntityState<BoardElement, string>;
  connections: EntityState<BoardConnection, string>;
}

const initialState: BoardState = {
  boardId: null,
  board: null,
  permission: null,
  phase: 'idle',
  failure: null,
  syncing: false,
  buffer: [],
  floor: 0,
  revision: 0,
  base: { elements: {}, connections: {} },
  revs: {},
  pending: [],
  elements: elementsAdapter.getInitialState(),
  connections: connectionsAdapter.getInitialState(),
};

export interface OpAcknowledgement {
  opId: string;
  ok: boolean;
}

export const boardSlice = createSlice({
  name: 'board',
  initialState,
  reducers: {
    boardOpened(_state, action: PayloadAction<string>) {
      return { ...initialState, boardId: action.payload, phase: 'loading' as const, syncing: true };
    },
    boardClosed() {
      return initialState;
    },
    /** A (re)join started: hold incoming changes until the snapshot that follows is in. */
    resyncStarted(state) {
      state.syncing = true;
      state.buffer = [];
    },
    snapshotLoaded(state, action: PayloadAction<BoardSnapshot>) {
      const snapshot = action.payload;
      if (snapshot.board.id !== state.boardId) {
        return;
      }

      state.board = snapshot.board;
      state.permission = snapshot.permission;
      state.phase = 'ready';
      state.failure = null;
      state.base = {
        elements: Object.fromEntries(snapshot.elements.map((element) => [element.id, element])),
        connections: Object.fromEntries(snapshot.connections.map((connection) => [connection.id, connection])),
      };
      state.revs = {};
      state.floor = snapshot.board.revision;
      state.revision = snapshot.board.revision;

      const buffered = [...state.buffer].sort((a, b) => a.revision - b.revision);
      state.buffer = [];
      state.syncing = false;
      for (const changes of buffered) {
        applyChangeSet(state, changes);
      }

      recomputeAll(state);
    },
    snapshotFailed(state, action: PayloadAction<BoardLoadFailure>) {
      state.phase = 'failed';
      state.failure = action.payload;
      state.syncing = false;
    },
    changeSetReceived(state, action: PayloadAction<BoardChangeSet>) {
      if (action.payload.boardId !== state.boardId) {
        return;
      }

      if (state.syncing) {
        state.buffer.push(action.payload);
        return;
      }

      recompute(state, applyChangeSet(state, action.payload));
    },
    /** Someone replaced the whole board. Operations aimed at the old content are dropped; a resync follows. */
    contentReplaced(state, action: PayloadAction<BoardReplaced>) {
      if (action.payload.boardId !== state.boardId) {
        return;
      }

      state.pending = [];
      state.syncing = true;
      state.buffer = [];
    },
    boardDetailsChanged(state, action: PayloadAction<BoardDetails>) {
      if (state.board && action.payload.boardId === state.boardId) {
        state.board.name = action.payload.name;
        state.board.archivedAt = action.payload.archivedAt;
      }
    },
    /** The person did something: show it now. The realtime middleware sends it. */
    opApplied(state, action: PayloadAction<PendingOp>) {
      state.pending.push(action.payload);
      recompute(state, touchedBy(action.payload.op));
    },
    /**
     * The hub answered. A success folds the operation into base until its broadcast brings the
     * committed images; a failure drops it, which rolls the view back.
     */
    opAcknowledged(state, action: PayloadAction<OpAcknowledgement>) {
      const index = state.pending.findIndex((pending) => pending.opId === action.payload.opId);
      if (index < 0) {
        return;
      }

      const { op } = state.pending[index]!;
      state.pending.splice(index, 1);
      const touched = touchedBy(op);
      if (action.payload.ok) {
        fold(state, op, touched);
      }

      recompute(state, touched);
    },
  },
});

export const {
  boardOpened,
  boardClosed,
  resyncStarted,
  snapshotLoaded,
  snapshotFailed,
  changeSetReceived,
  contentReplaced,
  boardDetailsChanged,
  opApplied,
  opAcknowledged,
} = boardSlice.actions;

/** Reads a value without leaving an Immer draft inside a new object. */
function peek<T>(value: T): T {
  return isDraft(value) ? (original(value as never) as T) : value;
}

function applyChangeSet(state: BoardState, changes: BoardChangeSet): Touched {
  const touched: Touched = { elements: [], connections: [] };
  if (changes.revision <= state.floor) {
    return touched;
  }

  const newer = (id: string) => (state.revs[id] ?? -1) < changes.revision;
  for (const element of changes.elements) {
    if (newer(element.id)) {
      state.revs[element.id] = changes.revision;
      state.base.elements[element.id] = element;
      touched.elements.push(element.id);
    }
  }

  for (const removed of changes.removedElements) {
    if (newer(removed.id)) {
      state.revs[removed.id] = changes.revision;
      delete state.base.elements[removed.id];
      touched.elements.push(removed.id);
    }
  }

  for (const connection of changes.connections) {
    if (newer(connection.id)) {
      state.revs[connection.id] = changes.revision;
      state.base.connections[connection.id] = connection;
      touched.connections.push(connection.id);
    }
  }

  for (const removed of changes.removedConnections) {
    if (newer(removed.id)) {
      state.revs[removed.id] = changes.revision;
      delete state.base.connections[removed.id];
      touched.connections.push(removed.id);
    }
  }

  state.revision = Math.max(state.revision, changes.revision);

  // Our own change came back: its effect is now in base, so it is no longer pending.
  if (changes.operationId) {
    const index = state.pending.findIndex((pending) => pending.opId === changes.operationId);
    if (index >= 0) {
      const confirmed = touchedBy(state.pending[index]!.op);
      state.pending.splice(index, 1);
      touched.elements.push(...confirmed.elements);
      touched.connections.push(...confirmed.connections);
    }
  }

  return touched;
}

function fold(state: BoardState, op: ContentOp, touched: Touched) {
  for (const id of touched.elements) {
    const next = applyToElement(op, id, peek(state.base.elements[id]));
    if (next) {
      state.base.elements[id] = next;
    } else {
      delete state.base.elements[id];
    }
  }

  for (const id of touched.connections) {
    const next = applyToConnection(op, id, peek(state.base.connections[id]));
    if (next) {
      state.base.connections[id] = next;
    } else {
      delete state.base.connections[id];
    }
  }
}

function viewOfElement(state: BoardState, id: string): BoardElement | undefined {
  let element: BoardElement | undefined = peek(state.base.elements[id]);
  for (const pending of state.pending) {
    element = applyToElement(pending.op, id, element);
  }

  return element;
}

function viewOfConnection(state: BoardState, id: string): BoardConnection | undefined {
  let connection: BoardConnection | undefined = peek(state.base.connections[id]);
  for (const pending of state.pending) {
    connection = applyToConnection(pending.op, id, connection);
  }

  return connection;
}

function recompute(state: BoardState, touched: Touched) {
  for (const id of new Set(touched.elements)) {
    const next = viewOfElement(state, id);
    const shown = peek(state.elements.entities[id]);
    if (!next) {
      if (shown) elementsAdapter.removeOne(state.elements, id);
    } else if (!shown || !sameElement(shown, next)) {
      elementsAdapter.setOne(state.elements, next);
    }
  }

  for (const id of new Set(touched.connections)) {
    const next = viewOfConnection(state, id);
    const shown = peek(state.connections.entities[id]);
    if (!next) {
      if (shown) connectionsAdapter.removeOne(state.connections, id);
    } else if (!shown || !sameConnection(shown, next)) {
      connectionsAdapter.setOne(state.connections, next);
    }
  }
}

/** Rebuilds the whole view, keeping the identity of every entity that did not change. */
function recomputeAll(state: BoardState) {
  const pendingTouched = state.pending.map((pending) => touchedBy(pending.op));
  const elementIds = new Set([...Object.keys(state.base.elements), ...pendingTouched.flatMap((touched) => touched.elements)]);
  const connectionIds = new Set([...Object.keys(state.base.connections), ...pendingTouched.flatMap((touched) => touched.connections)]);

  const elements: BoardElement[] = [];
  for (const id of elementIds) {
    const next = viewOfElement(state, id);
    if (next) {
      const shown = peek(state.elements.entities[id]);
      elements.push(shown && sameElement(shown, next) ? shown : next);
    }
  }

  const connections: BoardConnection[] = [];
  for (const id of connectionIds) {
    const next = viewOfConnection(state, id);
    if (next) {
      const shown = peek(state.connections.entities[id]);
      connections.push(shown && sameConnection(shown, next) ? shown : next);
    }
  }

  elementsAdapter.setAll(state.elements, elements);
  connectionsAdapter.setAll(state.connections, connections);
}
