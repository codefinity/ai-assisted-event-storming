import { createSelector, lruMemoize } from '@reduxjs/toolkit';
import { shallowEqual } from 'react-redux';
import type { BoardConnection, BoardElement, Rect } from '../model';
import { layerOf, typeOf, type NotationIndex } from '../notation/notation';
import { connectionsAdapter, elementsAdapter, type BoardState } from './boardSlice';

type WithBoard = { board: BoardState };

export const selectBoardState = (state: WithBoard) => state.board;
export const selectBoardSummary = (state: WithBoard) => state.board.board;
export const selectBoardPhase = (state: WithBoard) => state.board.phase;
export const selectPendingCount = (state: WithBoard) => state.board.pending.length;

const elementSelectors = elementsAdapter.getSelectors((state: WithBoard) => state.board.elements);
const connectionSelectors = connectionsAdapter.getSelectors((state: WithBoard) => state.board.connections);

export const selectElementById = elementSelectors.selectById;
export const selectElementEntities = elementSelectors.selectEntities;
export const selectAllElements = elementSelectors.selectAll;
export const selectConnectionById = connectionSelectors.selectById;
export const selectConnectionEntities = connectionSelectors.selectEntities;
export const selectAllConnections = connectionSelectors.selectAll;

/** Editing is possible only with edit permission on a board that is not archived. */
export const selectCanEdit = (state: WithBoard) =>
  state.board.permission === 'edit' && state.board.board !== null && state.board.board.archivedAt === null;

export interface LayeredIds {
  lanes: string[];
  areas: string[];
  stickies: string[];
}

/**
 * Element ids by drawing layer. The arrays keep their identity while only positions change, so
 * moving a sticky re-renders that sticky and nothing else.
 */
export const selectLayeredIds = createSelector(
  [selectElementEntities, (state: WithBoard) => state.board.elements.ids, (_state: WithBoard, index: NotationIndex | undefined) => index],
  (entities, ids, index): LayeredIds => {
    const layers: LayeredIds = { lanes: [], areas: [], stickies: [] };
    for (const id of ids) {
      const element = entities[id];
      if (!element) continue;
      const layer = layerOf(typeOf(index, element.type));
      (layer === 0 ? layers.lanes : layer === 1 ? layers.areas : layers.stickies).push(id);
    }

    return layers;
  },
  {
    memoize: lruMemoize,
    memoizeOptions: {
      resultEqualityCheck: (a: LayeredIds, b: LayeredIds) =>
        shallowEqual(a.lanes, b.lanes) && shallowEqual(a.areas, b.areas) && shallowEqual(a.stickies, b.stickies),
    },
  },
);

/** The adapter keeps this array's identity until a connection is added or removed. */
export const selectConnectionIds = (state: WithBoard) => state.board.connections.ids;

/** Pivotal events, for the dashed timeline dividers. */
export const selectPivotalIds = createSelector(
  [selectAllElements],
  (elements) => elements.filter((element) => element.pivotal).map((element) => element.id),
  { memoize: lruMemoize, memoizeOptions: { resultEqualityCheck: shallowEqual } },
);

/** Reading order on a timeline: left to right, then top to bottom. Used for Tab and screen readers. */
export const selectTimelineOrder = createSelector(
  [selectAllElements],
  (elements) =>
    [...elements]
      .sort((a, b) => a.x - b.x || a.y - b.y || a.id.localeCompare(b.id))
      .map((element) => element.id),
  { memoize: lruMemoize, memoizeOptions: { resultEqualityCheck: shallowEqual } },
);

export function boundsOf(elements: readonly Rect[]): Rect | null {
  if (elements.length === 0) {
    return null;
  }

  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;
  for (const element of elements) {
    minX = Math.min(minX, element.x);
    minY = Math.min(minY, element.y);
    maxX = Math.max(maxX, element.x + element.width);
    maxY = Math.max(maxY, element.y + element.height);
  }

  return { x: minX, y: minY, width: maxX - minX, height: maxY - minY };
}

export const selectContentBounds = createSelector([selectAllElements], (elements) => boundsOf(elements));

/** Connections that touch any of the given elements: deleting the elements removes these too. */
export function connectionsTouching(connections: readonly BoardConnection[], elementIds: ReadonlySet<string>): BoardConnection[] {
  return connections.filter((connection) => elementIds.has(connection.from) || elementIds.has(connection.to));
}

export function elementsIn(entities: Record<string, BoardElement | undefined>, ids: readonly string[]): BoardElement[] {
  return ids.map((id) => entities[id]).filter((element): element is BoardElement => element !== undefined);
}
