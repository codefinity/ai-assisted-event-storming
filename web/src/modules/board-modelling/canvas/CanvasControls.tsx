'use client';

import { useEffect, useMemo, useRef } from 'react';
import { shallowEqual } from 'react-redux';
import { Icon } from '@/shared/ui/Icon';
import { useAppDispatch, useAppSelector } from '@/store/hooks';
import type { RootState } from '@/store/types';
import { boundsOf, selectBoardSummary, selectConnectionById, selectElementEntities, selectCanEdit } from '../board/selectors';
import { duplicateSelection } from '../clipboard/clipboard';
import { changeType, deleteConnection, deleteElements, newElementOf, togglePivotal } from '../editor/commands';
import { draftStarted, editStarted, quickAddClosed, selectViewport } from '../editor/editorSlice';
import { useBoardContext } from '../editor/BoardContext';
import type { Rect } from '../model';
import { paletteFor, typeOf } from '../notation/notation';
import { arrowBetween } from './ConnectionLayer';

/** Flat, so a shallow comparison keeps the bar from re-rendering on unrelated changes. */
function selectedRect(state: RootState): (Rect & { count: number }) | null {
  const ids = Object.keys(state.editor.selected);
  if (ids.length === 0 || state.editor.drag || state.editor.resize || state.editor.editing || state.editor.draft) return null;
  const entities = selectElementEntities(state);
  const rect = boundsOf(ids.map((id) => entities[id]).filter((element) => element !== undefined));
  return rect ? { ...rect, count: ids.length } : null;
}

/** Actions for what is selected, floating just above it. */
export function SelectionBar() {
  const { index } = useBoardContext();
  const dispatch = useAppDispatch();
  const canEdit = useAppSelector(selectCanEdit);
  const viewport = useAppSelector(selectViewport, shallowEqual);
  const selection = useAppSelector(selectedRect, shallowEqual);
  const singleId = useAppSelector((state) => {
    const ids = Object.keys(state.editor.selected);
    return ids.length === 1 ? ids[0]! : null;
  });
  const single = useAppSelector((state) => (singleId ? state.board.elements.entities[singleId] : undefined));
  const connectionId = useAppSelector((state) => state.editor.selectedConnection);
  const connection = useAppSelector((state) => (connectionId ? selectConnectionById(state, connectionId) : undefined));
  const connectionEnds = useAppSelector(
    (state) => {
      if (!connection) return null;
      const from = state.board.elements.entities[connection.from];
      const to = state.board.elements.entities[connection.to];
      return from && to ? arrowBetween(from, to) : null;
    },
    shallowEqual,
  );

  if (!canEdit) return null;

  if (connection && connectionEnds) {
    const left = ((connectionEnds.x1 + connectionEnds.x2) / 2) * viewport.zoom + viewport.x;
    const top = ((connectionEnds.y1 + connectionEnds.y2) / 2) * viewport.zoom + viewport.y;
    return (
      <div className="floating-bar" style={{ left, top }} data-ui="true" role="toolbar" aria-label="Arrow">
        <button type="button" className="btn btn-ghost btn-sm btn-danger" onClick={() => dispatch(deleteConnection(connection.id))}>
          <Icon name="trash" />
          Delete arrow
        </button>
      </div>
    );
  }

  if (!selection) return null;
  const left = (selection.x + selection.width / 2) * viewport.zoom + viewport.x;
  const top = selection.y * viewport.zoom + viewport.y;

  if (single) {
    const type = typeOf(index, single.type);
    return (
      <div className="floating-bar" style={{ left, top }} data-ui="true" role="toolbar" aria-label={`${type.name} actions`}>
        <label className="visually-hidden" htmlFor="selection-type">
          Element type
        </label>
        <select
          id="selection-type"
          className="select"
          value={single.type}
          onChange={(event) => {
            const next = index?.byId.get(event.target.value);
            if (next) dispatch(changeType(single.id, next));
          }}
        >
          {index?.notation.types.map((candidate) => (
            <option key={candidate.id} value={candidate.id}>
              {candidate.name}
            </option>
          ))}
        </select>
        {type.canBePivotal && (
          <button type="button" className="btn btn-ghost btn-sm" aria-pressed={single.pivotal} onClick={() => dispatch(togglePivotal(single.id))} title="Pivotal events mark the big turning points of the timeline">
            <Icon name="flag" />
            Pivotal
          </button>
        )}
        <button type="button" className="btn btn-ghost btn-sm" onClick={() => dispatch(editStarted(single.id))}>
          Edit text
        </button>
        <button type="button" className="btn btn-ghost btn-icon btn-sm" aria-label="Duplicate" title="Duplicate (Ctrl+D)" onClick={() => dispatch(duplicateSelection())}>
          <Icon name="copy" />
        </button>
        <button type="button" className="btn btn-ghost btn-icon btn-sm btn-danger" aria-label="Delete" title="Delete (Del)" onClick={() => dispatch(deleteElements([single.id]))}>
          <Icon name="trash" />
        </button>
      </div>
    );
  }

  return (
    <div className="floating-bar" style={{ left, top }} data-ui="true" role="toolbar" aria-label="Selection actions">
      <span className="badge">{selection.count} selected</span>
      <button type="button" className="btn btn-ghost btn-sm" onClick={() => dispatch(duplicateSelection())}>
        <Icon name="copy" />
        Duplicate
      </button>
      <SelectedDelete />
    </div>
  );
}

