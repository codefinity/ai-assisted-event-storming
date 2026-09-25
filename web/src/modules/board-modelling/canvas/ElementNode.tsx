'use client';

import { memo, useEffect, useRef, useState, type CSSProperties, type KeyboardEvent } from 'react';
import { newId } from '@/shared/lib/ids';
import { Icon } from '@/shared/ui/Icon';
import { useAppDispatch, useAppSelector } from '@/store/hooks';
import type { RootState } from '@/store/types';
import { selectElementById, selectTimelineOrder } from '../board/selectors';
import type { NewElementData } from '../board/ops';
import { createElements, updateElement } from '../editor/commands';
import { draftEnded, draftStarted, editEnded, selectDragDx, selectDragDy, selectResizeOf } from '../editor/editorSlice';
import { useBoardContext } from '../editor/BoardContext';
import { typeOf, type ElementType } from '../notation/notation';
import { selectIsDimmed } from '../search/search';

function isSingleSelection(state: RootState, id: string) {
  if (state.editor.selected[id] !== true || state.editor.drag) return false;
  for (const other in state.editor.selected) if (other !== id) return false;
  return true;
}

function styleOf(type: ElementType, color: string | null, x: number, y: number, width: number, height: number): CSSProperties {
  return {
    transform: `translate(${x}px, ${y}px)`,
    width,
    height,
    ['--el-color' as string]: color ?? type.color,
    ['--el-text' as string]: type.textColor,
  };
}

interface TextEditorProps {
  initial: string;
  maxLength: number;
  multiline: boolean;
  label: string;
  onCommit: (text: string, via: 'enter' | 'tab' | 'blur') => void;
  onCancel: () => void;
}

/** Enter commits, Shift+Enter breaks the line, Escape cancels, Tab commits and moves on. */
function TextEditor({ initial, maxLength, multiline, label, onCommit, onCancel }: TextEditorProps) {
  const [text, setText] = useState(initial);
  const done = useRef(false);
  const ref = useRef<HTMLTextAreaElement & HTMLInputElement>(null);

  useEffect(() => {
    ref.current?.focus();
    ref.current?.select();
  }, []);

  const finish = (via: 'enter' | 'tab' | 'blur') => {
    if (done.current) return;
    done.current = true;
    onCommit(text, via);
  };

  const onKeyDown = (event: KeyboardEvent) => {
    event.stopPropagation();
    if (event.key === 'Escape') {
      event.preventDefault();
      done.current = true;
      onCancel();
    } else if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      finish('enter');
    } else if (event.key === 'Tab') {
      event.preventDefault();
      finish('tab');
    }
  };

  const props = {
    ref,
    'data-ui': true,
    'aria-label': label,
    value: text,
    maxLength,
    onChange: (event: { target: { value: string } }) => setText(event.target.value),
    onKeyDown,
    onBlur: () => finish('blur'),
    onPointerDown: (event: { stopPropagation: () => void }) => event.stopPropagation(),
  };

  return multiline ? <textarea className="el-editor" {...props} /> : <input className="el-label-editor" {...props} />;
}

