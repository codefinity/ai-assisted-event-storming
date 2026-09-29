import type { Middleware, ThunkDispatch, UnknownAction } from '@reduxjs/toolkit';
import {
  boardClosed,
  boardDetailsChanged,
  boardOpened,
  changeSetReceived,
  contentReplaced,
  opAcknowledged,
  opApplied,
  resyncStarted,
  snapshotFailed,
  snapshotLoaded,
  type BoardState,
} from '@/modules/board-modelling/board/boardSlice';
import type { PendingOp } from '@/modules/board-modelling/board/ops';
import { dragEnded, dragMoved, editEnded, editStarted, pointerMoved, type EditorState } from '@/modules/board-modelling/editor/editorSlice';
import type { BoardChangeSet, BoardDeleted, BoardDetails, BoardReplaced, BoardSnapshot } from '@/modules/board-modelling/model';
import { toProblem, type HubFailure } from '@/shared/api/problems';
import { noticeShown } from '@/shared/ui/noticesSlice';
import {
  cursorMoved,
  dragPreviewed,
  editingFocusChanged,
  participantJoined,
  participantLeft,
  presenceCleared,
  presenceJoined,
  type DragMove,
  type Participant,
} from '../presence/presenceSlice';
import type { HubClient, HubClientFactory } from './hubClient';
import { toHubCall, type JoinBoardResponse, type OperationResult } from './hubCalls';
import { connectionStatusChanged } from './realtimeSlice';

type Dispatch = ThunkDispatch<unknown, unknown, UnknownAction>;

interface State {
  board: BoardState;
  editor: EditorState;
}

export interface RealtimeDependencies {
  hubFactory: HubClientFactory;
  hubUrl: string;
  accessToken: (getState: () => unknown, dispatch: Dispatch) => Promise<string | null>;
  loadSnapshot: (boardId: string, dispatch: Dispatch) => Promise<BoardSnapshot>;
  /** How long to wait before starting again after the connection closed for good. */
  restartDelay?: number;
  /** Drag previews go out at most this often. */
  dragInterval?: number;
}

/**
 * Keeps the open board in step with the server over the board hub:
 *  1. connect, join the board (from then on its broadcasts arrive), then load a snapshot; the board
 *     slice holds broadcasts that arrive meanwhile and applies the newer ones on top;
 *  2. send each operation the person applies, and report the hub's answer (ack or failure);
 *  3. after a reconnect, join again, reload, and resend whatever had not been answered;
 *  4. share this person's cursor, editing focus and drag previews, and take in everyone else's.
 */
