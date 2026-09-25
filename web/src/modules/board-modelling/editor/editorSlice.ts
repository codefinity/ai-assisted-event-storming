import { createAction, createSlice, type PayloadAction } from '@reduxjs/toolkit';
import type { Rect } from '../model';
import { boardOpened, changeSetReceived, contentReplaced, opApplied } from '../board/boardSlice';
import type { NewElementData } from '../board/ops';

// What this person is doing on the board right now: where they are looking, what they have
// selected, what they are dragging. None of it is shared except through the realtime middleware,
// which picks a few of these actions up (cursor, editing focus, drag preview) and sends them on.

/** screen = world × zoom + (x, y), in pixels relative to the canvas's top-left corner. */
export interface Viewport {
  x: number;
  y: number;
  zoom: number;
}

export const minZoom = 0.1;
export const maxZoom = 3;

export type SidePanel = 'legend' | 'search';

export interface EditorState {
  viewport: Viewport;
  selected: Record<string, true>;
  selectedConnection: string | null;
  /** An existing element whose text is being edited. */
  editing: string | null;
  /** A new element being typed. It is created when the text is committed. */
  draft: NewElementData | null;
  drag: { ids: Record<string, true>; dx: number; dy: number } | null;
  resize: { id: string; width: number; height: number } | null;
  marquee: Rect | null;
  connecting: { from: string; x: number; y: number } | null;
  /** A type chosen in the palette, placed by the next click on the canvas. */
  placing: string | null;
  quickAdd: { x: number; y: number } | null;
  panel: SidePanel | null;
  help: boolean;
  search: { query: string; types: string[] };
  /** The element that holds the canvas's single tab stop. */
  focusId: string | null;
}

const initialState: EditorState = {
  viewport: { x: 80, y: 80, zoom: 1 },
  selected: {},
  selectedConnection: null,
  editing: null,
  draft: null,
  drag: null,
  resize: null,
  marquee: null,
  connecting: null,
  placing: null,
  quickAdd: null,
  panel: null,
  help: false,
  search: { query: '', types: [] },
  focusId: null,
};

export const clampZoom = (zoom: number) => Math.min(maxZoom, Math.max(minZoom, zoom));

