import { createSelector } from '@reduxjs/toolkit';
import type { RootState } from '@/store/types';
import { selectAllElements } from '../board/selectors';
import type { BoardElement } from '../model';
import { typeOf, type NotationIndex } from '../notation/notation';

export interface SearchCriteria {
  query: string;
  types: readonly string[];
}

export function matches(element: BoardElement, criteria: SearchCriteria, index: NotationIndex | undefined): boolean {
  if (criteria.types.length > 0 && !criteria.types.includes(element.type)) return false;
  const query = criteria.query.trim().toLowerCase();
  if (!query) return true;
  return element.text.toLowerCase().includes(query) || typeOf(index, element.type).name.toLowerCase().includes(query);
}

/**
 * The ids of the elements that match while the search panel is open, in timeline order; null when
 * nothing is being filtered (so nothing is dimmed).
 */
export const selectSearchMatches = createSelector(
  [
    selectAllElements,
    (state: RootState) => state.editor.search,
    (state: RootState) => state.editor.panel === 'search',
    (_state: RootState, index: NotationIndex | undefined) => index,
  ],
  (elements, criteria, open, index): { ids: string[]; set: ReadonlySet<string> } | null => {
    if (!open || (criteria.query.trim() === '' && criteria.types.length === 0)) return null;
    const ids = elements
      .filter((element) => matches(element, criteria, index))
      .sort((a, b) => a.x - b.x || a.y - b.y)
      .map((element) => element.id);
    return { ids, set: new Set(ids) };
  },
);

export const selectIsDimmed = (state: RootState, id: string, index: NotationIndex | undefined) => {
  const found = selectSearchMatches(state, index);
  return found !== null && !found.set.has(id);
};
