'use client';

import { useEffect, useRef, useState, type DragEvent, type KeyboardEvent, type PointerEvent, type ReactNode } from 'react';
import { shallowEqual } from 'react-redux';
import { useAppDispatch, useAppSelector, useAppStore } from '@/store/hooks';
import { selectBoardPhase, selectLayeredIds, selectTimelineOrder } from '../board/selectors';
import { connectElements, moveElements, newElementOf, resizeElement } from '../editor/commands';
import {
  connectingChanged,
  connectionSelected,
  dragEnded,
  dragMoved,
  dragStarted,
  draftStarted,
  editStarted,
  focusMoved,
  marqueeChanged,
  panned,
  pointerMoved,
  quickAddClosed,
  quickAddOpened,
  resizeChanged,
  selectDraft,
  selectionCleared,
  selectionSet,
  selectionToggled,
  selectViewport,
  toWorld,
  zoomedAt,
} from '../editor/editorSlice';
import { useBoardContext } from '../editor/BoardContext';
import type { Rect } from '../model';
import { layerOf, typeOf } from '../notation/notation';
import { ConnectionLayer } from './ConnectionLayer';
import { DraftNode, ElementNode } from './ElementNode';
import { QuickAdd, SelectionBar, ZoomControls } from './CanvasControls';

/** The palette puts a type id on the drag under this type. */
export const elementTypeMime = 'application/x-eventstorming-type';

type Point = { x: number; y: number };

type Gesture =
  | { kind: 'pan'; lastX: number; lastY: number; startX: number; startY: number; moved: boolean }
  | { kind: 'press'; id: string; startX: number; startY: number; world: Point; additive: boolean; wasSelected: boolean }
  | { kind: 'drag'; world: Point; ids: string[] }
  | { kind: 'marquee'; world: Point; base: string[] }
  | { kind: 'resize'; id: string; world: Point; width: number; height: number; min: number }
  | { kind: 'connect'; from: string };

const dragThreshold = 4;
const cursorInterval = 40;

function normalized(a: Point, b: Point): Rect {
  return { x: Math.min(a.x, b.x), y: Math.min(a.y, b.y), width: Math.abs(a.x - b.x), height: Math.abs(a.y - b.y) };
}

function intersects(a: Rect, b: Rect) {
  return a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height;
}

function contains(outer: Rect, inner: Rect) {
  return inner.x >= outer.x && inner.y >= outer.y && inner.x + inner.width <= outer.x + outer.width && inner.y + inner.height <= outer.y + outer.height;
}

function Grid() {
  const { x, y, zoom } = useAppSelector(selectViewport, shallowEqual);
  // Dots every 24px, thinned out when zoomed far out so the grid never turns into noise.
  let step = 24 * zoom;
  while (step < 12) step *= 4;
  return <div className="canvas-grid" aria-hidden="true" style={{ backgroundSize: `${step}px ${step}px`, backgroundPosition: `${x}px ${y}px` }} />;
}

function World({ children }: { children: ReactNode }) {
  const { x, y, zoom } = useAppSelector(selectViewport, shallowEqual);
  return (
    // --inverse-zoom keeps handles and outlines the same size on screen at any zoom.
    <div className="world" style={{ transform: `translate(${x}px, ${y}px) scale(${zoom})`, ['--inverse-zoom' as string]: 1 / zoom }}>
      {children}
    </div>
  );
}

function Marquee() {
  const marquee = useAppSelector((state) => state.editor.marquee, shallowEqual);
  if (!marquee) return null;
  return <div className="marquee" style={{ transform: `translate(${marquee.x}px, ${marquee.y}px)`, width: marquee.width, height: marquee.height }} />;
}

function Layer({ ids }: { ids: readonly string[] }) {
  return (
    <>
      {ids.map((id) => (
        <ElementNode key={id} id={id} />
      ))}
    </>
  );
}

