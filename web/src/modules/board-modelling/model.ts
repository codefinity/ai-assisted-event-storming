// The board's content as the server reports it: the REST snapshot and the hub's broadcasts share
// these flat shapes, so the web app handles one element type.

export type BoardLevel = 'big-picture' | 'process-modelling' | 'software-design';

export type BoardPermission = 'view' | 'edit';

export interface ActorRef {
  kind: 'account' | 'api-key';
  id: string;
  name: string;
}

export interface BoardSummary {
  id: string;
  teamId: string;
  name: string;
  level: BoardLevel;
  revision: number;
  elementCount: number;
  createdAt: string;
  createdBy: ActorRef;
  updatedAt: string;
  updatedBy: ActorRef;
  archivedAt: string | null;
}

export interface BoardElement {
  id: string;
  type: string;
  text: string;
  x: number;
  y: number;
  width: number;
  height: number;
  pivotal: boolean;
  color: string | null;
  version: number;
  updatedAt: string;
  updatedBy: ActorRef;
}

export interface BoardConnection {
  id: string;
  from: string;
  to: string;
  label: string | null;
  version: number;
}

export interface Removed {
  id: string;
  version: number;
}

/** A committed change: full post-images of what was added or changed, and what was removed. */
export interface BoardChangeSet {
  boardId: string;
  revision: number;
  operationId: string | null;
  actor: ActorRef;
  elements: BoardElement[];
  removedElements: Removed[];
  connections: BoardConnection[];
  removedConnections: Removed[];
}

export interface BoardSnapshot {
  board: BoardSummary;
  permission: BoardPermission;
  elements: BoardElement[];
  connections: BoardConnection[];
}

export interface BoardReplaced {
  boardId: string;
  revision: number;
  actor: ActorRef;
}

export interface BoardDetails {
  boardId: string;
  name: string;
  archivedAt: string | null;
}

export interface Rect {
  x: number;
  y: number;
  width: number;
  height: number;
}
