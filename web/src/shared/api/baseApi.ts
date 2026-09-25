import { createApi, fetchBaseQuery, type BaseQueryFn, type FetchArgs, type FetchBaseQueryError } from '@reduxjs/toolkit/query/react';
import type { ThunkDispatch, UnknownAction } from '@reduxjs/toolkit';
import { appApiUrl } from '../config';

/**
 * How the API layer gets an access token. The identity module owns the session and registers
 * these when the store is made, so shared code never reaches into a module.
 */
export interface AuthHooks {
  /** A token that is valid for a while yet, refreshing first if it is about to expire. Null when signed out. */
  freshAccessToken(getState: () => unknown, dispatch: ThunkDispatch<unknown, unknown, UnknownAction>): Promise<string | null>;
  /** Exchange the refresh cookie for a new token, whatever the current one looks like. */
  refreshAccessToken(dispatch: ThunkDispatch<unknown, unknown, UnknownAction>): Promise<string | null>;
}

let authHooks: AuthHooks = {
  freshAccessToken: async () => null,
  refreshAccessToken: async () => null,
};

export function registerAuthHooks(hooks: AuthHooks): void {
  authHooks = hooks;
}

/** Marks a request that must go out without a token (sign-in, previewing an invitation). */
export interface RequestOptions {
  anonymous?: boolean;
}

type Args = string | (FetchArgs & RequestOptions);

const rawBaseQuery = fetchBaseQuery({
  baseUrl: appApiUrl,
  credentials: 'include',
  headers: { 'X-Requested-With': 'fetch' },
});

/**
 * Sends the access token, and on a 401 refreshes the session once and retries. Concurrent
 * refreshes share one request (the identity module serialises them).
 */
const authenticatedBaseQuery: BaseQueryFn<Args, unknown, FetchBaseQueryError> = async (args, api, extraOptions) => {
  const anonymous = typeof args !== 'string' && args.anonymous === true;
  const request = (token: string | null): FetchArgs => {
    const fetchArgs: FetchArgs = typeof args === 'string' ? { url: args } : { ...args };
    delete (fetchArgs as RequestOptions).anonymous;
    if (token) {
      fetchArgs.headers = { ...(fetchArgs.headers as Record<string, string> | undefined), Authorization: `Bearer ${token}` };
    }
    return fetchArgs;
  };

  const dispatch = api.dispatch as ThunkDispatch<unknown, unknown, UnknownAction>;
  const token = anonymous ? null : await authHooks.freshAccessToken(api.getState, dispatch);
  let result = await rawBaseQuery(request(token), api, extraOptions);
  if (!anonymous && result.error?.status === 401) {
    const renewed = await authHooks.refreshAccessToken(dispatch);
    if (renewed) {
      result = await rawBaseQuery(request(renewed), api, extraOptions);
    }
  }

  return result;
};

/** The one RTK Query API. Each module injects its own endpoints into it. */
export const api = createApi({
  reducerPath: 'api',
  baseQuery: authenticatedBaseQuery,
  tagTypes: ['Me', 'Teams', 'Team', 'Invitations', 'Boards', 'ApiKeys', 'Notation'],
  endpoints: () => ({}),
});
