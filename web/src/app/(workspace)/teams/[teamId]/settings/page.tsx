'use client';

import Link from 'next/link';
import { useParams } from 'next/navigation';
import { AppHeader } from '@/modules/identity/components/AppHeader';
import { ApiKeysSection } from '@/modules/public-integration/components/ApiKeysSection';
import { InvitationsSection, MembersSection } from '@/modules/teams/components/TeamSettings';
import { useGetTeamQuery } from '@/modules/teams/teamsApi';
import { toProblem } from '@/shared/api/problems';
import { PageLoading } from '@/shared/ui/feedback';
import { FormProblem } from '@/shared/ui/forms';

export default function TeamSettingsPage() {
  const { teamId } = useParams<{ teamId: string }>();
  const { data: team, isLoading, error } = useGetTeamQuery(teamId);

  return (
    <>
      <AppHeader>
        <Link href="/teams">Teams</Link>
        {team && <Link href={`/teams/${team.id}`}>{team.name}</Link>}
        <span aria-current="page">Settings</span>
      </AppHeader>
      <main className="page stack">
        {isLoading ? (
          <PageLoading label="Loading the team" />
        ) : !team ? (
          <FormProblem problem={toProblem(error)} />
        ) : (
          <>
            <h1>{team.name}: settings</h1>
            <MembersSection team={team} />
            {team.myRole === 'owner' && (
              <>
                <InvitationsSection team={team} />
                <ApiKeysSection teamId={team.id} />
              </>
            )}
          </>
        )}
      </main>
    </>
  );
}
