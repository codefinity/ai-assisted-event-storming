'use client';

import { useCallback, useMemo, useState } from 'react';
import { Icon } from '@/shared/ui/Icon';
import { useAppDispatch, useAppSelector } from '@/store/hooks';
import { selectBoardSummary } from '../board/selectors';
import { elementTypeMime } from '../canvas/Canvas';
import { newElementOf } from '../editor/commands';
import { draftStarted, placingSet } from '../editor/editorSlice';
import { useBoardContext } from '../editor/BoardContext';
import { paletteFor, type ElementType } from '../notation/notation';

type ShowTip = (type: ElementType | null, anchor?: HTMLElement) => void;

function PaletteItem({ type, active, showTip }: { type: ElementType; active: boolean; showTip: ShowTip }) {
  const dispatch = useAppDispatch();
  const { viewCenter } = useBoardContext();

  return (
    <button
      type="button"
      className="palette-item"
      aria-pressed={active}
      aria-description={[type.description, type.whenToUse && `When: ${type.whenToUse}`].filter(Boolean).join(' ')}
      onPointerEnter={(event) => showTip(type, event.currentTarget)}
      onPointerLeave={() => showTip(null)}
      onFocus={(event) => showTip(type, event.currentTarget)}
      onBlur={() => showTip(null)}
      draggable
      onDragStart={(event) => {
        event.dataTransfer.setData(elementTypeMime, type.id);
        event.dataTransfer.effectAllowed = 'copy';
      }}
      onClick={(event) => {
        // From the keyboard, place it straight away in the middle of the view; with a pointer, arm it for the next click.
        if (event.detail === 0) dispatch(draftStarted(newElementOf(type, viewCenter())));
        else dispatch(placingSet(active ? null : type.id));
      }}
    >
      <span className="palette-swatch" style={{ background: type.color }}>
        <Icon name={type.icon} size={12} />
      </span>
      <span className="palette-name">{type.name}</span>
      {type.shortcut && <kbd aria-label={`Shortcut ${type.shortcut}`}>{type.shortcut}</kbd>}
    </button>
  );
}

/** Outside the palette's scroll area, so it can sit beside it. Screen readers get the same text from aria-description. */
function PaletteTip({ tip }: { tip: { type: ElementType; left: number; top: number } | null }) {
  if (!tip) return null;
  const { type } = tip;
  return (
    <div className="palette-tip" style={{ left: tip.left, top: tip.top }} aria-hidden="true">
      <strong>{type.name}</strong>
      {type.description}
      {type.whenToUse && <em>When: {type.whenToUse}</em>}
      {type.writingRule && <em>Write: {type.writingRule}</em>}
    </div>
  );
}

/** The element types for this board's level, straight from the registry. The rest are one click away. */
export function Palette() {
  const { index, canEdit } = useBoardContext();
  const level = useAppSelector((state) => selectBoardSummary(state)?.level);
  const placing = useAppSelector((state) => state.editor.placing);
  const [showMore, setShowMore] = useState(false);
  const [tip, setTip] = useState<{ type: ElementType; left: number; top: number } | null>(null);
  const showTip = useCallback<ShowTip>((type, anchor) => {
    if (!type || !anchor) {
      setTip(null);
      return;
    }
    const rect = anchor.getBoundingClientRect();
    setTip({ type, left: rect.right + 12, top: rect.top + rect.height / 2 });
  }, []);
  const palette = useMemo(() => (index && level ? paletteFor(index, level) : { primary: [], more: [] }), [index, level]);

  if (!canEdit) return null;

  return (
    <nav className="palette" aria-label="Element types">
      <p className="palette-heading" id="palette-heading">
        {index?.levels.get(level!)?.name ?? 'Elements'}
      </p>
      <div role="group" aria-labelledby="palette-heading" className="stack" style={{ gap: 2 }}>
        {palette.primary.map((type) => (
          <PaletteItem key={type.id} type={type} active={placing === type.id} showTip={showTip} />
        ))}
      </div>
      {palette.more.length > 0 && (
        <>
          <button type="button" className="btn btn-ghost btn-sm" aria-expanded={showMore} onClick={() => setShowMore((value) => !value)}>
            <Icon name="chevronDown" style={{ transform: showMore ? 'rotate(180deg)' : undefined }} />
            {showMore ? 'Fewer types' : `${palette.more.length} more types`}
          </button>
          {showMore && palette.more.map((type) => <PaletteItem key={type.id} type={type} active={placing === type.id} showTip={showTip} />)}
        </>
      )}
      <p className="palette-footer">Click a type, then click the board. Or press its letter.</p>
      <PaletteTip tip={tip} />
    </nav>
  );
}
