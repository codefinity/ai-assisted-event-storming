'use client';

import { useEffect, useMemo, useRef } from 'react';
import { useAppDispatch, useAppSelector } from '@/store/hooks';
import { selectBoardSummary, selectElementEntities } from '../board/selectors';
import { searchChanged, selectionSet } from '../editor/editorSlice';
import { useBoardContext } from '../editor/BoardContext';
import { paletteFor, typeOf } from '../notation/notation';
import { selectSearchMatches } from './search';

const shownLimit = 200;

/** Find elements by text or type. Everything else is dimmed on the board while this is open. */
export function SearchPanel() {
  const { index, fitTo, focusElement } = useBoardContext();
  const dispatch = useAppDispatch();
  const criteria = useAppSelector((state) => state.editor.search);
  const level = useAppSelector((state) => selectBoardSummary(state)?.level);
  const found = useAppSelector((state) => selectSearchMatches(state, index));
  const entities = useAppSelector(selectElementEntities);
  const total = useAppSelector((state) => state.board.elements.ids.length);
  const input = useRef<HTMLInputElement>(null);
  const types = useMemo(() => (index && level ? [...paletteFor(index, level).primary, ...paletteFor(index, level).more] : []), [index, level]);

  useEffect(() => {
    input.current?.focus();
    input.current?.select();
  }, []);

  const toggleType = (id: string) =>
    dispatch(searchChanged({ types: criteria.types.includes(id) ? criteria.types.filter((type) => type !== id) : [...criteria.types, id] }));

  const go = (id: string) => {
    dispatch(selectionSet([id]));
    fitTo([id]);
    focusElement(id);
  };

  return (
    <div>
      <label htmlFor="board-search" className="visually-hidden">
        Search this board
      </label>
      <input
        ref={input}
        id="board-search"
        className="input"
        style={{ width: '100%' }}
        type="search"
        placeholder="Search text or type…"
        value={criteria.query}
        onChange={(event) => dispatch(searchChanged({ query: event.target.value }))}
        onKeyDown={(event) => {
          if (event.key === 'Enter' && found?.ids[0]) go(found.ids[0]);
        }}
      />
      <div className="search-types" role="group" aria-label="Only these types">
        {types.map((type) => (
          <button key={type.id} type="button" className="chip" aria-pressed={criteria.types.includes(type.id)} onClick={() => toggleType(type.id)}>
            <span className="chip-swatch" style={{ background: type.color }} aria-hidden="true" />
            {type.name}
          </button>
        ))}
      </div>
      <p className="muted" role="status">
        {found ? `${found.ids.length} of ${total} elements match` : `${total} elements on this board`}
      </p>
      {found && (
        <ul className="search-results" aria-label="Matches">
          {found.ids.slice(0, shownLimit).map((id) => {
            const element = entities[id];
            if (!element) return null;
            const type = typeOf(index, element.type);
            return (
              <li key={id}>
                <button type="button" className="search-result" onClick={() => go(id)}>
                  <span className="chip-swatch" style={{ background: element.color ?? type.color }} aria-hidden="true" />
                  <span>
                    {element.text || <em className="muted">No text</em>}
                    <span className="visually-hidden">, {type.name}</span>
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
