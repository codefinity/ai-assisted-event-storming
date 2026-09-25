import Link from 'next/link';

export default function NotFound() {
  return (
    <main className="auth-page">
      <section className="auth-card card stack">
        <h1>Page not found</h1>
        <p className="muted">There is nothing at this address.</p>
        <Link href="/teams">Go to your teams</Link>
      </section>
    </main>
  );
}
