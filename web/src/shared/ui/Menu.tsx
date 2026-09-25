'use client';

import { useEffect, useId, useRef, useState, type ReactNode } from 'react';

export interface MenuItem {
  label: string;
  onSelect: () => void;
  disabled?: boolean;
  danger?: boolean;
  hint?: string;
}

export interface MenuProps {
  /** The trigger's accessible name and content. */
  label: string;
  trigger: ReactNode;
  items: ReadonlyArray<MenuItem | 'separator'>;
  align?: 'start' | 'end';
  triggerClassName?: string;
}

/** A menu button (WAI-ARIA pattern): arrow keys move, Enter picks, Escape closes and returns focus. */
export function Menu({ label, trigger, items, align = 'end', triggerClassName = 'btn btn-ghost btn-icon' }: MenuProps) {
  const [open, setOpen] = useState(false);
  const menuId = useId();
  const buttonRef = useRef<HTMLButtonElement>(null);
  const listRef = useRef<HTMLUListElement>(null);

  useEffect(() => {
    if (!open) return;
    listRef.current?.querySelector<HTMLButtonElement>('button:not(:disabled)')?.focus();
    const onPointerDown = (event: PointerEvent) => {
      if (!listRef.current?.contains(event.target as Node) && !buttonRef.current?.contains(event.target as Node)) setOpen(false);
    };
    document.addEventListener('pointerdown', onPointerDown);
    return () => document.removeEventListener('pointerdown', onPointerDown);
  }, [open]);

  const close = (refocus: boolean) => {
    setOpen(false);
    if (refocus) buttonRef.current?.focus();
  };

  const onKeyDown = (event: React.KeyboardEvent) => {
    const buttons = [...(listRef.current?.querySelectorAll<HTMLButtonElement>('button:not(:disabled)') ?? [])];
    const index = buttons.indexOf(document.activeElement as HTMLButtonElement);
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      buttons[(index + 1) % buttons.length]?.focus();
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      buttons[(index - 1 + buttons.length) % buttons.length]?.focus();
    } else if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      close(true);
    } else if (event.key === 'Tab') {
      close(false);
    }
  };

  return (
    <div className="menu">
      <button
        ref={buttonRef}
        type="button"
        className={triggerClassName}
        aria-label={label}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={() => setOpen((value) => !value)}
      >
        {trigger}
      </button>
      {open && (
        <ul ref={listRef} id={menuId} role="menu" aria-label={label} className={`menu-list menu-${align}`} onKeyDown={onKeyDown}>
          {items.map((item, index) =>
            item === 'separator' ? (
              <li key={`separator-${index}`} role="separator" className="menu-separator" />
            ) : (
              <li key={item.label} role="none">
                <button
                  type="button"
                  role="menuitem"
                  disabled={item.disabled}
                  className={item.danger ? 'menu-item menu-item-danger' : 'menu-item'}
                  onClick={() => {
                    close(true);
                    item.onSelect();
                  }}
                >
                  <span>{item.label}</span>
                  {item.hint && <kbd>{item.hint}</kbd>}
                </button>
              </li>
            ),
          )}
        </ul>
      )}
    </div>
  );
}
