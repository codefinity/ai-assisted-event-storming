import type { BoardConnection, BoardElement } from '../model';

// A content operation is one thing the person did to the board, in the terms the board hub accepts.
// It is applied to the local view at once (optimistically) and sent; the server's broadcast of what
// it committed then replaces the optimistic result.

export interface NewElementData {
  id: string;
  type: string;
  text: string;
  x: number;
  y: number;
  width: number;
  height: number;
  pivotal: boolean;
  color: string | null;
}

export interface NewConnectionData {
  id: string;
  from: string;
  to: string;
  label: string | null;
}

/** Only the fields present change. `color: null` clears a custom color. */
export interface ElementPatch {
  text?: string;
  type?: string;
  x?: number;
  y?: number;
  width?: number;
  height?: number;
  pivotal?: boolean;
  color?: string | null;
}

export interface ElementMove {
  id: string;
  x: number;
  y: number;
}

export type ContentOp =
  | { kind: 'add'; elements: NewElementData[]; connections: NewConnectionData[] }
  | { kind: 'update'; id: string; patch: ElementPatch }
  | { kind: 'move'; moves: ElementMove[] }
  /** `connectionIds` are the connections the server will cascade-delete, removed locally at once. */
  | { kind: 'delete'; elementIds: string[]; connectionIds: string[] }
  | { kind: 'connect'; connection: NewConnectionData }
  | { kind: 'disconnect'; connectionIds: string[] };

export interface PendingOp {
  opId: string;
  op: ContentOp;
}

export interface Touched {
  elements: string[];
  connections: string[];
}

export function touchedBy(op: ContentOp): Touched {
  switch (op.kind) {
    case 'add':
      return { elements: op.elements.map((element) => element.id), connections: op.connections.map((connection) => connection.id) };
    case 'update':
      return { elements: [op.id], connections: [] };
    case 'move':
      return { elements: op.moves.map((move) => move.id), connections: [] };
    case 'delete':
      return { elements: op.elementIds, connections: op.connectionIds };
    case 'connect':
      return { elements: [], connections: [op.connection.id] };
    case 'disconnect':
      return { elements: [], connections: op.connectionIds };
  }
}

const nobody = { kind: 'account', id: '', name: '' } as const;

export function elementFrom(data: NewElementData): BoardElement {
  return { ...data, version: 0, updatedAt: '', updatedBy: nobody };
}

export function connectionFrom(data: NewConnectionData): BoardConnection {
  return { ...data, version: 0 };
}

/** What one element looks like once the op is applied. Undefined means it does not exist. */
export function applyToElement(op: ContentOp, id: string, element: BoardElement | undefined): BoardElement | undefined {
  switch (op.kind) {
    case 'add': {
      // An add is idempotent: if the element already exists, the server's copy stands.
      if (element) {
        return element;
      }

      const data = op.elements.find((candidate) => candidate.id === id);
      return data ? elementFrom(data) : undefined;
    }
    case 'update':
      return element && op.id === id ? patched(element, op.patch) : element;
    case 'move': {
      const move = element ? op.moves.find((candidate) => candidate.id === id) : undefined;
      return element && move ? { ...element, x: move.x, y: move.y } : element;
    }
    case 'delete':
      return op.elementIds.includes(id) ? undefined : element;
    default:
      return element;
  }
}

export function applyToConnection(op: ContentOp, id: string, connection: BoardConnection | undefined): BoardConnection | undefined {
  switch (op.kind) {
    case 'add': {
      if (connection) {
        return connection;
      }

      const data = op.connections.find((candidate) => candidate.id === id);
      return data ? connectionFrom(data) : undefined;
    }
    case 'connect':
      return connection ?? (op.connection.id === id ? connectionFrom(op.connection) : undefined);
    case 'delete':
    case 'disconnect':
      return op.connectionIds.includes(id) ? undefined : connection;
    default:
      return connection;
  }
}

function patched(element: BoardElement, patch: ElementPatch): BoardElement {
  const next = { ...element };
  if (patch.text !== undefined) next.text = patch.text;
  if (patch.type !== undefined) next.type = patch.type;
  if (patch.x !== undefined) next.x = patch.x;
  if (patch.y !== undefined) next.y = patch.y;
  if (patch.width !== undefined) next.width = patch.width;
  if (patch.height !== undefined) next.height = patch.height;
  if (patch.pivotal !== undefined) next.pivotal = patch.pivotal;
  if (patch.color !== undefined) next.color = patch.color;
  return next;
}

/** Shallow equality for the view: a changed field means a new object, an unchanged one keeps its identity. */
export function sameElement(a: BoardElement, b: BoardElement): boolean {
  return (
    a.id === b.id &&
    a.type === b.type &&
    a.text === b.text &&
    a.x === b.x &&
    a.y === b.y &&
    a.width === b.width &&
    a.height === b.height &&
    a.pivotal === b.pivotal &&
    a.color === b.color &&
    a.version === b.version
  );
}

export function sameConnection(a: BoardConnection, b: BoardConnection): boolean {
  return a.id === b.id && a.from === b.from && a.to === b.to && a.label === b.label && a.version === b.version;
}
