'use client';

import { useEffect, useId, useRef, type ReactNode } from 'react';
import { Icon } from './Icon';

export interface DialogProps {
  open: boolean;
  title: string;
  onClose: () => void;
  children: ReactNode;
  /** Buttons along the bottom. */
  actions?: ReactNode;
  wide?: boolean;
}

/**
 * A modal on the native <dialog>: the browser traps focus, closes on Escape, restores focus to
 * whatever opened it, and makes the rest of the page inert.
 */
export function Dialog({ open, title, onClose, children, actions, wide }: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    if (open && !dialog.open) {
      if (typeof dialog.showModal === 'function') dialog.showModal();
      else dialog.setAttribute('open', '');
    } else if (!open && dialog.open) {
      if (typeof dialog.close === 'function') dialog.close();
      else dialog.removeAttribute('open');
    }
  }, [open]);

  return (
    <dialog
      ref={ref}
      className={`dialog${wide ? ' dialog-wide' : ''}`}
      aria-labelledby={titleId}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClick={(event) => {
        // A click on the backdrop lands on the dialog element itself.
        if (event.target === ref.current) onClose();
      }}
    >
      {open && (
        <div className="dialog-body">
          <header className="dialog-header">
            <h2 id={titleId}>{title}</h2>
            <button type="button" className="btn btn-ghost btn-icon btn-sm" onClick={onClose} aria-label="Close">
              <Icon name="close" />
            </button>
          </header>
          <div className="dialog-content">{children}</div>
          {actions && <footer className="dialog-actions">{actions}</footer>}
        </div>
      )}
    </dialog>
  );
}
