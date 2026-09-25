'use client';

import { memo } from 'react';
import { shallowEqual } from 'react-redux';
import { useAppSelector } from '@/store/hooks';
import type { RootState } from '@/store/types';
import { selectConnectionById, selectConnectionIds, selectContentBounds, selectPivotalIds } from '../board/selectors';
import { selectDragDx, selectDragDy, selectResizeOf } from '../editor/editorSlice';
import type { Rect } from '../model';

/** Where an element is drawn right now, following a drag or resize in progress. */
function liveRect(state: RootState, id: string): Rect | null {
  const element = state.board.elements.entities[id];
  if (!element) return null;
  const resize = selectResizeOf(state, id);
  return {
    x: element.x + selectDragDx(state, id),
    y: element.y + selectDragDy(state, id),
    width: resize?.width ?? element.width,
    height: resize?.height ?? element.height,
  };
}

/** The point where the line from a rectangle's centre towards (tx, ty) leaves the rectangle. */
export function edgePoint(rect: Rect, tx: number, ty: number): { x: number; y: number } {
  const cx = rect.x + rect.width / 2;
  const cy = rect.y + rect.height / 2;
  const dx = tx - cx;
  const dy = ty - cy;
  if (dx === 0 && dy === 0) return { x: cx, y: cy };
  const scale = Math.min(Math.abs(rect.width / 2 / (dx || Number.EPSILON)), Math.abs(rect.height / 2 / (dy || Number.EPSILON)));
  return { x: cx + dx * scale, y: cy + dy * scale };
}

export function arrowBetween(from: Rect, to: Rect): { x1: number; y1: number; x2: number; y2: number } {
  const start = edgePoint(from, to.x + to.width / 2, to.y + to.height / 2);
  const end = edgePoint(to, from.x + from.width / 2, from.y + from.height / 2);
  return { x1: start.x, y1: start.y, x2: end.x, y2: end.y };
}

const ConnectionPath = memo(function ConnectionPath({ id }: { id: string }) {
  const connection = useAppSelector((state) => selectConnectionById(state, id));
  const from = useAppSelector((state) => (connection ? liveRect(state, connection.from) : null), shallowEqual);
  const to = useAppSelector((state) => (connection ? liveRect(state, connection.to) : null), shallowEqual);
  const selected = useAppSelector((state) => state.editor.selectedConnection === id);
  // A connection whose end is not on the board (yet, or any more) is not drawn.
  if (!connection || !from || !to) return null;

  const { x1, y1, x2, y2 } = arrowBetween(from, to);
  const path = `M ${x1} ${y1} L ${x2} ${y2}`;
  return (
    <g className={selected ? 'connection connection-selected' : 'connection'} data-connection-id={id}>
      <path className="connection-hit" d={path} />
      <path className="connection-line" d={path} markerEnd={selected ? 'url(#arrow-selected)' : 'url(#arrow)'} />
      {connection.label && (
        <text className="connection-label" x={(x1 + x2) / 2} y={(y1 + y2) / 2 - 6} textAnchor="middle">
          {connection.label}
        </text>
      )}
    </g>
  );
});

const PivotalLine = memo(function PivotalLine({ id }: { id: string }) {
  const rect = useAppSelector((state) => liveRect(state, id), shallowEqual);
  const bounds = useAppSelector(selectContentBounds, shallowEqual);
  if (!rect || !bounds) return null;
  // A pivotal event divides the timeline: its line runs the full height of the content, behind the stickies.
  const x = rect.x + rect.width / 2;
  return <line className="pivotal-line" x1={x} x2={x} y1={bounds.y - 60} y2={bounds.y + bounds.height + 60} />;
});

function DraftConnection() {
  const connecting = useAppSelector((state) => state.editor.connecting, shallowEqual);
  const from = useAppSelector((state) => (connecting ? liveRect(state, connecting.from) : null), shallowEqual);
  if (!connecting || !from) return null;
  const start = edgePoint(from, connecting.x, connecting.y);
  return <path className="connection-draft" d={`M ${start.x} ${start.y} L ${connecting.x} ${connecting.y}`} markerEnd="url(#arrow-selected)" />;
}

/** Arrows and pivotal-event dividers, in one SVG in world coordinates. */
export function ConnectionLayer() {
  const ids = useAppSelector(selectConnectionIds);
  const pivotal = useAppSelector(selectPivotalIds);

  return (
    <svg className="connections" aria-hidden="true">
      <defs>
        <marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse">
          <path d="M 0 0 L 10 5 L 0 10 z" fill="#495057" />
        </marker>
        <marker id="arrow-selected" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="8" markerHeight="8" orient="auto-start-reverse">
          <path d="M 0 0 L 10 5 L 0 10 z" fill="#1d4ed8" />
        </marker>
      </defs>
      {pivotal.map((id) => (
        <PivotalLine key={id} id={id} />
      ))}
      {ids.map((id) => (
        <ConnectionPath key={id} id={id} />
      ))}
      <DraftConnection />
    </svg>
  );
}
