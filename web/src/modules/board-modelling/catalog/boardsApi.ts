import { api } from '@/shared/api/baseApi';
import type { BoardLevel, BoardSnapshot, BoardSummary } from '../model';
import type { Notation } from '../notation/notation';

export interface BoardList {
  items: BoardSummary[];
  nextCursor: string | null;
  /** Whether you may create and change boards in this team. */
  canEdit: boolean;
}

export interface ListBoardsArgs {
  teamId: string;
  includeArchived?: boolean;
  cursor?: string;
}

/** The Board Document v1: the JSON a board exports to and imports from. */
export interface BoardDocument {
  version: 1;
  board: { id?: string; name?: string; level?: BoardLevel };
  elements: Array<{
    key?: string;
    type: string;
    text?: string;
    position?: { x: number; y: number };
    size?: { width: number; height: number };
    pivotal?: boolean;
    color?: string | null;
    swimlane?: string | null;
    boundary?: string | null;
    anchor?: string | null;
  }>;
  connections?: Array<{ from: string; to: string; label?: string | null }>;
}

export interface ImportedBoard {
  board: BoardSummary;
  keys: Record<string, string>;
  elementCount: number;
  connectionCount: number;
}

export const boardsApi = api.injectEndpoints({
  endpoints: (build) => ({
    listBoards: build.query<BoardList, ListBoardsArgs>({
      query: ({ teamId, includeArchived, cursor }) => ({
        url: `/teams/${teamId}/boards`,
        params: { includeArchived: includeArchived ? true : undefined, cursor, limit: 48 },
      }),
      // One cache entry per team and filter; "load more" pages append to it.
      serializeQueryArgs: ({ queryArgs }) => `${queryArgs.teamId}:${queryArgs.includeArchived ? 'all' : 'active'}`,
      merge: (current, incoming, { arg }) => {
        if (!arg.cursor) {
          return incoming;
        }

        const known = new Set(current.items.map((board) => board.id));
        current.items.push(...incoming.items.filter((board) => !known.has(board.id)));
        current.nextCursor = incoming.nextCursor;
        current.canEdit = incoming.canEdit;
        return current;
      },
      forceRefetch: ({ currentArg, previousArg }) => currentArg?.cursor !== previousArg?.cursor,
      providesTags: (_result, _error, { teamId }) => [{ type: 'Boards', id: teamId }],
    }),
    createBoard: build.mutation<BoardSummary, { teamId: string; name: string; level: BoardLevel }>({
      query: ({ teamId, ...body }) => ({ url: `/teams/${teamId}/boards`, method: 'POST', body }),
      invalidatesTags: (_result, _error, { teamId }) => [{ type: 'Boards', id: teamId }],
    }),
    importBoard: build.mutation<ImportedBoard, { teamId: string; document: unknown }>({
      query: ({ teamId, document }) => ({ url: `/teams/${teamId}/boards/import`, method: 'POST', body: document }),
      invalidatesTags: (_result, _error, { teamId }) => [{ type: 'Boards', id: teamId }],
    }),
    renameBoard: build.mutation<BoardSummary, { board: BoardSummary; name: string }>({
      query: ({ board, name }) => ({ url: `/boards/${board.id}`, method: 'PATCH', body: { name } }),
      invalidatesTags: (_result, _error, { board }) => [{ type: 'Boards', id: board.teamId }],
    }),
    duplicateBoard: build.mutation<BoardSummary, { board: BoardSummary; name?: string }>({
      query: ({ board, name }) => ({ url: `/boards/${board.id}/duplicate`, method: 'POST', body: { name: name ?? null } }),
      invalidatesTags: (_result, _error, { board }) => [{ type: 'Boards', id: board.teamId }],
    }),
    archiveBoard: build.mutation<BoardSummary, BoardSummary>({
      query: (board) => ({ url: `/boards/${board.id}/archive`, method: 'POST' }),
      invalidatesTags: (_result, _error, board) => [{ type: 'Boards', id: board.teamId }],
    }),
    restoreBoard: build.mutation<BoardSummary, BoardSummary>({
      query: (board) => ({ url: `/boards/${board.id}/restore`, method: 'POST' }),
      invalidatesTags: (_result, _error, board) => [{ type: 'Boards', id: board.teamId }],
    }),
    deleteBoard: build.mutation<void, BoardSummary>({
      query: (board) => ({ url: `/boards/${board.id}`, method: 'DELETE' }),
      // A refetch merges into "load more" pages rather than replacing them, so drop the board from both lists directly.
      async onQueryStarted(board, { dispatch, queryFulfilled }) {
        if (!(await queryFulfilled.then(() => true, () => false))) return;
        for (const includeArchived of [false, true]) {
          dispatch(
            boardsApi.util.updateQueryData('listBoards', { teamId: board.teamId, includeArchived }, (list) => {
              list.items = list.items.filter((candidate) => candidate.id !== board.id);
            }),
          );
        }
      },
      invalidatesTags: (_result, _error, board) => [{ type: 'Boards', id: board.teamId }],
    }),
    // The editor loads snapshots itself (see the realtime middleware), never through a cache entry.
    getBoardSnapshot: build.query<BoardSnapshot, string>({
      query: (boardId) => `/boards/${boardId}`,
      keepUnusedDataFor: 0,
    }),
    exportBoardDocument: build.query<BoardDocument, string>({
      query: (boardId) => `/boards/${boardId}/document`,
      keepUnusedDataFor: 0,
    }),
    replaceBoardDocument: build.mutation<ImportedBoard, { boardId: string; document: unknown }>({
      query: ({ boardId, document }) => ({ url: `/boards/${boardId}/document`, method: 'PUT', body: document }),
    }),
    getNotation: build.query<Notation, void>({
      query: () => '/element-types',
      providesTags: ['Notation'],
      keepUnusedDataFor: 3600,
    }),
  }),
});

export const {
  useListBoardsQuery,
  useCreateBoardMutation,
  useImportBoardMutation,
  useRenameBoardMutation,
  useDuplicateBoardMutation,
  useArchiveBoardMutation,
  useRestoreBoardMutation,
  useDeleteBoardMutation,
  useReplaceBoardDocumentMutation,
  useGetNotationQuery,
  useLazyExportBoardDocumentQuery,
} = boardsApi;
