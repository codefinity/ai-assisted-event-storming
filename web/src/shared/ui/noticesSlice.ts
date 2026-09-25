import { createSlice, nanoid, type PayloadAction } from '@reduxjs/toolkit';

export interface Notice {
  id: string;
  tone: 'info' | 'error';
  text: string;
  /** What to do about it, when there is something. */
  fix?: string;
}

export interface NoticesState {
  items: Notice[];
}

const initialState: NoticesState = { items: [] };

/** At most this many are on screen; older ones make way. */
const visibleLimit = 4;

export const noticesSlice = createSlice({
  name: 'notices',
  initialState,
  reducers: {
    noticeShown: {
      reducer(state, action: PayloadAction<Notice>) {
        // The same message twice in a row is shown once.
        if (state.items.some((item) => item.text === action.payload.text && item.fix === action.payload.fix)) return;
        state.items.push(action.payload);
        if (state.items.length > visibleLimit) state.items.splice(0, state.items.length - visibleLimit);
      },
      prepare(notice: Omit<Notice, 'id'>) {
        return { payload: { ...notice, id: nanoid() } };
      },
    },
    noticeDismissed(state, action: PayloadAction<string>) {
      state.items = state.items.filter((item) => item.id !== action.payload);
    },
  },
});

export const { noticeShown, noticeDismissed } = noticesSlice.actions;

export const selectNotices = (state: { notices: NoticesState }) => state.notices.items;
