import type { AppThunk } from '@/store/types';
import { newId } from '@/shared/lib/ids';
import { opApplied } from '../board/boardSlice';
import type { BoardConnection, BoardElement } from '../model';
import type { ContentOp, ElementMove, ElementPatch, NewConnectionData, NewElementData } from '../board/ops';
import {
  connectionsTouching,
  elementsIn,
  selectAllConnections,
  selectCanEdit,
  selectConnectionById,
  selectElementById,
  selectElementEntities,
} from '../board/selectors';
import { stepRecorded, stepRedone, stepUndone } from '../history/historySlice';
import type { ElementType } from '../notation/notation';
import { selectionSet } from './editorSlice';

// Everything that changes content goes through here: each command works out the operation and its
// inverse from what is on screen, applies it, and records it for undo.

function perform(op: ContentOp, undo: ContentOp, label: string): AppThunk {
  return (dispatch) => {
    dispatch(opApplied({ opId: newId(), op }));
    dispatch(stepRecorded({ label, undo, redo: op }));
  };
}

export function dataOf(element: BoardElement): NewElementData {
  const { id, type, text, x, y, width, height, pivotal, color } = element;
  return { id, type, text, x, y, width, height, pivotal, color };
}

export function connectionDataOf(connection: BoardConnection): NewConnectionData {
  const { id, from, to, label } = connection;
  return { id, from, to, label };
}

/** A new element of a type, centred on a point, with the type's default size. */
export function newElementOf(type: ElementType, center: { x: number; y: number }, text = ''): NewElementData {
  const { width, height } = type.defaultSize;
  return {
    id: newId(),
    type: type.id,
    text,
    x: Math.round(center.x - width / 2),
    y: Math.round(center.y - height / 2),
    width,
    height,
    pivotal: false,
    color: null,
  };
}

export const undo = (): AppThunk => (dispatch, getState) => {
  const state = getState();
  const entry = state.history.past.at(-1);
  if (!entry || !selectCanEdit(state)) return;
  dispatch(opApplied({ opId: newId(), op: entry.undo }));
  dispatch(stepUndone());
};

export const redo = (): AppThunk => (dispatch, getState) => {
  const state = getState();
  const entry = state.history.future.at(-1);
  if (!entry || !selectCanEdit(state)) return;
  dispatch(opApplied({ opId: newId(), op: entry.redo }));
  dispatch(stepRedone());
};

export const createElements =
  (elements: NewElementData[], connections: NewConnectionData[] = [], label = elements.length === 1 ? 'Add element' : `Add ${elements.length} elements`): AppThunk =>
  (dispatch, getState) => {
    if (!selectCanEdit(getState()) || elements.length + connections.length === 0) return;
    dispatch(
      perform(
        { kind: 'add', elements, connections },
        { kind: 'delete', elementIds: elements.map((element) => element.id), connectionIds: connections.map((connection) => connection.id) },
        label,
      ),
    );
    if (elements.length > 0) dispatch(selectionSet(elements.map((element) => element.id)));
  };

export const updateElement =
  (id: string, patch: ElementPatch, label = 'Edit element'): AppThunk =>
  (dispatch, getState) => {
    const state = getState();
    const element = selectElementById(state, id);
    if (!element || !selectCanEdit(state)) return;

    const changed: ElementPatch = {};
    const previous: ElementPatch = {};
    for (const key of Object.keys(patch) as (keyof ElementPatch)[]) {
      const value = patch[key];
      if (value !== undefined && value !== element[key]) {
        (changed as Record<string, unknown>)[key] = value;
        (previous as Record<string, unknown>)[key] = element[key];
      }
    }

    if (Object.keys(changed).length === 0) return;

    // A position or a size travels as a pair.
    for (const [first, second] of [['x', 'y'], ['width', 'height']] as const) {
      if (changed[first] !== undefined || changed[second] !== undefined) {
        changed[first] ??= element[first];
        changed[second] ??= element[second];
        previous[first] ??= element[first];
        previous[second] ??= element[second];
      }
    }

    dispatch(perform({ kind: 'update', id, patch: changed }, { kind: 'update', id, patch: previous }, label));
  };

