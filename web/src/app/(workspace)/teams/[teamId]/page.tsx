'use client';

import Link from 'next/link';
import { useParams } from 'next/navigation';
import { BoardDashboard } from '@/modules/board-modelling/catalog/BoardDashboard';
import { AppHeader } from '@/modules/identity/components/AppHeader';
import { roleLabels, useGetTeamQuery } from '@/modules/teams/teamsApi';
import { toProblem } from '@/shared/api/problems';
import { PageLoading } from '@/shared/ui/feedback';
import { FormProblem } from '@/shared/ui/forms';
import { Icon } from '@/shared/ui/Icon';

export default function TeamPage() {
  const { teamId } = useParams<{ teamId: string }>();
  const { data: team, isLoading, error } = useGetTeamQuery(teamId);

  return (
    <>
      <AppHeader>
        <Link href="/teams">Teams</Link>
        {team && <span aria-current="page">{team.name}</span>}
      </AppHeader>
      <main className="page">
        {isLoading ? (
          <PageLoading label="Loading the team" />
        ) : !team ? (
          <FormProblem problem={toProblem(error)} />
        ) : (
          <>
            <div className="page-header">
              <div>
                <h1>{team.name}</h1>
                <p className="muted">
                  Your role: {roleLabels[team.myRole]} · {team.members.length}{' '}
                  {team.members.length === 1 ? 'member' : 'members'}
                </p>
              </div>
              <span className="spacer" />
              <Link className="btn" href={`/teams/${team.id}/settings`}>
                <Icon name="settings" />
                {team.myRole === 'owner' ? 'Members & API keys' : 'Members'}
              </Link>
            </div>
            <BoardDashboard teamId={team.id} />
          </>
        )}
      </main>
    </>
  );
}