export const editorSlice = createSlice({
  name: 'editor',
  initialState,
  reducers: {
    viewportChanged(state, action: PayloadAction<Viewport>) {
      state.viewport = { ...action.payload, zoom: clampZoom(action.payload.zoom) };
    },
    panned(state, action: PayloadAction<{ dx: number; dy: number }>) {
      state.viewport.x += action.payload.dx;
      state.viewport.y += action.payload.dy;
    },
    /** Zoom by a factor, keeping the world point under (sx, sy) where it is on screen. */
    zoomedAt(state, action: PayloadAction<{ factor: number; sx: number; sy: number }>) {
      const { factor, sx, sy } = action.payload;
      const { x, y, zoom } = state.viewport;
      const next = clampZoom(zoom * factor);
      const worldX = (sx - x) / zoom;
      const worldY = (sy - y) / zoom;
      state.viewport = { zoom: next, x: sx - worldX * next, y: sy - worldY * next };
    },
    selectionSet(state, action: PayloadAction<string[]>) {
      state.selected = Object.fromEntries(action.payload.map((id) => [id, true as const]));
      state.selectedConnection = null;
      if (action.payload.length > 0) state.focusId = action.payload[action.payload.length - 1]!;
    },
    selectionToggled(state, action: PayloadAction<string>) {
      if (state.selected[action.payload]) {
        delete state.selected[action.payload];
      } else {
        state.selected[action.payload] = true;
        state.focusId = action.payload;
      }
      state.selectedConnection = null;
    },
    selectionCleared(state) {
      state.selected = {};
      state.selectedConnection = null;
    },
    connectionSelected(state, action: PayloadAction<string | null>) {
      state.selectedConnection = action.payload;
      state.selected = {};
    },
    focusMoved(state, action: PayloadAction<string>) {
      state.focusId = action.payload;
    },
    editStarted(state, action: PayloadAction<string>) {
      state.editing = action.payload;
      state.draft = null;
      state.selected = { [action.payload]: true };
      state.focusId = action.payload;
    },
    editEnded(state) {
      state.editing = null;
    },
    draftStarted(state, action: PayloadAction<NewElementData>) {
      state.draft = action.payload;
      state.editing = null;
      state.placing = null;
      state.quickAdd = null;
      state.selected = {};
    },
    draftEnded(state) {
      state.draft = null;
    },
    dragStarted(state, action: PayloadAction<string[]>) {
      state.drag = { ids: Object.fromEntries(action.payload.map((id) => [id, true as const])), dx: 0, dy: 0 };
    },
    dragMoved(state, action: PayloadAction<{ dx: number; dy: number }>) {
      if (state.drag) {
        state.drag.dx = action.payload.dx;
        state.drag.dy = action.payload.dy;
      }
    },
    /** The drag is over: either a move op follows, or it was cancelled. */
    dragEnded(state) {
      state.drag = null;
    },
    resizeChanged(state, action: PayloadAction<EditorState['resize']>) {
      state.resize = action.payload;
    },
    marqueeChanged(state, action: PayloadAction<Rect | null>) {
      state.marquee = action.payload;
    },
    connectingChanged(state, action: PayloadAction<EditorState['connecting']>) {
      state.connecting = action.payload;
    },
    placingSet(state, action: PayloadAction<string | null>) {
      state.placing = action.payload;
      state.quickAdd = null;
    },
    quickAddOpened(state, action: PayloadAction<{ x: number; y: number }>) {
      state.quickAdd = action.payload;
      state.placing = null;
    },
    quickAddClosed(state) {
      state.quickAdd = null;
    },
    panelToggled(state, action: PayloadAction<SidePanel>) {
      state.panel = state.panel === action.payload ? null : action.payload;
    },
    panelClosed(state) {
      state.panel = null;
    },
    helpToggled(state, action: PayloadAction<boolean | undefined>) {
      state.help = action.payload ?? !state.help;
    },
    searchChanged(state, action: PayloadAction<Partial<EditorState['search']>>) {
      state.search = { ...state.search, ...action.payload };
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(boardOpened, () => initialState)
      .addCase(contentReplaced, (state) => ({ ...initialState, viewport: state.viewport, panel: state.panel }))
      // Whatever someone else removed can no longer be selected or edited here.
      .addCase(changeSetReceived, (state, action) => {
        for (const removed of action.payload.removedElements) {
          delete state.selected[removed.id];
          if (state.editing === removed.id) state.editing = null;
          if (state.drag?.ids[removed.id]) delete state.drag.ids[removed.id];
          if (state.focusId === removed.id) state.focusId = null;
        }

        for (const removed of action.payload.removedConnections) {
          if (state.selectedConnection === removed.id) state.selectedConnection = null;
        }
      })
      .addCase(opApplied, (state, action) => {
        const { op } = action.payload;
        if (op.kind === 'delete') {
          for (const id of op.elementIds) {
            delete state.selected[id];
            if (state.editing === id) state.editing = null;
          }
          if (state.selectedConnection && op.connectionIds.includes(state.selectedConnection)) state.selectedConnection = null;
        } else if (op.kind === 'disconnect' && state.selectedConnection && op.connectionIds.includes(state.selectedConnection)) {
          state.selectedConnection = null;
        }
      });
  },
});

export const {
  viewportChanged,
  panned,
  zoomedAt,
  selectionSet,
  selectionToggled,
  selectionCleared,
  connectionSelected,
  focusMoved,
  editStarted,
  editEnded,
  draftStarted,
  draftEnded,
  dragStarted,
  dragMoved,
  dragEnded,
  resizeChanged,
  marqueeChanged,
  connectingChanged,
  placingSet,
  quickAddOpened,
  quickAddClosed,
  panelToggled,
  panelClosed,
  helpToggled,
  searchChanged,
} = editorSlice.actions;

/**
 * Where the pointer is on the board (world coordinates). No reducer keeps it; the realtime
 * middleware shares it as this person's cursor. Callers throttle it.
 */
export const pointerMoved = createAction<{ x: number; y: number }>('editor/pointerMoved');

type WithEditor = { editor: EditorState };

export const selectEditor = (state: WithEditor) => state.editor;
export const selectViewport = (state: WithEditor) => state.editor.viewport;
export const selectZoom = (state: WithEditor) => state.editor.viewport.zoom;
export const selectSelected = (state: WithEditor) => state.editor.selected;
export const selectIsSelected = (state: WithEditor, id: string) => state.editor.selected[id] === true;
export const selectEditingId = (state: WithEditor) => state.editor.editing;
export const selectDraft = (state: WithEditor) => state.editor.draft;
export const selectDragDx = (state: WithEditor, id: string) => (state.editor.drag?.ids[id] ? state.editor.drag.dx : 0);
export const selectDragDy = (state: WithEditor, id: string) => (state.editor.drag?.ids[id] ? state.editor.drag.dy : 0);
export const selectResizeOf = (state: WithEditor, id: string) => (state.editor.resize?.id === id ? state.editor.resize : null);

/** Where a screen point (relative to the canvas) is on the board. */
export function toWorld(viewport: Viewport, sx: number, sy: number): { x: number; y: number } {
  return { x: (sx - viewport.x) / viewport.zoom, y: (sy - viewport.y) / viewport.zoom };
}
