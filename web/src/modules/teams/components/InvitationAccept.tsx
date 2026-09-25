'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useEffect } from 'react';
import { toProblem } from '@/shared/api/problems';
import { PageLoading } from '@/shared/ui/feedback';
import { FormProblem } from '@/shared/ui/forms';
import { useAppDispatch, useAppSelector } from '@/store/hooks';
import { selectAccount, selectSessionStatus } from '@/modules/identity/sessionSlice';
import { refreshAccessToken } from '@/modules/identity/tokens';
import { roleLabels, useAcceptInvitationMutation, usePreviewInvitationQuery } from '../teamsApi';

const closedReasons = {
  expired: 'This invitation has expired. Ask the team for a new one.',
  revoked: 'This invitation was revoked. Ask the team for a new one.',
  used: 'This invitation has already been used.',
} as const;

export function InvitationAccept({ token }: { token: string }) {
  const status = useAppSelector(selectSessionStatus);
  const account = useAppSelector(selectAccount);
  const dispatch = useAppDispatch();
  const router = useRouter();
  const preview = usePreviewInvitationQuery(token);
  const [accept, accepted] = useAcceptInvitationMutation();

  // Works signed out: find out quietly whether there is a session to accept with.
  useEffect(() => {
    if (status === 'unknown') void refreshAccessToken(dispatch);
  }, [status, dispatch]);

  if (preview.isLoading) return <PageLoading label="Opening the invitation" />;

  const invitation = preview.data;
  const next = encodeURIComponent(`/invitations/${token}`);

  return (
    <main className="auth-page">
      <section className="auth-card card stack">
        {!invitation ? (
          <>
            <h1>Invitation not found</h1>
            <FormProblem problem={toProblem(preview.error)} />
            <Link href="/teams">Go to your teams</Link>
          </>
        ) : (
          <>
            <h1>Join {invitation.teamName}</h1>
            <p>
              {invitation.invitedBy} invited {invitation.email ?? 'you'} to join as <strong>{roleLabels[invitation.role]}</strong>.
            </p>
            {invitation.status !== 'valid' ? (
              <p className="error-text">{closedReasons[invitation.status]}</p>
            ) : status === 'signed-in' ? (
              <>
                <FormProblem problem={toProblem(accepted.error)} />
                <button
                  type="button"
                  className="btn btn-primary"
                  disabled={accepted.isLoading}
                  onClick={async () => {
                    const team = await accept(token).unwrap().catch(() => null);
                    if (team) router.replace(`/teams/${team.id}`);
                  }}
                >
                  Join as {account?.displayName}
                </button>
              </>
            ) : status === 'signed-out' ? (
              <div className="row">
                <Link className="btn btn-primary" href={`/sign-in?next=${next}`}>
                  Sign in to accept
                </Link>
                <Link className="btn" href={`/sign-up?next=${next}`}>
                  Create an account
                </Link>
              </div>
            ) : (
              <PageLoading />
            )}
          </>
        )}
      </section>
    </main>
  );
}
