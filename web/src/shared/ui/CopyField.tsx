'use client';

import { useId, useState } from 'react';
import { Icon } from './Icon';

/** A read-only value with a copy button: invitation links, API keys. */
export function CopyField({ label, value, hint }: { label: string; value: string; hint?: string }) {
  const id = useId();
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      (document.getElementById(id) as HTMLInputElement | null)?.select();
    }
  };

  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <div className="row">
        <input id={id} className="input copy-input" readOnly value={value} onFocus={(event) => event.target.select()} />
        <button type="button" className="btn" onClick={() => void copy()}>
          <Icon name="copy" />
          {copied ? 'Copied' : 'Copy'}
        </button>
      </div>
      {hint && <span className="hint">{hint}</span>}
      <span className="visually-hidden" aria-live="polite">
        {copied ? 'Copied to the clipboard' : ''}
      </span>
    </div>
  );
}
