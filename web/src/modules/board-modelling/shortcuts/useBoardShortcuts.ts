'use client';

import { useEffect } from 'react';
import { useAppDispatch, useAppStore } from '@/store/hooks';
import { selectCanEdit } from '../board/selectors';
import { cutSelection, duplicateSelection, parsePayload, pastePayload, payloadFromText, payloadOf } from '../clipboard/clipboard';
import { deleteConnection, deleteElements, newElementOf, nudgeElements, redo, undo } from '../editor/commands';
import {
  draftStarted,
  editStarted,
  helpToggled,
  panelClosed,
  panelToggled,
  placingSet,
  quickAddClosed,
  selectionCleared,
  selectionSet,
} from '../editor/editorSlice';
import type { BoardContextValue } from '../editor/BoardContext';
import { paletteFor } from '../notation/notation';

function isTyping(target: EventTarget | null): boolean {
  const element = target as HTMLElement | null;
  return !!element?.closest?.('input, textarea, select, [contenteditable="true"]');
}

/** Shortcuts that belong to someone else right now: an open dialog, a menu, the quick-add list. */
function isElsewhere(target: EventTarget | null): boolean {
  const element = target as HTMLElement | null;
  return !!element?.closest?.('[role="menu"], dialog') || document.querySelector('dialog[open]') !== null;
}

/** The board's keyboard map. See shortcuts.ts for the list shown to people. */
export function useBoardShortcuts(board: BoardContextValue) {
  const dispatch = useAppDispatch();
  const store = useAppStore();

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.defaultPrevented || isTyping(event.target) || isElsewhere(event.target)) return;
      const state = store.getState();
      const mod = event.ctrlKey || event.metaKey;
      const key = event.key;
      const lower = key.toLowerCase();
      const canEdit = selectCanEdit(state);
      const selected = Object.keys(state.editor.selected);
      const handled = () => event.preventDefault();

      if (key === '?') {
        handled();
        dispatch(helpToggled(undefined));
      } else if (key === 'Escape') {
        if (state.editor.quickAdd) dispatch(quickAddClosed());
        else if (state.editor.placing) dispatch(placingSet(null));
        else if (selected.length > 0 || state.editor.selectedConnection) dispatch(selectionCleared());
        else if (state.editor.panel) dispatch(panelClosed());
      } else if (mod && lower === 'z') {
        handled();
        dispatch(event.shiftKey ? redo() : undo());
      } else if (mod && lower === 'y') {
        handled();
        dispatch(redo());
      } else if (mod && lower === 'a') {
        handled();
        dispatch(selectionSet(state.board.elements.ids));
      } else if (mod && lower === 'd') {
        handled();
        dispatch(duplicateSelection());
      } else if ((mod && lower === 'f') || (key === '/' && !mod)) {
        handled();
        if (state.editor.panel !== 'search') dispatch(panelToggled('search'));
        else document.getElementById('board-search')?.focus();
      } else if (key === 'Delete' || key === 'Backspace') {
        handled();
        if (state.editor.selectedConnection) dispatch(deleteConnection(state.editor.selectedConnection));
        else if (selected.length > 0) dispatch(deleteElements(selected));
      } else if ((key === 'Enter' || key === 'F2') && selected.length === 1 && canEdit) {
        handled();
        dispatch(editStarted(selected[0]!));
      } else if (key.startsWith('Arrow') && selected.length > 0 && canEdit && !mod) {
        handled();
        const step = event.shiftKey ? 40 : 8;
        const dx = key === 'ArrowLeft' ? -step : key === 'ArrowRight' ? step : 0;
        const dy = key === 'ArrowUp' ? -step : key === 'ArrowDown' ? step : 0;
        dispatch(nudgeElements(selected, dx, dy));
      } else if (!mod && !event.altKey) {
        if (key === '+' || key === '=') board.zoomBy(1.2);
        else if (key === '-' || key === '_') board.zoomBy(1 / 1.2);
        else if (key === '0') board.zoomBy(1 / state.editor.viewport.zoom);
        else if (key === '!' || (event.shiftKey && event.code === 'Digit1')) board.fitTo();
        else {
          const type = board.index?.byShortcut.get(lower);
          if (type && canEdit && key.length === 1) {
            handled();
            dispatch(draftStarted(newElementOf(type, board.pointerRef.current ?? board.viewCenter())));
          }
        }
      }
    };

    // Copy and paste go through the clipboard events, which need no permission prompt.
    const onCopy = (event: ClipboardEvent) => {
      if (isTyping(event.target)) return;
      const state = store.getState();
      const payload = payloadOf(state, Object.keys(state.editor.selected));
      if (!payload || !event.clipboardData) return;
      event.clipboardData.setData('text/plain', JSON.stringify(payload));
      event.preventDefault();
    };

    const onCut = (event: ClipboardEvent) => {
      if (isTyping(event.target) || !selectCanEdit(store.getState())) return;
      onCopy(event);
      if (event.defaultPrevented) dispatch(cutSelection());
    };

    const onPaste = (event: ClipboardEvent) => {
      const state = store.getState();
      if (isTyping(event.target) || isElsewhere(event.target) || !selectCanEdit(state)) return;
      const text = event.clipboardData?.getData('text/plain') ?? '';
      const level = state.board.board?.level;
      const defaultType = board.index && level ? paletteFor(board.index, level).primary[0] : undefined;
      const payload = parsePayload(text) ?? (defaultType ? payloadFromText(text, defaultType) : null);
      if (!payload) return;
      event.preventDefault();
      dispatch(pastePayload(payload, board.pointerRef.current));
    };

    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('copy', onCopy);
    window.addEventListener('cut', onCut);
    window.addEventListener('paste', onPaste);
    return () => {
      window.removeEventListener('keydown', onKeyDown);
      window.removeEventListener('copy', onCopy);
      window.removeEventListener('cut', onCut);
      window.removeEventListener('paste', onPaste);
    };
  }, [board, dispatch, store]);
}