export const ElementNode = memo(function ElementNode({ id }: { id: string }) {
  const { index, canEdit } = useBoardContext();
  const dispatch = useAppDispatch();
  const element = useAppSelector((state) => selectElementById(state, id));
  const selected = useAppSelector((state) => state.editor.selected[id] === true);
  const single = useAppSelector((state) => isSingleSelection(state, id));
  const dx = useAppSelector((state) => selectDragDx(state, id));
  const dy = useAppSelector((state) => selectDragDy(state, id));
  const resize = useAppSelector((state) => selectResizeOf(state, id));
  const editing = useAppSelector((state) => state.editor.editing === id);
  const dimmed = useAppSelector((state) => selectIsDimmed(state, id, index));
  const tabbable = useAppSelector((state) => state.editor.focusId === id || (state.editor.focusId === null && selectTimelineOrder(state)[0] === id));
  const root = useRef<HTMLDivElement>(null);
  const wasEditing = useRef(false);

  // Hand focus back to the element when its text editor closes, so the keyboard carries on from here.
  useEffect(() => {
    if (wasEditing.current && !editing && (document.activeElement === document.body || document.activeElement === null)) {
      root.current?.focus({ preventScroll: true });
    }
    wasEditing.current = editing;
  }, [editing]);

  if (!element) return null;

  const type = typeOf(index, element.type);
  const width = resize?.width ?? element.width;
  const height = resize?.height ?? element.height;
  const x = element.x + dx;
  const y = element.y + dy;
  const dragging = dx !== 0 || dy !== 0;
  const renderer = type.renderer === 'lane' || type.renderer === 'area' ? type.renderer : 'sticky';
  const label = `${type.name}${element.pivotal ? ', pivotal' : ''}: ${element.text || 'no text yet'}`;

  const commit = (text: string) => {
    if (text !== element.text) dispatch(updateElement(id, { text }, 'Edit text'));
    dispatch(editEnded());
  };

  const editor = editing && (
    <TextEditor
      initial={element.text}
      maxLength={type.maxTextLength}
      multiline={renderer === 'sticky'}
      label={`Text of this ${type.name}`}
      onCommit={commit}
      onCancel={() => dispatch(editEnded())}
    />
  );

  const className = [
    'el',
    `el-${renderer}`,
    element.pivotal && 'el-pivotal',
    selected && 'el-selected',
    dimmed && 'el-dimmed',
    dragging && 'el-dragging',
  ]
    .filter(Boolean)
    .join(' ');

  return (
    <div
      ref={root}
      className={className}
      style={styleOf(type, element.color, x, y, width, height)}
      data-element-id={id}
      role="button"
      aria-roledescription={type.name}
      aria-label={label}
      aria-pressed={selected}
      tabIndex={tabbable ? 0 : -1}
    >
      {renderer === 'sticky' ? (
        <>
          <span className="el-kind" aria-hidden="true">
            <Icon name={type.icon} size={11} />
            {type.name}
            {element.pivotal && <Icon name="flag" size={12} className="el-pivotal-flag" />}
          </span>
          {editor || (
            <span className="el-text" data-placeholder={canEdit ? 'Double-click to write' : ''}>
              {element.text}
            </span>
          )}
        </>
      ) : renderer === 'lane' ? (
        <span className="el-lane-label" data-grab="true">
          {editor || element.text || type.name}
        </span>
      ) : (
        <span className="el-area-label" data-grab="true">
          {editor || element.text || type.name}
        </span>
      )}
      {single && canEdit && !editing && (
        <>
          <span className="handle handle-resize" data-handle="resize" aria-hidden="true" />
          {renderer === 'sticky' && <span className="handle handle-connect" data-handle="connect" title="Drag to another sticky to connect" aria-hidden="true" />}
        </>
      )}
    </div>
  );
});

/** A new element being typed. It becomes a real element when the text is committed. */
export function DraftNode({ draft }: { draft: NewElementData }) {
  const { index } = useBoardContext();
  const dispatch = useAppDispatch();
  const type = typeOf(index, draft.type);
  const renderer = type.renderer === 'lane' || type.renderer === 'area' ? type.renderer : 'sticky';

  const commit = (text: string, via: 'enter' | 'tab' | 'blur') => {
    const kept = text.trim().length > 0 || renderer !== 'sticky';
    if (kept) dispatch(createElements([{ ...draft, text: text.trim() }], [], `Add ${type.name}`));
    // Tab carries on along the timeline: the next sticky of the same type, just to the right.
    if (via === 'tab' && kept && renderer === 'sticky') {
      dispatch(draftStarted({ ...draft, id: newId(), text: '', x: draft.x + draft.width + 24 }));
    } else {
      dispatch(draftEnded());
    }
  };

  return (
    <div className={`el el-${renderer} el-selected`} style={styleOf(type, draft.color, draft.x, draft.y, draft.width, draft.height)} data-ui="true">
      {renderer === 'sticky' ? (
        <>
          <span className="el-kind" aria-hidden="true">
            <Icon name={type.icon} size={11} />
            {type.name}
          </span>
          <TextEditor key={draft.id} initial="" maxLength={type.maxTextLength} multiline label={`Text of the new ${type.name}`} onCommit={commit} onCancel={() => dispatch(draftEnded())} />
        </>
      ) : (
        <span className={renderer === 'lane' ? 'el-lane-label' : 'el-area-label'}>
          <TextEditor key={draft.id} initial="" maxLength={type.maxTextLength} multiline={false} label={`Name of the new ${type.name}`} onCommit={commit} onCancel={() => dispatch(draftEnded())} />
        </span>
      )}
    </div>
  );
}
