'use client';

import { useRef, useState } from 'react';
import { toProblem, type Problem } from '@/shared/api/problems';
import { Dialog } from '@/shared/ui/Dialog';
import { FormProblem } from '@/shared/ui/forms';
import { Icon } from '@/shared/ui/Icon';
import { Menu } from '@/shared/ui/Menu';
import { noticeShown } from '@/shared/ui/noticesSlice';
import { useAppDispatch, useAppSelector, useAppStore } from '@/store/hooks';
import { selectAllConnections, selectAllElements, selectBoardSummary, selectCanEdit } from '../board/selectors';
import { boardsApi, useReplaceBoardDocumentMutation } from '../catalog/boardsApi';
import { useBoardContext } from '../editor/BoardContext';
import { boardToSvg } from './exportSvg';
import { downloadBlob, downloadJson, readJsonFile } from './files';

/** Export as JSON or SVG, and replace the content from a Board Document. */
export function DocumentMenu() {
  const { index } = useBoardContext();
  const dispatch = useAppDispatch();
  const store = useAppStore();
  const board = useAppSelector(selectBoardSummary);
  const canEdit = useAppSelector(selectCanEdit);
  const [replaceDocument, replacing] = useReplaceBoardDocumentMutation();
  const [pending, setPending] = useState<{ name: string; document: unknown } | null>(null);
  const [problem, setProblem] = useState<Problem | undefined>(undefined);
  const fileInput = useRef<HTMLInputElement>(null);

  if (!board) return null;

  const exportJson = async () => {
    try {
      const document = await dispatch(boardsApi.endpoints.exportBoardDocument.initiate(board.id, { forceRefetch: true, subscribe: false })).unwrap();
      downloadJson(document, `${board.name}.board.json`);
    } catch (error) {
      setProblem(toProblem(error));
    }
  };

  const exportSvg = () => {
    const state = store.getState();
    const svg = boardToSvg(selectAllElements(state), selectAllConnections(state), index, board.name);
    downloadBlob(new Blob([svg], { type: 'image/svg+xml' }), `${board.name}.svg`);
  };

  const choose = async (file: File | undefined) => {
    if (!file) return;
    const parsed = await readJsonFile(file);
    if (parsed.ok) {
      setProblem(undefined);
      setPending({ name: file.name, document: parsed.value });
    } else {
      setProblem(parsed.problem);
    }
  };

  const replace = async () => {
    if (!pending) return;
    try {
      const result = await replaceDocument({ boardId: board.id, document: pending.document }).unwrap();
      setPending(null);
      dispatch(noticeShown({ tone: 'info', text: `The board now has ${result.elementCount} elements and ${result.connectionCount} arrows from “${pending.name}”.` }));
    } catch (error) {
      setProblem(toProblem(error));
    }
  };

  return (
    <>
      <Menu
        label="Import and export"
        trigger={<Icon name="download" />}
        items={[
          { label: 'Export as JSON (Board Document)', onSelect: () => void exportJson() },
          { label: 'Export as SVG image', onSelect: exportSvg },
          'separator',
          { label: 'Replace content from JSON…', onSelect: () => fileInput.current?.click(), disabled: !canEdit },
        ]}
      />
      <input
        ref={fileInput}
        type="file"
        accept="application/json,.json"
        hidden
        onChange={(event) => {
          void choose(event.target.files?.[0]);
          event.target.value = '';
        }}
      />
      <Dialog
        open={pending !== null || problem !== undefined}
        title={pending ? 'Replace this board’s content?' : 'That did not work'}
        onClose={() => {
          setPending(null);
          setProblem(undefined);
        }}
        wide={problem !== undefined}
        actions={
          pending ? (
            <>
              <button type="button" className="btn" onClick={() => setPending(null)}>
                Cancel
              </button>
              <button type="button" className="btn btn-primary btn-danger-solid" disabled={replacing.isLoading} onClick={() => void replace()}>
                Replace everything
              </button>
            </>
          ) : undefined
        }
      >
        <div className="stack">
          {pending && (
            <p>
              Everything on “{board.name}” is replaced by the content of <strong>{pending.name}</strong>. Everyone with the board open sees the change at once, and it cannot be undone.
              Export first if you want to keep a copy.
            </p>
          )}
          <FormProblem problem={problem} />
        </div>
      </Dialog>
    </>
  );
}
