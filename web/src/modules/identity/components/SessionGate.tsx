'use client';

import { usePathname, useRouter } from 'next/navigation';
import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { EmptyState, PageLoading } from '@/shared/ui/feedback';
import { useAppDispatch, useAppSelector } from '@/store/hooks';
import { selectSessionStatus } from '../sessionSlice';
import { refreshAccessToken } from '../tokens';

/**
 * Renders its children only for a signed-in person. On a fresh page load the access token is not
 * in memory yet, so it is restored from the refresh cookie first.
 */
export function SessionGate({ children }: { children: ReactNode }) {
  const status = useAppSelector(selectSessionStatus);
  const dispatch = useAppDispatch();
  const router = useRouter();
  const pathname = usePathname();
  const [unreachable, setUnreachable] = useState(false);

  const restore = useCallback(async () => {
    setUnreachable(false);
    const token = await refreshAccessToken(dispatch);
    if (token === null) setUnreachable(true);
  }, [dispatch]);

  useEffect(() => {
    if (status === 'unknown') void restore();
  }, [status, restore]);

  useEffect(() => {
    if (status === 'signed-out') router.replace(`/sign-in?next=${encodeURIComponent(pathname)}`);
  }, [status, router, pathname]);

  if (status === 'signed-in') return children;

  if (status === 'unknown' && unreachable) {
    return (
      <EmptyState
        title="The server cannot be reached"
        action={
          <button type="button" className="btn" onClick={() => void restore()}>
            Try again
          </button>
        }
      >
        Check your connection. Your boards are safe.
      </EmptyState>
    );
  }

  return <PageLoading label="Signing you in" />;
}

/** Only same-site paths are followed after signing in, so a crafted link cannot send you elsewhere. */
export function safeNext(next: string | null, fallback = '/teams'): string {
  return next && next.startsWith('/') && !next.startsWith('//') && !next.startsWith('/\\') ? next : fallback;
}
