import type { ReactNode } from 'react';

export function Spinner({ label = 'Loading' }: { label?: string }) {
  return (
    <span className="spinner" role="status">
      <span className="spinner-ring" aria-hidden="true" />
      <span className="visually-hidden">{label}</span>
    </span>
  );
}

export function PageLoading({ label = 'Loading' }: { label?: string }) {
  return (
    <div className="page-loading">
      <Spinner label={label} />
    </div>
  );
}

export function EmptyState({ title, children, action }: { title: string; children?: ReactNode; action?: ReactNode }) {
  return (
    <div className="empty-state">
      <h2>{title}</h2>
      {children && <p className="muted">{children}</p>}
      {action}
    </div>
  );
}

export function relativeTime(iso: string, now = Date.now()): string {
  const seconds = Math.round((now - Date.parse(iso)) / 1000);
  const format = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' });
  const steps: Array<[Intl.RelativeTimeFormatUnit, number]> = [
    ['year', 31_536_000],
    ['month', 2_592_000],
    ['week', 604_800],
    ['day', 86_400],
    ['hour', 3600],
    ['minute', 60],
  ];
  for (const [unit, size] of steps) {
    if (Math.abs(seconds) >= size) return format.format(-Math.round(seconds / size), unit);
  }

  return 'just now';
}
