'use client';

import Link from 'next/link';
import { useState, type ReactNode } from 'react';
import { Icon } from '@/shared/ui/Icon';
import { noticeShown } from '@/shared/ui/noticesSlice';
import { toProblem } from '@/shared/api/problems';
import { useAppDispatch, useAppSelector } from '@/store/hooks';
import { selectBoardState, selectBoardSummary, selectCanEdit } from '../board/selectors';
import { useRenameBoardMutation } from '../catalog/boardsApi';
import { DocumentMenu } from '../documents/DocumentMenu';
import { selectCanRedo, selectCanUndo, selectRedoLabel, selectUndoLabel } from '../history/historySlice';
import { modifierKey } from '../shortcuts/shortcuts';
import { useBoardContext } from './BoardContext';
import { redo, undo } from './commands';
import { helpToggled, panelToggled } from './editorSlice';

function BoardTitle() {
  const board = useAppSelector(selectBoardSummary);
  const canEdit = useAppSelector(selectCanEdit);
  const [renameBoard] = useRenameBoardMutation();
  const dispatch = useAppDispatch();
  const [draft, setDraft] = useState<string | null>(null);
  if (!board) return null;

  const commit = async () => {
    const name = draft?.trim();
    setDraft(null);
    if (!name || name === board.name) return;
    await renameBoard({ board, name })
      .unwrap()
      .catch((error: unknown) => {
        const problem = toProblem(error);
        dispatch(noticeShown({ tone: 'error', text: problem?.detail ?? 'The board could not be renamed.', fix: problem?.fix }));
      });
  };

  if (draft !== null) {
    return (
      <input
        className="input board-title-input"
        aria-label="Board name"
        value={draft}
        maxLength={120}
        autoFocus
        onChange={(event) => setDraft(event.target.value)}
        onBlur={() => void commit()}
        onKeyDown={(event) => {
          if (event.key === 'Enter') void commit();
          if (event.key === 'Escape') setDraft(null);
        }}
      />
    );
  }

  return canEdit ? (
    <button type="button" className="board-title-button" onClick={() => setDraft(board.name)} title="Rename the board">
      <h1>{board.name}</h1>
    </button>
  ) : (
    <h1>{board.name}</h1>
  );
}

export function Toolbar({ presence, status }: { presence?: ReactNode; status?: ReactNode }) {
  const { index } = useBoardContext();
  const dispatch = useAppDispatch();
  const { board, permission } = useAppSelector(selectBoardState);
  const panel = useAppSelector((state) => state.editor.panel);
  const canUndo = useAppSelector(selectCanUndo);
  const canRedo = useAppSelector(selectCanRedo);
  const undoLabel = useAppSelector(selectUndoLabel);
  const redoLabel = useAppSelector(selectRedoLabel);
  const canEdit = useAppSelector(selectCanEdit);
  const mod = modifierKey();

  return (
    <header className="board-toolbar">
      {board && (
        <Link href={`/teams/${board.teamId}`} className="btn btn-ghost btn-icon" aria-label="Back to the team's boards" title="Back to boards">
          <Icon name="chevronLeft" />
        </Link>
      )}
      <div className="board-title">
        <BoardTitle />
        {board && <span className="badge hide-narrow">{index?.levels.get(board.level)?.name ?? board.level}</span>}
        {board?.archivedAt && <span className="badge badge-warning">Archived · read-only</span>}
        {board && !board.archivedAt && permission === 'view' && <span className="badge">View only</span>}
      </div>
      {canEdit && (
        <>
          <span className="toolbar-divider" />
          <div className="toolbar-group" role="group" aria-label="History">
            <button
              type="button"
              className="btn btn-ghost btn-icon"
              disabled={!canUndo}
              onClick={() => dispatch(undo())}
              aria-label={undoLabel ? `Undo: ${undoLabel}` : 'Undo'}
              title={`${undoLabel ? `Undo ${undoLabel.toLowerCase()}` : 'Undo'} (${mod}+Z)`}
            >
              <Icon name="undo" />
            </button>
            <button
              type="button"
              className="btn btn-ghost btn-icon"
              disabled={!canRedo}
              onClick={() => dispatch(redo())}
              aria-label={redoLabel ? `Redo: ${redoLabel}` : 'Redo'}
              title={`${redoLabel ? `Redo ${redoLabel.toLowerCase()}` : 'Redo'} (${mod}+Shift+Z)`}
            >
              <Icon name="redo" />
            </button>
          </div>
        </>
      )}
      <span className="spacer" />
      {status}
      {presence}
      <span className="toolbar-divider" />
      <div className="toolbar-group">
        <button type="button" className="btn btn-ghost btn-icon" aria-pressed={panel === 'search'} aria-label="Search and filter" title={`Search and filter (${mod}+F)`} onClick={() => dispatch(panelToggled('search'))}>
          <Icon name="search" />
        </button>
        <button type="button" className="btn btn-ghost btn-icon" aria-pressed={panel === 'legend'} aria-label="Legend" title="Legend: what each color means" onClick={() => dispatch(panelToggled('legend'))}>
          <Icon name="legend" />
        </button>
        <DocumentMenu />
        <button type="button" className="btn btn-ghost btn-icon" aria-label="Keyboard shortcuts" title="Keyboard shortcuts (?)" onClick={() => dispatch(helpToggled(true))}>
          <Icon name="help" />
        </button>
      </div>
    </header>
  );
}
