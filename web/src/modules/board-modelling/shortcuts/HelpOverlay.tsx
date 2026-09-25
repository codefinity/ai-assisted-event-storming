'use client';

import { Fragment } from 'react';
import { Dialog } from '@/shared/ui/Dialog';
import { useAppDispatch, useAppSelector } from '@/store/hooks';
import { helpToggled } from '../editor/editorSlice';
import { useBoardContext } from '../editor/BoardContext';
import { shortcutGroups } from './shortcuts';

function Keys({ keys }: { keys: string[] }) {
  return (
    <>
      {keys.map((key, index) => (
        <Fragment key={key}>
          {index > 0 && ' '}
          {/^(its letter|or .*|Click|Drag|Double-click|Wheel)$/.test(key) ? <span className="muted">{key}</span> : <kbd>{key}</kbd>}
        </Fragment>
      ))}
    </>
  );
}

export function HelpOverlay() {
  const open = useAppSelector((state) => state.editor.help);
  const dispatch = useAppDispatch();
  const { index } = useBoardContext();
  const types = index?.notation.types.filter((type) => type.shortcut) ?? [];

  return (
    <Dialog open={open} title="Keyboard shortcuts" onClose={() => dispatch(helpToggled(false))} wide>
      <div className="shortcut-columns">
        {shortcutGroups().map((group) => (
          <table key={group.title} className="shortcut-table">
            <thead>
              <tr>
                <th colSpan={2} scope="colgroup">
                  {group.title}
                </th>
              </tr>
            </thead>
            <tbody>
              {group.items.map((item) => (
                <tr key={item.action}>
                  <td>{item.action}</td>
                  <td>
                    <Keys keys={item.keys} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ))}
        <table className="shortcut-table">
          <thead>
            <tr>
              <th colSpan={2} scope="colgroup">
                Element types
              </th>
            </tr>
          </thead>
          <tbody>
            {types.map((type) => (
              <tr key={type.id}>
                <td>
                  <span className="chip-swatch" style={{ background: type.color, display: 'inline-block', marginRight: 8 }} aria-hidden="true" />
                  {type.name}
                </td>
                <td>
                  <kbd>{type.shortcut}</kbd>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Dialog>
  );
}
