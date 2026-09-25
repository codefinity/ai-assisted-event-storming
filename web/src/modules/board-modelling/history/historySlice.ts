import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import { boardOpened, contentReplaced } from '../board/boardSlice';
import type { ContentOp } from '../board/ops';

/**
 * One undoable step of this person's own work. Each side is a complete operation, worked out when
 * the step was taken, so undo and redo are ordinary operations: they sync like any other change,
 * and never touch what others did in between (last writer wins).
 */
export interface HistoryEntry {
  label: string;
  undo: ContentOp;
  redo: ContentOp;
}

export interface HistoryState {
  past: HistoryEntry[];
  future: HistoryEntry[];
}

export const historyLimit = 100;

const initialState: HistoryState = { past: [], future: [] };

export const historySlice = createSlice({
  name: 'history',
  initialState,
  reducers: {
    stepRecorded(state, action: PayloadAction<HistoryEntry>) {
      state.past.push(action.payload);
      if (state.past.length > historyLimit) {
        state.past.splice(0, state.past.length - historyLimit);
      }
      state.future = [];
    },
    stepUndone(state) {
      const entry = state.past.pop();
      if (entry) state.future.push(entry);
    },
    stepRedone(state) {
      const entry = state.future.pop();
      if (entry) state.past.push(entry);
    },
  },
  extraReducers: (builder) => {
    builder.addCase(boardOpened, () => initialState).addCase(contentReplaced, () => initialState);
  },
});

export const { stepRecorded, stepUndone, stepRedone } = historySlice.actions;

type WithHistory = { history: HistoryState };

export const selectCanUndo = (state: WithHistory) => state.history.past.length > 0;
export const selectCanRedo = (state: WithHistory) => state.history.future.length > 0;
export const selectUndoLabel = (state: WithHistory) => state.history.past.at(-1)?.label ?? null;
export const selectRedoLabel = (state: WithHistory) => state.history.future.at(-1)?.label ?? null;
