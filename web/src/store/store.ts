import { combineReducers, configureStore } from '@reduxjs/toolkit';
import { boardsApi } from '@/modules/board-modelling/catalog/boardsApi';
import { boardSlice } from '@/modules/board-modelling/board/boardSlice';
import { editorSlice } from '@/modules/board-modelling/editor/editorSlice';
import { historySlice } from '@/modules/board-modelling/history/historySlice';
import { presenceSlice } from '@/modules/collaboration/presence/presenceSlice';
import { signalRHubClient, type HubClientFactory } from '@/modules/collaboration/realtime/hubClient';
import { createRealtimeMiddleware, type RealtimeDependencies } from '@/modules/collaboration/realtime/realtimeMiddleware';
import { realtimeSlice } from '@/modules/collaboration/realtime/realtimeSlice';
import { sessionSlice } from '@/modules/identity/sessionSlice';
import { freshAccessToken, identityAuthHooks } from '@/modules/identity/tokens';
import { api, registerAuthHooks } from '@/shared/api/baseApi';
import { boardHubUrl } from '@/shared/config';
import { noticesSlice } from '@/shared/ui/noticesSlice';

export const rootReducer = combineReducers({
  [api.reducerPath]: api.reducer,
  [sessionSlice.name]: sessionSlice.reducer,
  [boardSlice.name]: boardSlice.reducer,
  [editorSlice.name]: editorSlice.reducer,
  [historySlice.name]: historySlice.reducer,
  [presenceSlice.name]: presenceSlice.reducer,
  [realtimeSlice.name]: realtimeSlice.reducer,
  [noticesSlice.name]: noticesSlice.reducer,
});

export type RootState = ReturnType<typeof rootReducer>;

export interface StoreOptions {
  hubFactory?: HubClientFactory;
  /** Tests replace the REST snapshot call; the app loads it through the API. */
  loadSnapshot?: RealtimeDependencies['loadSnapshot'];
  preloadedState?: Partial<RootState>;
}

const loadSnapshotFromApi: RealtimeDependencies['loadSnapshot'] = (boardId, dispatch) =>
  dispatch(boardsApi.endpoints.getBoardSnapshot.initiate(boardId, { forceRefetch: true, subscribe: false })).unwrap();

export function makeStore({ hubFactory = signalRHubClient, loadSnapshot = loadSnapshotFromApi, preloadedState }: StoreOptions = {}) {
  registerAuthHooks(identityAuthHooks);

  return configureStore({
    reducer: rootReducer,
    preloadedState,
    middleware: (getDefault) =>
      getDefault({
        // The board holds a few thousand entities; the dev-only checks would dominate every frame.
        immutableCheck: { warnAfter: 128, ignoredPaths: ['board', 'api'] },
        serializableCheck: { warnAfter: 128, ignoredPaths: ['board', 'api'] },
      }).concat(
        api.middleware,
        createRealtimeMiddleware({
          hubFactory,
          hubUrl: boardHubUrl,
          accessToken: freshAccessToken,
          loadSnapshot,
        }),
      ),
  });
}

export type AppStore = ReturnType<typeof makeStore>;
