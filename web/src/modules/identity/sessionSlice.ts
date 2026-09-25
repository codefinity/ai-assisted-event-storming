import { createSlice, type PayloadAction } from '@reduxjs/toolkit';

export interface Account {
  id: string;
  email: string;
  displayName: string;
}

/** What sign-in, sign-up and refresh return. The refresh token itself stays in its HttpOnly cookie. */
export interface SessionResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  account: Account;
}

export type SessionStatus = 'unknown' | 'signed-in' | 'signed-out';

/** The access token lives only here, in memory: a reload restores it from the refresh cookie. */
export interface SessionState {
  status: SessionStatus;
  accessToken: string | null;
  expiresAt: number | null;
  account: Account | null;
}

const initialState: SessionState = { status: 'unknown', accessToken: null, expiresAt: null, account: null };

export const sessionSlice = createSlice({
  name: 'session',
  initialState,
  reducers: {
    sessionStarted(state, action: PayloadAction<SessionResponse>) {
      state.status = 'signed-in';
      state.accessToken = action.payload.accessToken;
      state.expiresAt = Date.parse(action.payload.accessTokenExpiresAt);
      state.account = action.payload.account;
    },
    sessionEnded() {
      return { ...initialState, status: 'signed-out' as const };
    },
  },
});

export const { sessionStarted, sessionEnded } = sessionSlice.actions;

type WithSession = { session: SessionState };

export const selectSession = (state: WithSession) => state.session;
export const selectAccount = (state: WithSession) => state.session.account;
export const selectSessionStatus = (state: WithSession) => state.session.status;
