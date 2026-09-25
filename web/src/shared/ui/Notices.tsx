'use client';

import { useEffect } from 'react';
import { useAppDispatch, useAppSelector } from '@/store/hooks';
import { Icon } from './Icon';
import { noticeDismissed, selectNotices, type Notice } from './noticesSlice';

const lifetime = { info: 5000, error: 8000 };

function NoticeItem({ notice }: { notice: Notice }) {
  const dispatch = useAppDispatch();

  useEffect(() => {
    const timer = setTimeout(() => dispatch(noticeDismissed(notice.id)), lifetime[notice.tone]);
    return () => clearTimeout(timer);
  }, [dispatch, notice.id, notice.tone]);

  return (
    <li className={`notice notice-${notice.tone}`}>
      <div>
        <p>{notice.text}</p>
        {notice.fix && <p className="notice-fix">{notice.fix}</p>}
      </div>
      <button type="button" className="btn btn-ghost btn-icon btn-sm" aria-label="Dismiss" onClick={() => dispatch(noticeDismissed(notice.id))}>
        <Icon name="close" size={14} />
      </button>
    </li>
  );
}

/** Toasts, announced politely to screen readers (errors assertively). */
export function Notices() {
  const notices = useAppSelector(selectNotices);
  return (
    <>
      <ol className="notices" aria-live="polite" aria-relevant="additions">
        {notices
          .filter((notice) => notice.tone === 'info')
          .map((notice) => (
            <NoticeItem key={notice.id} notice={notice} />
          ))}
      </ol>
      <ol className="notices notices-errors" aria-live="assertive" aria-relevant="additions">
        {notices
          .filter((notice) => notice.tone === 'error')
          .map((notice) => (
            <NoticeItem key={notice.id} notice={notice} />
          ))}
      </ol>
    </>
  );
}
