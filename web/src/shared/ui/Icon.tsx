import type { SVGProps } from 'react';

// A small stroke icon set. The element registry names icons (zap, send, user, ...); anything it
// names that is not here falls back to a plain square, so a new type never breaks the page.

const paths: Record<string, string> = {
  zap: 'M13 2 4 14h7l-1 8 9-12h-7l1-8z',
  send: 'M22 2 11 13M22 2l-7 20-4-9-9-4 20-7z',
  user: 'M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8z',
  workflow: 'M3 3h7v7H3zM14 14h7v7h-7zM6.5 10v4a3 3 0 0 0 3 3H14',
  table: 'M3 3h18v18H3zM3 9h18M3 15h18M9 3v18',
  plug: 'M12 22v-5M9 8V2M15 8V2M6 8h12v4a6 6 0 0 1-12 0V8z',
  box: 'M21 8 12 3 3 8v8l9 5 9-5V8zM3 8l9 5 9-5M12 13v8',
  alert: 'M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0zM12 9v4M12 17h.01',
  lightbulb: 'M9 18h6M10 22h4M12 2a7 7 0 0 0-4 12.7V17h8v-2.3A7 7 0 0 0 12 2z',
  rows: 'M3 4h18v6H3zM3 14h18v6H3z',
  frame: 'M4 4h16v16H4z M8 4v-2 M4 8H2',
  square: 'M4 4h16v16H4z',
  undo: 'M9 14 4 9l5-5M4 9h11a5 5 0 0 1 0 10h-3',
  redo: 'm15 14 5-5-5-5M20 9H9a5 5 0 0 0 0 10h3',
  search: 'M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16zM21 21l-4.3-4.3',
  help: 'M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20zM9.1 9a3 3 0 0 1 5.8 1c0 2-3 3-3 3M12 17h.01',
  legend: 'M4 6h.01M4 12h.01M4 18h.01M8 6h12M8 12h12M8 18h12',
  plus: 'M12 5v14M5 12h14',
  minus: 'M5 12h14',
  trash: 'M3 6h18M8 6V4h8v2M19 6l-1 14H6L5 6',
  arrow: 'M5 12h14M13 6l6 6-6 6',
  flag: 'M4 22V4M4 4h13l-2 4 2 4H4',
  download: 'M12 3v12M7 10l5 5 5-5M5 21h14',
  upload: 'M12 21V9M7 14l5-5 5 5M5 3h14',
  close: 'M18 6 6 18M6 6l12 12',
  chevronDown: 'm6 9 6 6 6-6',
  chevronLeft: 'm15 18-6-6 6-6',
  more: 'M5 12h.01M12 12h.01M19 12h.01',
  fit: 'M3 9V3h6M21 9V3h-6M3 15v6h6M21 15v6h-6',
  copy: 'M9 9h11v11H9zM5 15H4V4h11v1',
  palette: 'M12 22a10 10 0 1 1 10-10c0 3-2 4-4 4h-2a2 2 0 0 0-1 3.7A2 2 0 0 1 12 22zM7.5 10.5h.01M12 7.5h.01M16.5 10.5h.01',
  settings: 'M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 0 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 0 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 0 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 0 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z',
  link: 'M10 13a5 5 0 0 0 7.5.5l3-3a5 5 0 0 0-7-7l-1.8 1.7M14 11a5 5 0 0 0-7.5-.5l-3 3a5 5 0 0 0 7 7l1.7-1.7',
  key: 'M21 2l-2 2m-7.6 7.6a5.5 5.5 0 1 1-7.8 7.8 5.5 5.5 0 0 1 7.8-7.8zm0 0L15.5 7.5m0 0 3 3L22 7l-3-3m-3.5 3.5L19 4',
  signal: 'M2 20h.01M7 20v-4M12 20v-8M17 20V8M22 4v16',
};

export interface IconProps extends SVGProps<SVGSVGElement> {
  name: string;
  size?: number;
  /** Icons are decorative unless given a label. */
  label?: string;
}

export function Icon({ name, size = 16, label, ...rest }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden={label ? undefined : true}
      role={label ? 'img' : undefined}
      aria-label={label}
      focusable="false"
      {...rest}
    >
      <path d={paths[name] ?? paths.square} />
    </svg>
  );
}
