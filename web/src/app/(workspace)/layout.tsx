import type { ReactNode } from 'react';
import { SessionGate } from '@/modules/identity/components/SessionGate';

export default function WorkspaceLayout({ children }: { children: ReactNode }) {
  return <SessionGate>{children}</SessionGate>;
}
