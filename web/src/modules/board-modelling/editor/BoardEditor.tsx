'use client';

import Link from 'next/link';
import { useCallback, useEffect, useMemo, useRef, type ReactNode } from 'react';
import { EmptyState, Spinner } from '@/shared/ui/feedback';
import { Icon } from '@/shared/ui/Icon';
import { useAppDispatch, useAppSelector, useAppStore } from '@/store/hooks';
import { boardClosed, boardOpened } from '../board/boardSlice';
import { boundsOf, elementsIn, selectAllElements, selectBoardState, selectCanEdit, selectElementEntities, selectPendingCount } from '../board/selectors';
import { Canvas } from '../canvas/Canvas';
import { useGetNotationQuery } from '../catalog/boardsApi';
import { Legend } from '../legend/Legend';
import { indexNotation } from '../notation/notation';
import { Palette } from '../palette/Palette';
import { SearchPanel } from '../search/SearchPanel';
import { HelpOverlay } from '../shortcuts/HelpOverlay';
import { useBoardShortcuts } from '../shortcuts/useBoardShortcuts';
import { BoardContext, type BoardContextValue } from './BoardContext';
import { clampZoom, focusMoved, panelClosed, toWorld, viewportChanged, zoomedAt } from './editorSlice';
import { Toolbar } from './Toolbar';
import './board.css';

export interface BoardEditorProps {
  boardId: string;
  /** Slots the page fills with collaboration features, so this module does not depend on them. */
  presence?: ReactNode;
  status?: ReactNode;
  worldOverlay?: ReactNode;
}

function SidePanel() {
  const panel = useAppSelector((state) => state.editor.panel);
  const dispatch = useAppDispatch();
  if (!panel) return null;
  const title = panel === 'legend' ? 'Legend' : 'Search and filter';

  return (
    <aside className="side-panel" aria-label={title}>
      <div className="side-panel-header">
        <h2>{title}</h2>
        <button type="button" className="btn btn-ghost btn-icon btn-sm" aria-label={`Close ${title.toLowerCase()}`} onClick={() => dispatch(panelClosed())}>
          <Icon name="close" />
        </button>
      </div>
      <div className="side-panel-body">{panel === 'legend' ? <Legend /> : <SearchPanel />}</div>
    </aside>
  );
}

export function BoardEditor({ boardId, presence, status, worldOverlay }: BoardEditorProps) {
  const dispatch = useAppDispatch();
  const store = useAppStore();
  const { data: notation, isError: notationFailed } = useGetNotationQuery();
  const index = useMemo(() => (notation ? indexNotation(notation) : undefined), [notation]);
  const canEdit = useAppSelector(selectCanEdit);
  const { phase, failure, board } = useAppSelector(selectBoardState);
  const pending = useAppSelector(selectPendingCount);
  const canvasRef = useRef<HTMLDivElement>(null);
  const pointerRef = useRef<{ x: number; y: number } | null>(null);
  const fitted = useRef(false);

  useEffect(() => {
    fitted.current = false;
    dispatch(boardOpened(boardId));
    return () => {
      dispatch(boardClosed());
    };
  }, [boardId, dispatch]);

  const size = useCallback(() => {
    const rect = canvasRef.current?.getBoundingClientRect();
    return { width: rect?.width ?? 1000, height: rect?.height ?? 700 };
  }, []);

  const fitTo = useCallback(
    (ids?: readonly string[]) => {
      const state = store.getState();
      const elements = ids ? elementsIn(selectElementEntities(state), ids) : selectAllElements(state);
      const bounds = boundsOf(elements);
      if (!bounds) return;
      const { width, height } = size();
      const padding = ids ? 160 : 64;
      const zoom = clampZoom(Math.min((width - padding * 2) / Math.max(bounds.width, 1), (height - padding * 2) / Math.max(bounds.height, 1), ids ? 1.25 : 1.5));
      dispatch(viewportChanged({ zoom, x: width / 2 - (bounds.x + bounds.width / 2) * zoom, y: height / 2 - (bounds.y + bounds.height / 2) * zoom }));
    },
    [dispatch, size, store],
  );

  const reveal = useCallback(
    (id: string) => {
      const state = store.getState();
      const element = state.board.elements.entities[id];
      if (!element) return;
      const { x, y, zoom } = state.editor.viewport;
      const { width, height } = size();
      const left = element.x * zoom + x;
      const top = element.y * zoom + y;
      const inView = left >= 0 && top >= 0 && left + element.width * zoom <= width && top + element.height * zoom <= height;
      if (!inView) {
        dispatch(viewportChanged({ zoom, x: width / 2 - (element.x + element.width / 2) * zoom, y: height / 2 - (element.y + element.height / 2) * zoom }));
      }
    },
    [dispatch, size, store],
  );

  const context = useMemo<BoardContextValue>(
    () => ({
      index,
      canEdit,
      canvasRef,
      pointerRef,
      fitTo,
      reveal,
      zoomBy: (factor) => {
        const { width, height } = size();
        dispatch(zoomedAt({ factor, sx: width / 2, sy: height / 2 }));
      },
      viewCenter: () => {
        const { width, height } = size();
        return toWorld(store.getState().editor.viewport, width / 2, height / 2);
      },
      focusElement: (id) => {
        dispatch(focusMoved(id));
        reveal(id);
        // Ids are UUIDs, so they need no escaping in the selector.
        requestAnimationFrame(() => canvasRef.current?.querySelector<HTMLElement>(`[data-element-id="${id}"]`)?.focus({ preventScroll: true }));
      },
    }),
    [index, canEdit, fitTo, reveal, size, dispatch, store],
  );

  useBoardShortcuts(context);

  // The first time the content is in, show all of it.
  useEffect(() => {
    if (phase === 'ready' && !fitted.current) {
      fitted.current = true;
      fitTo();
    }
  }, [phase, fitTo]);

  useEffect(() => {
    if (board) document.title = `${board.name} · EventStorming`;
  }, [board]);

  // Changes not yet confirmed by the server would be lost by leaving: ask first.
  useEffect(() => {
    if (pending === 0) return;
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
    };
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => window.removeEventListener('beforeunload', onBeforeUnload);
  }, [pending]);

  if (phase === 'failed') {
    return (
      <main className="page">
        <EmptyState title="This board cannot be opened" action={<Link href="/teams">Go to your teams</Link>}>
          {failure?.message ?? 'It may have been removed, or you may not have access to it.'}
        </EmptyState>
      </main>
    );
  }

  return (
    <BoardContext.Provider value={context}>
      <div className="board-page">
        <Toolbar presence={presence} status={status} />
        <Palette />
        <Canvas worldOverlay={worldOverlay} label={board ? `Board: ${board.name}` : 'Board'} />
        <SidePanel />
        <HelpOverlay />
        {(phase !== 'ready' || !index) && (
          <div className="canvas-overlay" style={{ gridArea: 'canvas' }}>
            {notationFailed ? <p>The element types could not be loaded. Reload the page to try again.</p> : <Spinner label="Opening the board" />}
          </div>
        )}
      </div>
    </BoardContext.Provider>
  );
}
