import type { AppThunk } from '@/store/types';
import { newId } from '@/shared/lib/ids';
import type { NewConnectionData, NewElementData } from '../board/ops';
import { boundsOf, elementsIn, selectAllConnections, selectElementEntities } from '../board/selectors';
import { connectionDataOf, createElements, dataOf, deleteElements, newElementOf } from '../editor/commands';
import type { ElementType } from '../notation/notation';

/** What a copy puts on the clipboard, as plain-text JSON, so it pastes into another board or tab. */
export interface ClipboardPayload {
  format: 'eventstorming/elements@1';
  elements: NewElementData[];
  connections: NewConnectionData[];
}

const format = 'eventstorming/elements@1';

/** Pasting the same thing again lands a little further along each time. */
const pasteStep = 24;

let lastPaste: { signature: string; count: number } | null = null;

export function payloadOf(state: Parameters<typeof selectElementEntities>[0], ids: readonly string[]): ClipboardPayload | null {
  const elements = elementsIn(selectElementEntities(state), ids);
  if (elements.length === 0) return null;

  const inside = new Set(elements.map((element) => element.id));
  const connections = selectAllConnections(state).filter((connection) => inside.has(connection.from) && inside.has(connection.to));
  return { format, elements: elements.map(dataOf), connections: connections.map(connectionDataOf) };
}

export function parsePayload(text: string): ClipboardPayload | null {
  try {
    const value: unknown = JSON.parse(text);
    if (typeof value === 'object' && value !== null && (value as ClipboardPayload).format === format && Array.isArray((value as ClipboardPayload).elements)) {
      return value as ClipboardPayload;
    }
  } catch {
    // Not ours.
  }

  return null;
}

/**
 * Plain text pastes as a row of stickies of the default type, one per line: a quick way to bring in
 * a list of events written elsewhere.
 */
export function payloadFromText(text: string, type: ElementType): ClipboardPayload | null {
  const lines = text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
    .slice(0, 100);
  if (lines.length === 0) return null;

  const gap = 24;
  const elements = lines.map((line, index) =>
    newElementOf(type, { x: index * (type.defaultSize.width + gap) + type.defaultSize.width / 2, y: type.defaultSize.height / 2 }, line.slice(0, type.maxTextLength)),
  );
  return { format, elements, connections: [] };
}

/** New ids for everything, the connections remapped, and the group moved to `at` (its top-left corner). */
export function placePayload(payload: ClipboardPayload, at: { x: number; y: number } | null): { elements: NewElementData[]; connections: NewConnectionData[] } {
  const bounds = boundsOf(payload.elements)!;
  let dx: number;
  let dy: number;
  if (at) {
    dx = at.x - bounds.x;
    dy = at.y - bounds.y;
  } else {
    const signature = payload.elements.map((element) => element.id).join(',');
    lastPaste = lastPaste?.signature === signature ? { signature, count: lastPaste.count + 1 } : { signature, count: 1 };
    dx = pasteStep * lastPaste.count;
    dy = pasteStep * lastPaste.count;
  }

  const ids = new Map(payload.elements.map((element) => [element.id, newId()]));
  const elements = payload.elements.map((element) => ({
    ...element,
    id: ids.get(element.id)!,
    x: Math.round(element.x + dx),
    y: Math.round(element.y + dy),
  }));
  const connections = payload.connections
    .filter((connection) => ids.has(connection.from) && ids.has(connection.to))
    .map((connection) => ({ ...connection, id: newId(), from: ids.get(connection.from)!, to: ids.get(connection.to)! }));
  return { elements, connections };
}

export const pastePayload =
  (payload: ClipboardPayload, at: { x: number; y: number } | null): AppThunk =>
  (dispatch) => {
    const placed = placePayload(payload, at);
    dispatch(createElements(placed.elements, placed.connections, placed.elements.length === 1 ? 'Paste element' : `Paste ${placed.elements.length} elements`));
  };

export const duplicateSelection = (): AppThunk => (dispatch, getState) => {
  const state = getState();
  const payload = payloadOf(state, Object.keys(state.editor.selected));
  if (payload) dispatch(pastePayload(payload, null));
};

export const cutSelection = (): AppThunk => (dispatch, getState) => {
  dispatch(deleteElements(Object.keys(getState().editor.selected)));
};
