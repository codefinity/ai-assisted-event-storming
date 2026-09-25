'use client';

import { useParams } from 'next/navigation';
import { BoardEditor } from '@/modules/board-modelling/editor/BoardEditor';
import {
  ConnectionIndicator,
  PresenceBar,
  RemoteCursors,
  RemoteDragGhosts,
  RemoteEditingMarkers,
} from '@/modules/collaboration/presence/PresenceViews';

/** The board editor, with the collaboration features plugged into its slots. */
export default function BoardPage() {
  const { boardId } = useParams<{ boardId: string }>();
  return (
    <BoardEditor
      key={boardId}
      boardId={boardId}
      status={<ConnectionIndicator />}
      presence={<PresenceBar />}
      worldOverlay={
        <>
          <RemoteEditingMarkers />
          <RemoteDragGhosts />
          <RemoteCursors />
        </>
      }
    />
  );
}
