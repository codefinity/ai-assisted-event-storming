'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import type { ReactNode } from 'react';
import { Menu } from '@/shared/ui/Menu';
import { useAppSelector } from '@/store/hooks';
import { useSignOutMutation } from '../identityApi';
import { selectAccount } from '../sessionSlice';

export function Initials({ name, color }: { name: string; color?: string }) {
  const initials = name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]!.toUpperCase())
    .join('');
  return (
    <span className="avatar" style={color ? { background: color } : undefined} aria-hidden="true">
      {initials || '?'}
    </span>
  );
}

export function AppHeader({ children }: { children?: ReactNode }) {
  const account = useAppSelector(selectAccount);
  const [signOut] = useSignOutMutation();
  const router = useRouter();

  return (
    <header className="app-header">
      <Link href="/teams" className="app-brand">
        <span className="app-brand-mark" aria-hidden="true" />
        EventStorming
      </Link>
      <nav aria-label="Breadcrumb" className="app-crumbs">
        {children}
      </nav>
      <span className="spacer" />
      {account && (
        <Menu
          label={`Account: ${account.displayName}`}
          triggerClassName="btn btn-ghost"
          trigger={
            <>
              <Initials name={account.displayName} />
              <span className="app-account-name">{account.displayName}</span>
            </>
          }
          items={[
            { label: account.email, onSelect: () => undefined, disabled: true },
            'separator',
            {
              label: 'Sign out',
              onSelect: () => {
                void signOut()
                  .unwrap()
                  .catch(() => undefined)
                  .finally(() => router.replace('/sign-in'));
              },
            },
          ]}
        />
      )}
    </header>
  );
}
