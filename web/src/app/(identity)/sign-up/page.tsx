import type { Metadata } from 'next';
import { Suspense } from 'react';
import { SignUpForm } from '@/modules/identity/components/AuthForms';

export const metadata: Metadata = { title: 'Create an account' };

export default function SignUpPage() {
  return (
    <Suspense>
      <SignUpForm />
    </Suspense>
  );
}
