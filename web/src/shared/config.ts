/** Where the browser reaches the API. Inlined into the bundle at build time. */
export const apiBaseUrl = (process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5080').replace(/\/+$/, '');

export const appApiUrl = `${apiBaseUrl}/api/app`;

export const boardHubUrl = `${apiBaseUrl}/hubs/board`;
