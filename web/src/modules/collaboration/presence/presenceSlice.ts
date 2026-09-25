import { createEntityAdapter, createSlice, type EntityState, type PayloadAction } from '@reduxjs/toolkit';
import { boardClosed, boardOpened, changeSetReceived } from '@/modules/board-modelling/board/boardSlice';

/** Someone with the board open. One person in two tabs is two participants with one account. */
export interface Participant {
  connectionId: string;
  accountId: string;
  displayName: string;
  color: string;
  editingElementId: string | null;
}

export interface DragMove {
  elementId: string;
  x: number;
  y: number;
}

export const participantsAdapter = createEntityAdapter<Participant, string>({ selectId: (participant) => participant.connectionId });

export interface PresenceState {
  you: Participant | null;
  /** Everyone else. */
  participants: EntityState<Participant, string>;
  cursors: Record<string, { x: number; y: number }>;
  /** Where others are dragging elements right now, by connection. */
  drags: Record<string, DragMove[]>;
}

const initialState: PresenceState = {
  you: null,
  participants: participantsAdapter.getInitialState(),
  cursors: {},
  drags: {},
};

export const presenceSlice = createSlice({
  name: 'presence',
  initialState,
  reducers: {
    presenceJoined(state, action: PayloadAction<{ you: Participant; participants: Participant[] }>) {
      state.you = action.payload.you;
      participantsAdapter.setAll(
        state.participants,
        action.payload.participants.filter((participant) => participant.connectionId !== action.payload.you.connectionId),
      );
      state.cursors = {};
      state.drags = {};
    },
    /** The connection dropped: nobody's presence can be trusted until we join again. */
    presenceCleared() {
      return initialState;
    },
    participantJoined(state, action: PayloadAction<Participant>) {
      if (action.payload.connectionId !== state.you?.connectionId) {
        participantsAdapter.setOne(state.participants, action.payload);
      }
    },
    participantLeft(state, action: PayloadAction<{ connectionId: string }>) {
      participantsAdapter.removeOne(state.participants, action.payload.connectionId);
      delete state.cursors[action.payload.connectionId];
      delete state.drags[action.payload.connectionId];
    },
    cursorMoved(state, action: PayloadAction<{ connectionId: string; x: number; y: number }>) {
      const { connectionId, x, y } = action.payload;
      if (state.participants.entities[connectionId]) {
        state.cursors[connectionId] = { x, y };
      }
    },
    editingFocusChanged(state, action: PayloadAction<{ connectionId: string; elementId: string | null }>) {
      const participant = state.participants.entities[action.payload.connectionId];
      if (participant) participant.editingElementId = action.payload.elementId;
    },
    dragPreviewed(state, action: PayloadAction<{ connectionId: string; moves: DragMove[] }>) {
      const { connectionId, moves } = action.payload;
      if (moves.length === 0) {
        delete state.drags[connectionId];
      } else if (state.participants.entities[connectionId]) {
        state.drags[connectionId] = moves;
      }
    },
  },
  extraReducers: (builder) => {
    builder
      .addCase(boardOpened, () => initialState)
      .addCase(boardClosed, () => initialState)
      // Once a move is committed, the preview of it has served its purpose.
      .addCase(changeSetReceived, (state, action) => {
        const changed = new Set([...action.payload.elements.map((element) => element.id), ...action.payload.removedElements.map((removed) => removed.id)]);
        for (const [connectionId, moves] of Object.entries(state.drags)) {
          const remaining = moves.filter((move) => !changed.has(move.elementId));
          if (remaining.length === 0) delete state.drags[connectionId];
          else if (remaining.length !== moves.length) state.drags[connectionId] = remaining;
        }
      });
  },
});

export const {
  presenceJoined,
  presenceCleared,
  participantJoined,
  participantLeft,
  cursorMoved,
  editingFocusChanged,
  dragPreviewed,
} = presenceSlice.actions;

type WithPresence = { presence: PresenceState };

const participantSelectors = participantsAdapter.getSelectors((state: WithPresence) => state.presence.participants);

export const selectYou = (state: WithPresence) => state.presence.you;
export const selectParticipants = participantSelectors.selectAll;
export const selectParticipantIds = participantSelectors.selectIds;
export const selectParticipant = participantSelectors.selectById;
export const selectCursor = (state: WithPresence, connectionId: string) => state.presence.cursors[connectionId];
export const selectDrags = (state: WithPresence) => state.presence.drags;
