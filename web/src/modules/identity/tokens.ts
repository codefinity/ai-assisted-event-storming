import type { ThunkDispatch, UnknownAction } from '@reduxjs/toolkit';
import type { AuthHooks } from '@/shared/api/baseApi';
import { isProblem } from '@/shared/api/problems';
import { appApiUrl } from '@/shared/config';
import { sessionEnded, sessionStarted, type SessionResponse, type SessionState } from './sessionSlice';

type Dispatch = ThunkDispatch<unknown, unknown, UnknownAction>;

/** Refresh a little before expiry, so a request never races the token's last second. */
const expiryMargin = 30_000;

let inflight: Promise<string | null> | null = null;

async function postRefresh(): Promise<Response> {
  return fetch(`${appApiUrl}/auth/refresh`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'X-Requested-With': 'fetch' },
  });
}

/**
 * Exchanges the refresh cookie for a new access token. Calls made while one is under way share
 * it. Another tab may rotate the cookie at the same moment ("session-rotated"): the new cookie
 * is then already in the jar, so one retry settles it.
 */
export function refreshAccessToken(dispatch: Dispatch): Promise<string | null> {
  inflight ??= (async () => {
    try {
      let response = await postRefresh();
      if (!response.ok && (await codeOf(response)) === 'session-rotated') {
        await new Promise((resolve) => setTimeout(resolve, 400));
        response = await postRefresh();
      }

      if (!response.ok) {
        dispatch(sessionEnded());
        return null;
      }

      const session = (await response.json()) as SessionResponse;
      dispatch(sessionStarted(session));
      return session.accessToken;
    } catch {
      // Offline: keep whatever session we have and let the caller's request fail on its own.
      return null;
    } finally {
      inflight = null;
    }
  })();
  return inflight;
}

export async function freshAccessToken(getState: () => unknown, dispatch: Dispatch): Promise<string | null> {
  const session = (getState() as { session: SessionState }).session;
  if (session.status === 'signed-out') {
    return null;
  }

  if (session.accessToken && session.expiresAt !== null && session.expiresAt - Date.now() > expiryMargin) {
    return session.accessToken;
  }

  return refreshAccessToken(dispatch);
}

async function codeOf(response: Response): Promise<string | undefined> {
  try {
    const body: unknown = await response.clone().json();
    return isProblem(body) ? body.code : undefined;
  } catch {
    return undefined;
  }
}

export const identityAuthHooks: AuthHooks = { freshAccessToken, refreshAccessToken };
