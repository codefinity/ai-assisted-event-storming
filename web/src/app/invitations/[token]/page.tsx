'use client';

import { useParams } from 'next/navigation';
import { InvitationAccept } from '@/modules/teams/components/InvitationAccept';

export default function InvitationPage() {
  const { token } = useParams<{ token: string }>();
  return <InvitationAccept token={token} />;
}
