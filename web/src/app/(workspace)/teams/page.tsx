'use client';

import { AppHeader } from '@/modules/identity/components/AppHeader';
import { TeamList } from '@/modules/teams/components/TeamList';

export default function TeamsPage() {
  return (
    <>
      <AppHeader />
      <TeamList />
    </>
  );
}
