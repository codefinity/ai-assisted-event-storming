import type { Metadata, Viewport } from 'next';
import type { ReactNode } from 'react';
import { StoreProvider } from '@/store/StoreProvider';
import { Notices } from '@/shared/ui/Notices';
import './globals.css';
import '@/shared/ui/ui.css';
import './pages.css';

export const metadata: Metadata = {
  title: { default: 'EventStorming', template: '%s · EventStorming' },
  description: 'Collaborative, real-time EventStorming boards.',
};

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  themeColor: '#f7f7f5',
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body>
        <StoreProvider>
          {children}
          <Notices />
        </StoreProvider>
      </body>
    </html>
  );
}
