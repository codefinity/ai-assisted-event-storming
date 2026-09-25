'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { toProblem } from '@/shared/api/problems';
import { EmptyState, PageLoading } from '@/shared/ui/feedback';
import { FormProblem, TextField } from '@/shared/ui/forms';
import { roleLabels, useCreateTeamMutation, useListMyTeamsQuery } from '../teamsApi';

function CreateTeamForm({ first }: { first: boolean }) {
  const [createTeam, { isLoading, error }] = useCreateTeamMutation();
  const [name, setName] = useState('');
  const router = useRouter();
  const problem = toProblem(error);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const team = await createTeam({ name }).unwrap().catch(() => null);
    if (team) {
      setName('');
      router.push(`/teams/${team.id}`);
    }
  };

  return (
    <form className="inline-form" onSubmit={submit} noValidate>
      <FormProblem problem={problem} fields={['name']} />
      <div className="row row-end">
        <TextField
          label={first ? 'Name your first team' : 'New team'}
          name="name"
          value={name}
          onChange={(event) => setName(event.target.value)}
          placeholder="e.g. Food delivery"
          problem={problem}
          required
        />
        <button type="submit" className="btn btn-primary" disabled={isLoading || name.trim() === ''}>
          Create team
        </button>
      </div>
    </form>
  );
}

export function TeamList() {
  const { data: teams, isLoading, error } = useListMyTeamsQuery();

  if (isLoading) return <PageLoading label="Loading your teams" />;
  if (error) return <FormProblem problem={toProblem(error)} />;

  return (
    <main className="page">
      <div className="page-header">
        <h1>Your teams</h1>
      </div>
      {teams && teams.length === 0 ? (
        <EmptyState title="You are not in a team yet">
          Boards belong to teams. Create one, or open an invitation link someone sent you.
        </EmptyState>
      ) : (
        <ul className="card-grid" aria-label="Teams">
          {teams?.map((team) => (
            <li key={team.id}>
              <Link href={`/teams/${team.id}`} className="card team-card">
                <h2>{team.name}</h2>
                <p className="muted">
                  {roleLabels[team.myRole]} · {team.memberCount} {team.memberCount === 1 ? 'member' : 'members'}
                </p>
              </Link>
            </li>
          ))}
        </ul>
      )}
      <section className="card section" aria-label="Create a team">
        <CreateTeamForm first={teams?.length === 0} />
      </section>
    </main>
  );
}
