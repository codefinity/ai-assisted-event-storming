'use client';

import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { useEffect, useState, type FormEvent } from 'react';
import { toProblem } from '@/shared/api/problems';
import { FormProblem, TextField } from '@/shared/ui/forms';
import { useAppDispatch, useAppSelector } from '@/store/hooks';
import { useSignInMutation, useSignUpMutation } from '../identityApi';
import { selectSessionStatus } from '../sessionSlice';
import { refreshAccessToken } from '../tokens';
import { safeNext } from './SessionGate';

function useLeaveWhenSignedIn(next: string) {
  const status = useAppSelector(selectSessionStatus);
  const dispatch = useAppDispatch();
  const router = useRouter();

  // Someone who still has a session does not need to sign in again.
  useEffect(() => {
    if (status === 'unknown') void refreshAccessToken(dispatch);
  }, [status, dispatch]);

  useEffect(() => {
    if (status === 'signed-in') router.replace(next);
  }, [status, next, router]);
}

function AuthShell({ title, children, footer }: { title: string; children: React.ReactNode; footer: React.ReactNode }) {
  return (
    <main className="auth-page">
      <div className="auth-brand" aria-hidden="true">
        <span className="auth-sticky" style={{ background: '#FFA94D' }} />
        <span className="auth-sticky" style={{ background: '#74C0FC' }} />
        <span className="auth-sticky" style={{ background: '#FFE066' }} />
      </div>
      <section className="auth-card card">
        <h1>{title}</h1>
        {children}
      </section>
      <p className="auth-footer">{footer}</p>
    </main>
  );
}

export function SignInForm() {
  const params = useSearchParams();
  const next = safeNext(params.get('next'));
  useLeaveWhenSignedIn(next);
  const [signIn, { isLoading, error }] = useSignInMutation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const problem = toProblem(error);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    await signIn({ email, password }).unwrap().catch(() => undefined);
  };

  return (
    <AuthShell
      title="Sign in"
      footer={
        <>
          New here? <Link href={`/sign-up${next !== '/teams' ? `?next=${encodeURIComponent(next)}` : ''}`}>Create an account</Link>
        </>
      }
    >
      <form className="stack" onSubmit={submit} noValidate>
        <FormProblem problem={problem} fields={['email', 'password']} />
        <TextField label="Email" name="email" type="email" autoComplete="email" required value={email} onChange={(event) => setEmail(event.target.value)} problem={problem} autoFocus />
        <TextField
          label="Password"
          name="password"
          type="password"
          autoComplete="current-password"
          required
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          problem={problem}
        />
        <button type="submit" className="btn btn-primary" disabled={isLoading}>
          {isLoading ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </AuthShell>
  );
}

export function SignUpForm() {
  const params = useSearchParams();
  const next = safeNext(params.get('next'));
  useLeaveWhenSignedIn(next);
  const [signUp, { isLoading, error }] = useSignUpMutation();
  const [email, setEmail] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const problem = toProblem(error);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    await signUp({ email, displayName, password }).unwrap().catch(() => undefined);
  };

  return (
    <AuthShell
      title="Create your account"
      footer={
        <>
          Already have one? <Link href={`/sign-in${next !== '/teams' ? `?next=${encodeURIComponent(next)}` : ''}`}>Sign in</Link>
        </>
      }
    >
      <form className="stack" onSubmit={submit} noValidate>
        <FormProblem problem={problem} fields={['email', 'displayName', 'password']} />
        <TextField label="Your name" name="displayName" autoComplete="name" required value={displayName} onChange={(event) => setDisplayName(event.target.value)} problem={problem} autoFocus hint="Shown to your team on boards." />
        <TextField label="Email" name="email" type="email" autoComplete="email" required value={email} onChange={(event) => setEmail(event.target.value)} problem={problem} />
        <TextField
          label="Password"
          name="password"
          type="password"
          autoComplete="new-password"
          required
          minLength={8}
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          problem={problem}
          hint="At least 8 characters."
        />
        <button type="submit" className="btn btn-primary" disabled={isLoading}>
          {isLoading ? 'Creating account…' : 'Create account'}
        </button>
      </form>
    </AuthShell>
  );
}