export const moveElements =
  (moves: ElementMove[], label = moves.length === 1 ? 'Move element' : `Move ${moves.length} elements`): AppThunk =>
  (dispatch, getState) => {
    const state = getState();
    if (!selectCanEdit(state)) return;
    const entities = selectElementEntities(state);
    const changed: ElementMove[] = [];
    const previous: ElementMove[] = [];
    for (const move of moves) {
      const element = entities[move.id];
      const x = Math.round(move.x);
      const y = Math.round(move.y);
      if (element && (element.x !== x || element.y !== y)) {
        changed.push({ id: move.id, x, y });
        previous.push({ id: move.id, x: element.x, y: element.y });
      }
    }

    if (changed.length === 0) return;
    dispatch(perform({ kind: 'move', moves: changed }, { kind: 'move', moves: previous }, label));
  };

export const nudgeElements =
  (ids: string[], dx: number, dy: number): AppThunk =>
  (dispatch, getState) => {
    const elements = elementsIn(selectElementEntities(getState()), ids);
    dispatch(moveElements(elements.map((element) => ({ id: element.id, x: element.x + dx, y: element.y + dy }))));
  };

export const resizeElement =
  (id: string, size: { width: number; height: number }): AppThunk =>
  (dispatch) =>
    dispatch(updateElement(id, { width: Math.round(size.width), height: Math.round(size.height) }, 'Resize element'));

/** Deletes elements and, with them, every connection that touches them - as the server does. */
export const deleteElements =
  (ids: string[]): AppThunk =>
  (dispatch, getState) => {
    const state = getState();
    if (!selectCanEdit(state)) return;
    const elements = elementsIn(selectElementEntities(state), ids);
    if (elements.length === 0) return;

    const cascaded = connectionsTouching(selectAllConnections(state), new Set(elements.map((element) => element.id)));
    dispatch(
      perform(
        { kind: 'delete', elementIds: elements.map((element) => element.id), connectionIds: cascaded.map((connection) => connection.id) },
        { kind: 'add', elements: elements.map(dataOf), connections: cascaded.map(connectionDataOf) },
        elements.length === 1 ? 'Delete element' : `Delete ${elements.length} elements`,
      ),
    );
  };

export const connectElements =
  (from: string, to: string): AppThunk =>
  (dispatch, getState) => {
    const state = getState();
    if (!selectCanEdit(state) || from === to) return;
    if (!selectElementById(state, from) || !selectElementById(state, to)) return;
    if (selectAllConnections(state).some((connection) => connection.from === from && connection.to === to)) return;

    const connection: NewConnectionData = { id: newId(), from, to, label: null };
    dispatch(perform({ kind: 'connect', connection }, { kind: 'disconnect', connectionIds: [connection.id] }, 'Connect'));
  };

export const deleteConnection =
  (id: string): AppThunk =>
  (dispatch, getState) => {
    const state = getState();
    const connection = selectConnectionById(state, id);
    if (!connection || !selectCanEdit(state)) return;
    dispatch(
      perform(
        { kind: 'disconnect', connectionIds: [id] },
        { kind: 'add', elements: [], connections: [connectionDataOf(connection)] },
        'Delete arrow',
      ),
    );
  };

/** Changing to a type that cannot be pivotal takes the pivotal mark off, as the server does. */
export const changeType =
  (id: string, type: ElementType): AppThunk =>
  (dispatch, getState) => {
    const element = selectElementById(getState(), id);
    if (!element) return;
    dispatch(updateElement(id, { type: type.id, pivotal: type.canBePivotal ? element.pivotal : false }, `Make it a ${type.name}`));
  };

export const togglePivotal =
  (id: string): AppThunk =>
  (dispatch, getState) => {
    const element = selectElementById(getState(), id);
    if (!element) return;
    dispatch(updateElement(id, { pivotal: !element.pivotal }, element.pivotal ? 'Unmark pivotal' : 'Mark pivotal'));
  };