export function createRealtimeMiddleware(deps: RealtimeDependencies): Middleware<object, State> {
  const restartDelay = deps.restartDelay ?? 3000;
  const dragInterval = deps.dragInterval ?? 50;

  return (store) => {
    const dispatch = store.dispatch as Dispatch;
    let hub: HubClient | null = null;
    let boardId: string | null = null;
    let joined = false;
    /** Bumped whenever a join or load starts or the board closes, so late answers to old ones are ignored. */
    let generation = 0;
    const inflight = new Set<string>();
    let restartTimer: ReturnType<typeof setTimeout> | null = null;
    /** A closed board's connection lingers for a moment, so reopening it at once (React's dev double mount, a quick back-and-forth) reuses it. */
    let lingering: { connection: HubClient; timer: ReturnType<typeof setTimeout> } | null = null;
    let dragTimer: ReturnType<typeof setTimeout> | null = null;
    let lastDragSentAt = 0;
    let lastApiNoticeAt = 0;

    const live = () => hub !== null && hub.connected && joined && boardId !== null;

    function client(): HubClient {
      if (hub) return hub;
      const created = deps.hubFactory({
        url: deps.hubUrl,
        accessToken: async () => (await deps.accessToken(store.getState, dispatch)) ?? '',
      });
      listen(created);
      hub = created;
      return created;
    }

    function listen(connection: HubClient) {
      const mine = (message: { boardId: string }) => connection === hub && message.boardId === boardId;

      connection.on('boardChanged', (message: BoardChangeSet) => {
        if (!mine(message)) return;
        dispatch(changeSetReceived(message));
        if (message.actor.kind === 'api-key' && Date.now() - lastApiNoticeAt > 10_000) {
          lastApiNoticeAt = Date.now();
          dispatch(noticeShown({ tone: 'info', text: `“${message.actor.name}” (an API key) is changing this board.` }));
        }
      });
      connection.on('boardReplaced', (message: BoardReplaced) => {
        if (!mine(message)) return;
        dispatch(contentReplaced(message));
        dispatch(noticeShown({ tone: 'info', text: `${message.actor.name} replaced the whole board. It has been reloaded.` }));
        void load(++generation);
      });
      connection.on('boardDetailsChanged', (message: BoardDetails) => {
        if (mine(message)) dispatch(boardDetailsChanged(message));
      });
      connection.on('boardDeleted', (message: BoardDeleted) => {
        if (!mine(message)) return;
        // Nothing more can be saved or loaded: stop retrying and show why the board went away.
        generation++;
        joined = false;
        dispatch(snapshotFailed({ code: 'board-deleted', message: `${message.actor.name} deleted this board and everything on it.` }));
        dispatch(presenceCleared());
        dispatch(connectionStatusChanged('offline'));
      });
      connection.on('participantJoined', (message: { boardId: string; participant: Participant }) => {
        if (mine(message)) dispatch(participantJoined(message.participant));
      });
      connection.on('participantLeft', (message: { boardId: string; connectionId: string }) => {
        if (mine(message)) dispatch(participantLeft({ connectionId: message.connectionId }));
      });
      connection.on('cursorMoved', (message: { boardId: string; connectionId: string; x: number; y: number }) => {
        if (mine(message)) dispatch(cursorMoved(message));
      });
      connection.on('editingFocusChanged', (message: { boardId: string; connectionId: string; elementId: string | null }) => {
        if (mine(message)) dispatch(editingFocusChanged({ connectionId: message.connectionId, elementId: message.elementId }));
      });
      connection.on('dragPreviewed', (message: { boardId: string; connectionId: string; moves: DragMove[] }) => {
        if (mine(message)) dispatch(dragPreviewed({ connectionId: message.connectionId, moves: message.moves }));
      });

      connection.onConnectionEvent((event) => {
        if (connection !== hub || boardId === null) return;
        joined = false;
        if (event === 'reconnected') {
          void join();
          return;
        }

        dispatch(presenceCleared());
        dispatch(connectionStatusChanged('reconnecting'));
        if (event === 'closed') scheduleRestart();
      });
    }

    function scheduleRestart() {
      if (boardId === null || restartTimer !== null) return;
      dispatch(connectionStatusChanged('reconnecting'));
      restartTimer = setTimeout(() => {
        restartTimer = null;
        if (boardId !== null) void connectAndJoin();
      }, restartDelay);
    }

    async function connectAndJoin() {
      const connection = client();
      if (!connection.connected) dispatch(connectionStatusChanged('connecting'));
      try {
        await connection.start();
      } catch {
        if (connection === hub) scheduleRestart();
        return;
      }

      await join();
    }

    async function join() {
      const id = boardId;
      const connection = hub;
      if (id === null || connection === null) return;

      const mine = ++generation;
      joined = false;
      dispatch(resyncStarted());
      let response: JoinBoardResponse;
      try {
        response = await connection.invoke<JoinBoardResponse>('JoinBoard', id);
      } catch {
        if (mine === generation) scheduleRestart();
        return;
      }

      if (mine !== generation) return;
      if (!response.ok || !response.you) {
        const failure = response.failures[0];
        dispatch(snapshotFailed({ code: failure?.code ?? 'not-found', message: failure?.message ?? 'The board could not be opened.' }));
        dispatch(connectionStatusChanged('offline'));
        return;
      }

      joined = true;
      dispatch(presenceJoined({ you: response.you, participants: response.participants }));
      const editing = store.getState().editor.editing;
      if (editing) void connection.send('SetEditingFocus', id, editing).catch(() => undefined);
      await load(mine);
    }

    async function load(mine: number) {
      const id = boardId;
      if (id === null) return;
      dispatch(resyncStarted());
      try {
        const snapshot = await deps.loadSnapshot(id, dispatch);
        if (mine !== generation) return;
        dispatch(snapshotLoaded(snapshot));
        dispatch(connectionStatusChanged('live'));
        flush();
      } catch (error) {
        if (mine !== generation) return;
        const problem = toProblem(error);
        if (problem && (problem.status === 403 || problem.status === 404)) {
          dispatch(snapshotFailed({ code: problem.code ?? 'not-found', message: problem.detail ?? problem.title }));
          dispatch(connectionStatusChanged('offline'));
        } else {
          scheduleRestart();
        }
      }
    }

    /** Sends every pending operation the hub has not yet been given on this connection. */
    function flush() {
      for (const pending of store.getState().board.pending) {
        if (!inflight.has(pending.opId)) send(pending);
      }
    }

    function send(pending: PendingOp) {
      const connection = hub;
      if (!live() || connection === null || boardId === null) return;

      inflight.add(pending.opId);
      const { method, request } = toHubCall(boardId, pending);
      connection.invoke<OperationResult>(method, request).then(
        (result) => {
          inflight.delete(pending.opId);
          if (connection !== hub) return;
          dispatch(opAcknowledged({ opId: pending.opId, ok: result.ok }));
          if (!result.ok) refused(result.failures);
        },
        () => {
          inflight.delete(pending.opId);
          // A dropped connection is not a refusal: the operation stays pending and is resent after the resync.
          if (connection !== hub || !connection.connected) return;
          dispatch(opAcknowledged({ opId: pending.opId, ok: false }));
          dispatch(noticeShown({ tone: 'error', text: 'That change could not be saved.', fix: 'Try it again in a moment.' }));
        },
      );
    }

    function refused(failures: HubFailure[]) {
      const failure = failures[0];
      dispatch(
        noticeShown({
          tone: 'error',
          text: failure?.message ?? 'That change was refused.',
          fix: failure?.fix ?? undefined,
        }),
      );
    }

    function sendDragPreview() {
      dragTimer = null;
      const state = store.getState();
      const drag = state.editor.drag;
      if (!drag || !live()) return;
      lastDragSentAt = Date.now();
      const moves = Object.keys(drag.ids)
        .map((id) => state.board.elements.entities[id])
        .filter((element) => element !== undefined)
        .map((element) => ({ elementId: element.id, x: element.x + drag.dx, y: element.y + drag.dy }));
      void hub!.send('ShareDragPreview', boardId, moves).catch(() => undefined);
    }

    function open(id: string) {
      if (boardId === id) return;
      if (boardId !== null) close();
      if (lingering) {
        clearTimeout(lingering.timer);
        hub = lingering.connection;
        lingering = null;
      }
      boardId = id;
      void connectAndJoin();
    }

    function close() {
      boardId = null;
      joined = false;
      generation++;
      inflight.clear();
      if (restartTimer !== null) clearTimeout(restartTimer);
      if (dragTimer !== null) clearTimeout(dragTimer);
      restartTimer = null;
      dragTimer = null;
      const connection = hub;
      hub = null;
      if (!connection) return;
      if (connection.connected) void connection.send('LeaveBoard').catch(() => undefined);
      // Stopping the connection is also how the server learns we left for good.
      const timer = setTimeout(() => {
        lingering = null;
        void connection.stop().catch(() => undefined);
      }, 1000);
      lingering = { connection, timer };
    }

    return (next) => (action) => {
      const result = next(action);

      if (boardOpened.match(action)) {
        open(action.payload);
      } else if (boardClosed.match(action)) {
        close();
      } else if (opApplied.match(action)) {
        send(action.payload);
      } else if (pointerMoved.match(action)) {
        if (live()) void hub!.send('MoveCursor', boardId, action.payload.x, action.payload.y).catch(() => undefined);
      } else if (editStarted.match(action)) {
        if (live()) void hub!.send('SetEditingFocus', boardId, action.payload).catch(() => undefined);
      } else if (editEnded.match(action)) {
        if (live()) void hub!.send('SetEditingFocus', boardId, null).catch(() => undefined);
      } else if (dragMoved.match(action)) {
        const wait = dragInterval - (Date.now() - lastDragSentAt);
        if (wait <= 0) sendDragPreview();
        else dragTimer ??= setTimeout(sendDragPreview, wait);
      } else if (dragEnded.match(action)) {
        if (dragTimer !== null) clearTimeout(dragTimer);
        dragTimer = null;
        if (live()) void hub!.send('ShareDragPreview', boardId, []).catch(() => undefined);
      }

      return result;
    };
  };
}