function SelectedDelete() {
  const dispatch = useAppDispatch();
  const ids = useAppSelector((state) => Object.keys(state.editor.selected), shallowEqual);
  return (
    <button type="button" className="btn btn-ghost btn-sm btn-danger" onClick={() => dispatch(deleteElements(ids))}>
      <Icon name="trash" />
      Delete
    </button>
  );
}

/** Double-click on empty space: choose what to put there. A type's shortcut letter picks it. */
export function QuickAdd() {
  const { index } = useBoardContext();
  const dispatch = useAppDispatch();
  const point = useAppSelector((state) => state.editor.quickAdd, shallowEqual);
  const viewport = useAppSelector(selectViewport, shallowEqual);
  const level = useAppSelector((state) => selectBoardSummary(state)?.level);
  const list = useRef<HTMLDivElement>(null);
  const types = useMemo(() => (index && level ? paletteFor(index, level).primary : []), [index, level]);

  useEffect(() => {
    if (point) list.current?.querySelector('button')?.focus();
  }, [point]);

  if (!point || !index) return null;

  const pick = (typeId: string) => {
    const type = index.byId.get(typeId);
    if (type) dispatch(draftStarted(newElementOf(type, point)));
  };

  return (
    <div
      ref={list}
      className="quick-add"
      data-ui="true"
      role="menu"
      aria-label="Add an element here"
      style={{ left: point.x * viewport.zoom + viewport.x, top: point.y * viewport.zoom + viewport.y }}
      onKeyDown={(event) => {
        event.stopPropagation();
        const buttons = [...(list.current?.querySelectorAll('button') ?? [])];
        const at = buttons.indexOf(document.activeElement as HTMLButtonElement);
        if (event.key === 'Escape') dispatch(quickAddClosed());
        else if (event.key === 'ArrowDown') buttons[(at + 1) % buttons.length]?.focus();
        else if (event.key === 'ArrowUp') buttons[(at - 1 + buttons.length) % buttons.length]?.focus();
        else {
          const type = index.byShortcut.get(event.key.toLowerCase());
          if (type && !event.ctrlKey && !event.metaKey) {
            event.preventDefault();
            pick(type.id);
          }
        }
      }}
    >
      {types.map((type) => (
        <button key={type.id} type="button" role="menuitem" className="quick-add-item" onClick={() => pick(type.id)}>
          <span className="palette-swatch" style={{ background: type.color }}>
            <Icon name={type.icon} size={12} />
          </span>
          <span className="palette-name">{type.name}</span>
          {type.shortcut && <kbd>{type.shortcut}</kbd>}
        </button>
      ))}
    </div>
  );
}

export function ZoomControls() {
  const { fitTo, zoomBy } = useBoardContext();
  const zoom = useAppSelector((state) => state.editor.viewport.zoom);
  return (
    <div className="zoom-controls" data-ui="true" role="toolbar" aria-label="Zoom">
      <button type="button" className="btn btn-ghost btn-icon btn-sm" aria-label="Zoom out" title="Zoom out (-)" onClick={() => zoomBy(1 / 1.2)}>
        <Icon name="minus" />
      </button>
      <button type="button" className="btn btn-ghost btn-sm zoom-value" aria-label={`Zoom ${Math.round(zoom * 100)}%, reset to 100%`} title="Reset to 100% (0)" onClick={() => zoomBy(1 / zoom)}>
        {Math.round(zoom * 100)}%
      </button>
      <button type="button" className="btn btn-ghost btn-icon btn-sm" aria-label="Zoom in" title="Zoom in (+)" onClick={() => zoomBy(1.2)}>
        <Icon name="plus" />
      </button>
      <button type="button" className="btn btn-ghost btn-icon btn-sm" aria-label="Fit the board" title="Fit everything (Shift+1)" onClick={() => fitTo()}>
        <Icon name="fit" />
      </button>
    </div>
  );
}
