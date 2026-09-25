import { redirect } from 'next/navigation';

/** Signed-in people land on their teams; the session gate there sends everyone else to sign in. */
export default function Home() {
  redirect('/teams');
}
