'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useRef, useState, type FormEvent } from 'react';
import { toProblem, type Problem } from '@/shared/api/problems';
import { Dialog } from '@/shared/ui/Dialog';
import { EmptyState, PageLoading, relativeTime } from '@/shared/ui/feedback';
import { FormProblem, SelectField, TextField } from '@/shared/ui/forms';
import { Icon } from '@/shared/ui/Icon';
import { Menu } from '@/shared/ui/Menu';
import type { BoardLevel, BoardSummary } from '../model';
import { downloadJson, readJsonFile } from '../documents/files';
import {
  boardsApi,
  useArchiveBoardMutation,
  useCreateBoardMutation,
  useDeleteBoardMutation,
  useDuplicateBoardMutation,
  useGetNotationQuery,
  useImportBoardMutation,
  useListBoardsQuery,
  useRenameBoardMutation,
  useRestoreBoardMutation,
} from './boardsApi';
import { useAppDispatch } from '@/store/hooks';

function levelName(levels: ReadonlyArray<{ id: string; name: string }> | undefined, level: BoardLevel) {
  return levels?.find((candidate) => candidate.id === level)?.name ?? level;
}

function NewBoardDialog({ teamId, open, onClose }: { teamId: string; open: boolean; onClose: () => void }) {
  const { data: notation } = useGetNotationQuery();
  const [createBoard, { isLoading, error, reset }] = useCreateBoardMutation();
  const [name, setName] = useState('');
  const [level, setLevel] = useState<BoardLevel>('big-picture');
  const router = useRouter();
  const problem = toProblem(error);
  const levels = notation?.levels ?? [];

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const board = await createBoard({ teamId, name, level }).unwrap().catch(() => null);
    if (board) router.push(`/boards/${board.id}`);
  };

  return (
    <Dialog
      open={open}
      title="New board"
      onClose={() => {
        reset();
        onClose();
      }}
    >
      <form className="stack" onSubmit={submit} noValidate>
        <FormProblem problem={problem} fields={['name', 'level']} />
        <TextField label="Name" name="name" value={name} onChange={(event) => setName(event.target.value)} problem={problem} placeholder="e.g. Ordering food" autoFocus required />
        <SelectField
          label="Level"
          name="level"
          value={level}
          onChange={(event) => setLevel(event.target.value as BoardLevel)}
          options={levels.map((candidate) => ({ value: candidate.id, label: candidate.name }))}
          hint={levels.find((candidate) => candidate.id === level)?.description}
          problem={problem}
        />
        <div className="row row-end">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={isLoading || name.trim() === ''}>
            Create board
          </button>
        </div>
      </form>
    </Dialog>
  );
}

function RenameDialog({ board, onClose }: { board: BoardSummary; onClose: () => void }) {
  const [renameBoard, { isLoading, error }] = useRenameBoardMutation();
  const [name, setName] = useState(board.name);
  const problem = toProblem(error);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const renamed = await renameBoard({ board, name }).unwrap().catch(() => null);
    if (renamed) onClose();
  };

  return (
    <Dialog open title="Rename board" onClose={onClose}>
      <form className="stack" onSubmit={submit} noValidate>
        <FormProblem problem={problem} fields={['name']} />
        <TextField label="Name" name="name" value={name} onChange={(event) => setName(event.target.value)} problem={problem} autoFocus required />
        <div className="row row-end">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={isLoading || name.trim() === ''}>
            Rename
          </button>
        </div>
      </form>
    </Dialog>
  );
}

function DeleteDialog({ board, onClose }: { board: BoardSummary; onClose: () => void }) {
  const [deleteBoard, { isLoading, error }] = useDeleteBoardMutation();
  const [typed, setTyped] = useState('');
  const problem = toProblem(error);
  const confirmed = typed.trim() === board.name.trim();

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (!confirmed) return;
    const deleted = await deleteBoard(board).unwrap().then(() => true, () => false);
    if (deleted) onClose();
  };

  return (
    <Dialog open title="Delete board?" onClose={onClose}>
      <form className="stack" onSubmit={submit} noValidate>
        <p>
          “{board.name}” and everything on it — {board.elementCount} {board.elementCount === 1 ? 'element' : 'elements'} and all of their connections — is deleted for good. Anyone with the board
          open is closed out of it. This cannot be undone: export it first if you want to keep a copy, or archive it instead.
        </p>
        <FormProblem problem={problem} />
        <TextField label={`Type “${board.name}” to confirm`} name="confirm" value={typed} onChange={(event) => setTyped(event.target.value)} autoComplete="off" autoFocus />
        <div className="row row-end">
          <button type="button" className="btn" onClick={onClose}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary btn-danger-solid" disabled={isLoading || !confirmed}>
            Delete board
          </button>
        </div>
      </form>
    </Dialog>
  );
}

