/** Fails fast, with the fix, when the stack is not running. */
export default async function globalSetup() {
  const web = process.env.E2E_BASE_URL ?? 'http://localhost:3000';
  const api = process.env.E2E_API_URL ?? 'http://localhost:5080';
  for (const [name, url] of [
    ['API', `${api}/health`],
    ['web app', `${web}/sign-in`],
  ] as const) {
    try {
      const response = await fetch(url);
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
    } catch (error) {
      throw new Error(`The ${name} is not reachable at ${url} (${String(error)}). Start the stack first: docker compose up`);
    }
  }
}