export function Canvas({ worldOverlay, label }: { worldOverlay?: ReactNode; label: string }) {
  const board = useBoardContext();
  const { canvasRef, pointerRef, index, canEdit } = board;
  const dispatch = useAppDispatch();
  const store = useAppStore();
  const layers = useAppSelector((state) => selectLayeredIds(state, index));
  const draft = useAppSelector(selectDraft);
  const placing = useAppSelector((state) => state.editor.placing);
  const phase = useAppSelector(selectBoardPhase);
  const empty = useAppSelector((state) => state.board.elements.ids.length === 0);
  const gesture = useRef<Gesture | null>(null);
  const spaceHeld = useRef(false);
  const lastCursorSent = useRef(0);
  const [mode, setMode] = useState<'idle' | 'pan' | 'panning'>('idle');
  /** Measured once per gesture and on resize: reading layout on every pointer move would force a reflow each time. */
  const rectRef = useRef<DOMRect | null>(null);
  const settleTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const measure = () => {
    rectRef.current = canvasRef.current?.getBoundingClientRect() ?? null;
    return rectRef.current;
  };

  const screenPoint = (clientX: number, clientY: number) => {
    const rect = rectRef.current ?? measure()!;
    return { sx: clientX - rect.left, sy: clientY - rect.top };
  };

  /**
   * While the view moves, the world gets its own compositor layer, so a pan or zoom only re-composites
   * instead of repainting every element. It is dropped when the view settles, so text is redrawn sharp.
   */
  const viewMoving = (moving: boolean) => {
    const world = canvasRef.current?.querySelector('.world');
    if (settleTimer.current) clearTimeout(settleTimer.current);
    settleTimer.current = null;
    if (moving) {
      world?.classList.add('world-moving');
      settleTimer.current = setTimeout(() => viewMoving(false), 200);
    } else {
      world?.classList.remove('world-moving');
    }
  };

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const observer = new ResizeObserver(() => measure());
    observer.observe(canvas);
    window.addEventListener('scroll', measure, true);
    return () => {
      observer.disconnect();
      window.removeEventListener('scroll', measure, true);
    };
    // measure only reads refs.
  }, [canvasRef]);

  const worldPoint = (clientX: number, clientY: number) => {
    const { sx, sy } = screenPoint(clientX, clientY);
    return toWorld(store.getState().editor.viewport, sx, sy);
  };

  const cancelGesture = () => {
    const current = gesture.current;
    gesture.current = null;
    if (!current) return false;
    if (current.kind === 'drag') dispatch(dragEnded());
    if (current.kind === 'resize') dispatch(resizeChanged(null));
    if (current.kind === 'marquee') dispatch(marqueeChanged(null));
    if (current.kind === 'connect') dispatch(connectingChanged(null));
    if (current.kind === 'pan') setMode(spaceHeld.current ? 'pan' : 'idle');
    return true;
  };

  // Wheel zooms around the pointer (pinch too); a trackpad's sideways scroll, or Shift+wheel, pans.
  // React's wheel listener is passive, so this one is attached by hand.
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const onWheel = (event: WheelEvent) => {
      if ((event.target as HTMLElement).closest('[data-ui]')) return;
      event.preventDefault();
      const unit = event.deltaMode === 1 ? 16 : event.deltaMode === 2 ? 400 : 1;
      const dx = event.deltaX * unit;
      const dy = event.deltaY * unit;
      const rect = rectRef.current ?? canvas.getBoundingClientRect();
      viewMoving(true);
      const sx = event.clientX - rect.left;
      const sy = event.clientY - rect.top;
      if (event.ctrlKey || event.metaKey) {
        dispatch(zoomedAt({ factor: Math.exp(-dy * 0.01), sx, sy }));
      } else if (event.shiftKey) {
        dispatch(panned({ dx: -(dx || dy), dy: 0 }));
      } else if (Math.abs(dx) > 0) {
        dispatch(panned({ dx: -dx, dy: -dy }));
      } else {
        dispatch(zoomedAt({ factor: Math.exp(-dy * 0.0015), sx, sy }));
      }
    };
    canvas.addEventListener('wheel', onWheel, { passive: false });
    return () => canvas.removeEventListener('wheel', onWheel);
  }, [canvasRef, dispatch]);

  // Space held turns any drag into a pan; Escape abandons whatever gesture is under way.
  useEffect(() => {
    const typing = (event: globalThis.KeyboardEvent) => (event.target as HTMLElement).closest?.('input, textarea, select, [contenteditable="true"]') !== null;
    const onKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape' && cancelGesture()) {
        event.stopImmediatePropagation();
        return;
      }
      if (event.code === 'Space' && !typing(event) && !event.repeat) {
        spaceHeld.current = true;
        setMode((current) => (current === 'idle' ? 'pan' : current));
        event.preventDefault();
      }
    };
    const onKeyUp = (event: globalThis.KeyboardEvent) => {
      if (event.code === 'Space') {
        spaceHeld.current = false;
        setMode((current) => (current === 'pan' ? 'idle' : current));
      }
    };
    window.addEventListener('keydown', onKeyDown, true);
    window.addEventListener('keyup', onKeyUp);
    return () => {
      window.removeEventListener('keydown', onKeyDown, true);
      window.removeEventListener('keyup', onKeyUp);
    };
    // cancelGesture only touches refs and dispatch, so the listeners are attached once.
  }, []);

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    if (event.button !== 0 && event.button !== 1) return;
    const target = event.target as HTMLElement;
    if (target.closest('[data-ui]')) return;

    const state = store.getState();
    measure();
    const world = worldPoint(event.clientX, event.clientY);
    if (state.editor.quickAdd) dispatch(quickAddClosed());
    const capture = () => canvasRef.current?.setPointerCapture(event.pointerId);

    if (event.button === 1 || spaceHeld.current) {
      event.preventDefault();
      gesture.current = { kind: 'pan', startX: event.clientX, startY: event.clientY, lastX: event.clientX, lastY: event.clientY, moved: true };
      setMode('panning');
      capture();
      return;
    }

    const handle = target.closest<HTMLElement>('[data-handle]');
    const node = target.closest<HTMLElement>('[data-element-id]');
    const connection = target.closest<SVGElement>('[data-connection-id]');

    if (handle && node && canEdit) {
      event.preventDefault();
      const id = node.dataset.elementId!;
      const element = state.board.elements.entities[id];
      if (!element) return;
      if (handle.dataset.handle === 'resize') {
        const renderer = typeOf(index, element.type).renderer;
        gesture.current = { kind: 'resize', id, world, width: element.width, height: element.height, min: renderer === 'sticky' ? 60 : 120 };
        dispatch(resizeChanged({ id, width: element.width, height: element.height }));
      } else {
        gesture.current = { kind: 'connect', from: id };
        dispatch(connectingChanged({ from: id, x: world.x, y: world.y }));
      }
      capture();
      return;
    }

    if (node) {
      event.preventDefault();
      const id = node.dataset.elementId!;
      const additive = event.shiftKey || event.metaKey || event.ctrlKey;
      const wasSelected = state.editor.selected[id] === true;
      if (additive) dispatch(selectionToggled(id));
      else if (!wasSelected) dispatch(selectionSet([id]));
      else dispatch(focusMoved(id));
      node.focus({ preventScroll: true });
      gesture.current = { kind: 'press', id, startX: event.clientX, startY: event.clientY, world, additive, wasSelected };
      capture();
      return;
    }

    if (connection) {
      dispatch(connectionSelected(connection.dataset.connectionId!));
      return;
    }

    if (placing && canEdit) {
      event.preventDefault();
      const type = index?.byId.get(placing);
      if (type) dispatch(draftStarted(newElementOf(type, world)));
      return;
    }

    if (event.shiftKey) {
      gesture.current = { kind: 'marquee', world, base: Object.keys(state.editor.selected) };
      capture();
      return;
    }

    gesture.current = { kind: 'pan', startX: event.clientX, startY: event.clientY, lastX: event.clientX, lastY: event.clientY, moved: false };
    setMode('panning');
    capture();
  };

  const onPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const world = worldPoint(event.clientX, event.clientY);
    pointerRef.current = world;
    const now = performance.now();
    if (now - lastCursorSent.current >= cursorInterval) {
      lastCursorSent.current = now;
      dispatch(pointerMoved({ x: Math.round(world.x), y: Math.round(world.y) }));
    }

    const current = gesture.current;
    if (!current) return;
    const state = store.getState();

    switch (current.kind) {
      case 'pan': {
        viewMoving(true);
        dispatch(panned({ dx: event.clientX - current.lastX, dy: event.clientY - current.lastY }));
        current.lastX = event.clientX;
        current.lastY = event.clientY;
        if (Math.hypot(event.clientX - current.startX, event.clientY - current.startY) > dragThreshold) current.moved = true;
        break;
      }
      case 'press': {
        if (Math.hypot(event.clientX - current.startX, event.clientY - current.startY) <= dragThreshold) break;
        if (!canEdit) {
          // Viewers cannot move anything, so a drag over a sticky just pans.
          gesture.current = { kind: 'pan', startX: current.startX, startY: current.startY, lastX: event.clientX, lastY: event.clientY, moved: true };
          setMode('panning');
          break;
        }
        if (!state.editor.selected[current.id]) {
          gesture.current = null;
          break;
        }
        const ids = Object.keys(state.editor.selected);
        dispatch(dragStarted(ids));
        gesture.current = { kind: 'drag', world: current.world, ids };
        dispatch(dragMoved({ dx: Math.round(world.x - current.world.x), dy: Math.round(world.y - current.world.y) }));
        break;
      }
      case 'drag':
        dispatch(dragMoved({ dx: Math.round(world.x - current.world.x), dy: Math.round(world.y - current.world.y) }));
        break;
      case 'marquee':
        dispatch(marqueeChanged(normalized(current.world, world)));
        break;
      case 'resize':
        dispatch(
          resizeChanged({
            id: current.id,
            width: Math.round(Math.max(current.min, current.width + world.x - current.world.x)),
            height: Math.round(Math.max(current.min * 0.6, current.height + world.y - current.world.y)),
          }),
        );
        break;
      case 'connect':
        dispatch(connectingChanged({ from: current.from, x: world.x, y: world.y }));
        break;
    }
  };

  const onPointerUp = (event: PointerEvent<HTMLDivElement>) => {
    const current = gesture.current;
    gesture.current = null;
    if (canvasRef.current?.hasPointerCapture(event.pointerId)) canvasRef.current.releasePointerCapture(event.pointerId);
    if (!current) return;
    const state = store.getState();

    switch (current.kind) {
      case 'pan':
        setMode(spaceHeld.current ? 'pan' : 'idle');
        if (!current.moved) dispatch(selectionCleared());
        break;
      case 'press':
        // A plain click on one of several selected elements narrows the selection to it.
        if (!current.additive && current.wasSelected && Object.keys(state.editor.selected).length > 1) dispatch(selectionSet([current.id]));
        break;
      case 'drag': {
        const drag = state.editor.drag;
        if (drag && (drag.dx !== 0 || drag.dy !== 0)) {
          const moves = current.ids
            .map((id) => state.board.elements.entities[id])
            .filter((element) => element !== undefined)
            .map((element) => ({ id: element.id, x: element.x + drag.dx, y: element.y + drag.dy }));
          dispatch(moveElements(moves));
        }
        dispatch(dragEnded());
        break;
      }
      case 'marquee': {
        const area = state.editor.marquee;
        dispatch(marqueeChanged(null));
        if (!area) break;
        // Stickies are caught by touching the box; lanes and boundaries only when wholly inside it.
        const hits = Object.values(state.board.elements.entities)
          .filter((element) => element !== undefined)
          .filter((element) => (layerOf(typeOf(index, element.type)) === 2 ? intersects(area, element) : contains(area, element)))
          .map((element) => element.id);
        dispatch(selectionSet([...new Set([...current.base, ...hits])]));
        break;
      }
      case 'resize': {
        const resize = state.editor.resize;
        if (resize) dispatch(resizeElement(resize.id, resize));
        dispatch(resizeChanged(null));
        break;
      }
      case 'connect': {
        const target = document.elementFromPoint(event.clientX, event.clientY)?.closest<HTMLElement>('[data-element-id]');
        const to = target?.dataset.elementId;
        if (to && to !== current.from) dispatch(connectElements(current.from, to));
        dispatch(connectingChanged(null));
        break;
      }
    }
  };

  const onDoubleClick = (event: React.MouseEvent<HTMLDivElement>) => {
    if (!canEdit) return;
    // The canvas captured the pointer on pointerdown, so the event's own target is the canvas: hit-test instead.
    const target = (document.elementFromPoint(event.clientX, event.clientY) as HTMLElement | null) ?? (event.target as HTMLElement);
    if (target.closest('[data-ui]') || target.closest('[data-handle]')) return;
    const node = target.closest<HTMLElement>('[data-element-id]');
    if (node) {
      dispatch(editStarted(node.dataset.elementId!));
    } else if (!target.closest('[data-connection-id]')) {
      dispatch(quickAddOpened(worldPoint(event.clientX, event.clientY)));
    }
  };

  const onKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    // Tab walks the timeline (left to right, then top to bottom), not the DOM order of the layers.
    if (event.key !== 'Tab' || event.altKey || event.ctrlKey || event.metaKey) return;
    const id = (event.target as HTMLElement).dataset?.elementId;
    if (!id) return;
    const order = selectTimelineOrder(store.getState());
    const next = order[order.indexOf(id) + (event.shiftKey ? -1 : 1)];
    if (next) {
      event.preventDefault();
      dispatch(selectionSet([next]));
      board.focusElement(next);
    }
  };

  const onDragOver = (event: DragEvent<HTMLDivElement>) => {
    if (canEdit && event.dataTransfer.types.includes(elementTypeMime)) {
      event.preventDefault();
      event.dataTransfer.dropEffect = 'copy';
    }
  };

  const onDrop = (event: DragEvent<HTMLDivElement>) => {
    const type = index?.byId.get(event.dataTransfer.getData(elementTypeMime));
    if (!type || !canEdit) return;
    event.preventDefault();
    dispatch(draftStarted(newElementOf(type, worldPoint(event.clientX, event.clientY))));
  };

  return (
    <div
      ref={canvasRef}
      className="canvas"
      role="application"
      aria-roledescription="EventStorming board"
      aria-label={label}
      data-mode={mode !== 'idle' ? mode : placing ? 'placing' : undefined}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerMove}
      onPointerUp={onPointerUp}
      onPointerCancel={() => cancelGesture()}
      onPointerLeave={() => {
        pointerRef.current = null;
      }}
      onDoubleClick={onDoubleClick}
      onKeyDown={onKeyDown}
      onDragOver={onDragOver}
      onDrop={onDrop}
    >
      <Grid />
      <World>
        <Layer ids={layers.lanes} />
        <Layer ids={layers.areas} />
        <ConnectionLayer />
        <Layer ids={layers.stickies} />
        {draft && <DraftNode draft={draft} />}
        <Marquee />
        {worldOverlay}
      </World>
      {phase === 'ready' && empty && !draft && (
        <p className="canvas-hint">
          {canEdit ? (
            <>
              Press <kbd>E</kbd> to add a Domain Event where the pointer is, pick a type from the palette, or double-click anywhere. Press <kbd>?</kbd> for all shortcuts.
            </>
          ) : (
            'This board is empty.'
          )}
        </p>
      )}
      <SelectionBar />
      <QuickAdd />
      <ZoomControls />
    </div>
  );
}