function BoardCard({
  board,
  canEdit,
  levels,
  onRename,
  onDelete,
  onProblem,
}: {
  board: BoardSummary;
  canEdit: boolean;
  levels?: ReadonlyArray<{ id: string; name: string }>;
  onRename: () => void;
  onDelete: () => void;
  onProblem: (problem?: Problem) => void;
}) {
  const [duplicateBoard] = useDuplicateBoardMutation();
  const [archiveBoard] = useArchiveBoardMutation();
  const [restoreBoard] = useRestoreBoardMutation();
  const dispatch = useAppDispatch();
  const router = useRouter();
  const archived = board.archivedAt !== null;

  const run = (action: Promise<unknown>) => action.then(() => onProblem(undefined), (error: unknown) => onProblem(toProblem(error)));

  return (
    <li className={`card board-card${archived ? ' board-card-archived' : ''}`}>
      <Link href={`/boards/${board.id}`} className="board-card-link">
        <span className="board-card-preview" aria-hidden="true">
          <span style={{ background: '#FFA94D' }} />
          <span style={{ background: '#74C0FC' }} />
          <span style={{ background: '#FFE066' }} />
        </span>
        <h2>{board.name}</h2>
      </Link>
      <div className="row row-wrap">
        <span className="badge">{levelName(levels, board.level)}</span>
        <span className="badge">
          {board.elementCount} {board.elementCount === 1 ? 'element' : 'elements'}
        </span>
        {archived && <span className="badge badge-warning">Archived</span>}
      </div>
      <p className="muted board-card-meta">
        Updated {relativeTime(board.updatedAt)} by {board.updatedBy.name}
      </p>
      <div className="board-card-menu">
        <Menu
          label={`Actions for ${board.name}`}
          trigger={<Icon name="more" />}
          items={[
            { label: 'Open', onSelect: () => router.push(`/boards/${board.id}`) },
            {
              label: 'Export as JSON',
              onSelect: () =>
                void run(
                  dispatch(boardsApi.endpoints.exportBoardDocument.initiate(board.id, { forceRefetch: true, subscribe: false }))
                    .unwrap()
                    .then((document) => downloadJson(document, `${board.name}.board.json`)),
                ),
            },
            'separator',
            { label: 'Rename…', onSelect: onRename, disabled: !canEdit || archived },
            { label: 'Duplicate', onSelect: () => void run(duplicateBoard({ board }).unwrap()), disabled: !canEdit },
            archived
              ? { label: 'Restore', onSelect: () => void run(restoreBoard(board).unwrap()), disabled: !canEdit }
              : { label: 'Archive', onSelect: () => void run(archiveBoard(board).unwrap()), disabled: !canEdit, danger: true },
            { label: 'Delete…', onSelect: onDelete, disabled: !canEdit, danger: true },
          ]}
        />
      </div>
    </li>
  );
}

export function BoardDashboard({ teamId }: { teamId: string }) {
  const [includeArchived, setIncludeArchived] = useState(false);
  const [cursor, setCursor] = useState<string | undefined>(undefined);
  const { data, isLoading, isFetching, error } = useListBoardsQuery({ teamId, includeArchived, cursor });
  const { data: notation } = useGetNotationQuery();
  const [importBoard, importing] = useImportBoardMutation();
  const [creating, setCreating] = useState(false);
  const [renaming, setRenaming] = useState<BoardSummary | null>(null);
  const [deleting, setDeleting] = useState<BoardSummary | null>(null);
  const [problem, setProblem] = useState<Problem | undefined>(undefined);
  const fileInput = useRef<HTMLInputElement>(null);
  const router = useRouter();

  if (isLoading) return <PageLoading label="Loading boards" />;
  if (error && !data) return <FormProblem problem={toProblem(error)} />;

  const canEdit = data?.canEdit ?? false;
  const boards = data?.items ?? [];

  const onImport = async (file: File | undefined) => {
    if (!file) return;
    const parsed = await readJsonFile(file);
    if (!parsed.ok) {
      setProblem(parsed.problem);
      return;
    }

    const imported = await importBoard({ teamId, document: parsed.value }).unwrap().catch((reason: unknown) => {
      setProblem(toProblem(reason));
      return null;
    });
    if (imported) router.push(`/boards/${imported.board.id}`);
  };

  return (
    <section aria-labelledby="boards-heading" className="stack">
      <div className="page-header">
        <h2 id="boards-heading">Boards</h2>
        <span className="spacer" />
        <label className="checkbox">
          <input
            type="checkbox"
            checked={includeArchived}
            onChange={(event) => {
              setCursor(undefined);
              setIncludeArchived(event.target.checked);
            }}
          />
          Show archived
        </label>
        {canEdit && (
          <>
            <button type="button" className="btn" onClick={() => fileInput.current?.click()} disabled={importing.isLoading}>
              <Icon name="upload" />
              Import JSON
            </button>
            <input
              ref={fileInput}
              type="file"
              accept="application/json,.json"
              hidden
              onChange={(event) => {
                void onImport(event.target.files?.[0]);
                event.target.value = '';
              }}
            />
            <button type="button" className="btn btn-primary" onClick={() => setCreating(true)}>
              <Icon name="plus" />
              New board
            </button>
          </>
        )}
      </div>
      <FormProblem problem={problem} />
      {boards.length === 0 ? (
        <EmptyState title="No boards yet">
          {canEdit ? 'Start a Big Picture board to explore the whole domain, or import a Board Document.' : 'Nothing to see here yet.'}
        </EmptyState>
      ) : (
        <ul className="card-grid" aria-label="Boards">
          {boards.map((board) => (
            <BoardCard key={board.id} board={board} canEdit={canEdit} levels={notation?.levels} onRename={() => setRenaming(board)} onDelete={() => setDeleting(board)} onProblem={setProblem} />
          ))}
        </ul>
      )}
      {data?.nextCursor && (
        <div className="row row-center">
          <button type="button" className="btn" disabled={isFetching} onClick={() => setCursor(data.nextCursor ?? undefined)}>
            Load more
          </button>
        </div>
      )}
      <NewBoardDialog teamId={teamId} open={creating} onClose={() => setCreating(false)} />
      {renaming && <RenameDialog board={renaming} onClose={() => setRenaming(null)} />}
      {deleting && <DeleteDialog board={deleting} onClose={() => setDeleting(null)} />}
    </section>
  );
}
