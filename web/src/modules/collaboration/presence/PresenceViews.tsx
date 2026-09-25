'use client';

import { memo, useMemo } from 'react';
import { shallowEqual } from 'react-redux';
import { Initials } from '@/modules/identity/components/AppHeader';
import { useAppSelector } from '@/store/hooks';
import { selectConnectionStatus, type ConnectionStatus } from '../realtime/realtimeSlice';
import { selectCursor, selectDrags, selectParticipant, selectParticipantIds, selectParticipants, selectYou, type Participant } from './presenceSlice';

const statusLabels: Record<ConnectionStatus, string> = {
  offline: 'Offline',
  connecting: 'Connecting…',
  live: 'Live',
  reconnecting: 'Reconnecting…',
};

export function ConnectionIndicator() {
  const status = useAppSelector(selectConnectionStatus);
  const pending = useAppSelector((state) => state.board.pending.length);
  const label = status === 'live' && pending > 0 ? 'Saving…' : statusLabels[status];
  return (
    <span className={`connection-status connection-${status}`} role="status" aria-live="polite" title={status === 'live' ? 'Changes are saved and shared as you make them' : undefined}>
      <span className="connection-dot" aria-hidden="true" />
      <span className="hide-narrow">{label}</span>
    </span>
  );
}

/** Everyone on the board, once per person however many tabs they have open. */
export function PresenceBar() {
  const you = useAppSelector(selectYou);
  const others = useAppSelector(selectParticipants);
  const people = useMemo(() => {
    const seen = new Map<string, Participant>();
    for (const participant of [...(you ? [you] : []), ...others]) {
      if (!seen.has(participant.accountId)) seen.set(participant.accountId, participant);
    }
    return [...seen.values()];
  }, [you, others]);

  if (people.length === 0) return null;
  const shown = people.slice(0, 5);
  const names = people.map((person) => (person === you ? `${person.displayName} (you)` : person.displayName)).join(', ');

  return (
    <div className="presence" role="group" aria-label={`On this board: ${names}`} title={names}>
      {shown.map((person) => (
        <Initials key={person.accountId} name={person.displayName} color={person.color} />
      ))}
      {people.length > shown.length && <span className="avatar">+{people.length - shown.length}</span>}
    </div>
  );
}

const RemoteCursor = memo(function RemoteCursor({ connectionId }: { connectionId: string }) {
  const participant = useAppSelector((state) => selectParticipant(state, connectionId));
  const cursor = useAppSelector((state) => selectCursor(state, connectionId), shallowEqual);
  const zoom = useAppSelector((state) => state.editor.viewport.zoom);
  if (!participant || !cursor) return null;

  // Drawn in world coordinates but kept the same size on screen at any zoom.
  return (
    <div className="remote-cursor" style={{ transform: `translate(${cursor.x}px, ${cursor.y}px) scale(${1 / zoom})` }} aria-hidden="true">
      <svg width="18" height="18" viewBox="0 0 18 18" style={{ position: 'absolute', left: 0, top: 0 }}>
        <path d="M1 1 L1 15 L5 11 L8 17 L10.5 16 L7.5 10 L13 10 Z" fill={participant.color} stroke="#fff" strokeWidth="1.2" strokeLinejoin="round" />
      </svg>
      <span className="remote-cursor-name" style={{ background: participant.color }}>
        {participant.displayName}
      </span>
    </div>
  );
});

export function RemoteCursors() {
  const ids = useAppSelector(selectParticipantIds);
  return (
    <>
      {ids.map((id) => (
        <RemoteCursor key={id} connectionId={id} />
      ))}
    </>
  );
}

/** Outlines where others are dragging elements, before they let go. */
export function RemoteDragGhosts() {
  const drags = useAppSelector(selectDrags);
  const entities = useAppSelector((state) => state.board.elements.entities);
  const participants = useAppSelector((state) => state.presence.participants.entities);

  return (
    <>
      {Object.entries(drags).flatMap(([connectionId, moves]) => {
        const participant = participants[connectionId];
        if (!participant) return [];
        return moves.map((move) => {
          const element = entities[move.elementId];
          if (!element) return null;
          return (
            <div
              key={`${connectionId}:${move.elementId}`}
              className="remote-ghost"
              aria-hidden="true"
              style={{ transform: `translate(${move.x}px, ${move.y}px)`, width: element.width, height: element.height, borderColor: participant.color }}
            />
          );
        });
      })}
    </>
  );
}

const EditingMarker = memo(function EditingMarker({ connectionId }: { connectionId: string }) {
  const participant = useAppSelector((state) => selectParticipant(state, connectionId));
  const elementId = participant?.editingElementId ?? null;
  const rect = useAppSelector((state) => {
    const element = elementId ? state.board.elements.entities[elementId] : undefined;
    return element ? { x: element.x, y: element.y, width: element.width, height: element.height } : null;
  }, shallowEqual);
  if (!participant || !rect) return null;

  return (
    <div
      className="remote-editing"
      style={{ transform: `translate(${rect.x - 4}px, ${rect.y - 4}px)`, width: rect.width + 8, height: rect.height + 8, borderColor: participant.color }}
      role="note"
      aria-label={`${participant.displayName} is editing this`}
    >
      <span className="remote-tag" style={{ background: participant.color }}>
        {participant.displayName} is editing
      </span>
    </div>
  );
});

export function RemoteEditingMarkers() {
  const ids = useAppSelector(selectParticipantIds);
  return (
    <>
      {ids.map((id) => (
        <EditingMarker key={id} connectionId={id} />
      ))}
    </>
  );
}
