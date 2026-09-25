import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import { boardClosed } from '@/modules/board-modelling/board/boardSlice';

/**
 * - offline: no board open, or the connection is down and a retry is scheduled;
 * - connecting: starting, joining and loading the snapshot;
 * - live: joined, snapshot in, changes flowing;
 * - reconnecting: the connection dropped and is being re-established.
 */
export type ConnectionStatus = 'offline' | 'connecting' | 'live' | 'reconnecting';

export interface RealtimeState {
  status: ConnectionStatus;
}

const initialState: RealtimeState = { status: 'offline' };

export const realtimeSlice = createSlice({
  name: 'realtime',
  initialState,
  reducers: {
    connectionStatusChanged(state, action: PayloadAction<ConnectionStatus>) {
      state.status = action.payload;
    },
  },
  extraReducers: (builder) => {
    builder.addCase(boardClosed, () => initialState);
  },
});

export const { connectionStatusChanged } = realtimeSlice.actions;

export const selectConnectionStatus = (state: { realtime: RealtimeState }) => state.realtime.status;
