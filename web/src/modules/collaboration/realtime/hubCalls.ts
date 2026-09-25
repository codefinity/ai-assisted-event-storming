import type { PendingOp } from '@/modules/board-modelling/board/ops';
import type { HubFailure } from '@/shared/api/problems';
import type { Participant } from '../presence/presenceSlice';

/** The hub's answer to an editing call. The change itself arrives separately, as a broadcast. */
export interface OperationResult {
  ok: boolean;
  operationId: string | null;
  revision: number;
  failures: HubFailure[];
}

export interface JoinBoardResponse {
  ok: boolean;
  you: Participant | null;
  participants: Participant[];
  failures: HubFailure[];
}

export interface HubCall {
  method: string;
  request: Record<string, unknown>;
}

/** Maps an operation onto the board hub method and request that carry it out. */
export function toHubCall(boardId: string, { opId, op }: PendingOp): HubCall {
  const base = { boardId, operationId: opId };
  switch (op.kind) {
    case 'add':
      return {
        method: 'AddElements',
        request: {
          ...base,
          elements: op.elements.map(({ id, type, text, x, y, width, height, pivotal, color }) => ({ id, type, text, x, y, width, height, pivotal, color })),
          connections: op.connections.map(({ id, from, to, label }) => ({ id, from, to, label })),
        },
      };
    case 'update': {
      const { patch } = op;
      const request: Record<string, unknown> = { ...base, elementId: op.id };
      if (patch.text !== undefined) request.text = patch.text;
      if (patch.type !== undefined) request.type = patch.type;
      if (patch.pivotal !== undefined) request.pivotal = patch.pivotal;
      // The hub clears a custom color when it is sent as an empty string.
      if (patch.color !== undefined) request.color = patch.color ?? '';
      if (patch.x !== undefined || patch.y !== undefined) {
        request.x = patch.x;
        request.y = patch.y;
      }
      if (patch.width !== undefined || patch.height !== undefined) {
        request.width = patch.width;
        request.height = patch.height;
      }
      return { method: 'UpdateElement', request };
    }
    case 'move':
      return { method: 'MoveElements', request: { ...base, moves: op.moves.map((move) => ({ elementId: move.id, x: move.x, y: move.y })) } };
    case 'delete':
      return { method: 'DeleteElements', request: { ...base, elementIds: op.elementIds } };
    case 'connect':
      return { method: 'AddConnection', request: { ...base, ...op.connection } };
    case 'disconnect':
      return { method: 'DeleteConnections', request: { ...base, connectionIds: op.connectionIds } };
  }
}
