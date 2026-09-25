'use client';

import { useMemo } from 'react';
import { Icon } from '@/shared/ui/Icon';
import { useAppSelector } from '@/store/hooks';
import { selectBoardSummary } from '../board/selectors';
import { useBoardContext } from '../editor/BoardContext';
import { paletteFor } from '../notation/notation';

/** What each color means at this level, with the registry's own guidance for when and how to use it. */
export function Legend() {
  const { index } = useBoardContext();
  const level = useAppSelector((state) => selectBoardSummary(state)?.level);
  const palette = useMemo(() => (index && level ? paletteFor(index, level) : null), [index, level]);
  if (!index || !level || !palette) return null;
  const info = index.levels.get(level);

  return (
    <div>
      {info && (
        <p className="muted" style={{ marginBottom: 8 }}>
          <strong style={{ color: 'var(--text)' }}>{info.name}.</strong> {info.description}
        </p>
      )}
      {[...palette.primary, ...palette.more].map((type) => (
        <details key={type.id} className="legend-item">
          <summary>
            <span className="palette-swatch" style={{ background: type.color }}>
              <Icon name={type.icon} size={12} />
            </span>
            <span className="spacer">{type.name}</span>
            {type.shortcut && <kbd>{type.shortcut}</kbd>}
          </summary>
          <p>{type.description}</p>
          {type.whenToUse && (
            <p>
              <strong>When:</strong> {type.whenToUse}
            </p>
          )}
          {type.writingRule && (
            <p>
              <strong>Write it:</strong> {type.writingRule}
            </p>
          )}
          {type.canBePivotal && <p>Can be marked pivotal: a turning point that divides the timeline.</p>}
          {type.examples.length > 0 && (
            <ul aria-label={`Examples of ${type.name}`}>
              {type.examples.map((example) => (
                <li key={example}>{example}</li>
              ))}
            </ul>
          )}
        </details>
      ))}
    </div>
  );
}
