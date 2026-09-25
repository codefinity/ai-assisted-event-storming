import { api } from '@/shared/api/baseApi';

export type ApiScope = 'read' | 'write';

export interface ApiKey {
  id: string;
  name: string;
  /** The start of the key, enough to recognise it. */
  prefix: string;
  scopes: ApiScope[];
  createdAt: string;
  expiresAt: string | null;
  lastUsedAt: string | null;
  revokedAt: string | null;
}

export interface CreatedApiKey {
  apiKey: ApiKey;
  /** The full key. It is shown this once and cannot be retrieved again. */
  key: string;
}

export const apiKeysApi = api.injectEndpoints({
  endpoints: (build) => ({
    listApiKeys: build.query<ApiKey[], string>({
      query: (teamId) => `/teams/${teamId}/api-keys`,
      providesTags: (_result, _error, teamId) => [{ type: 'ApiKeys', id: teamId }],
    }),
    createApiKey: build.mutation<CreatedApiKey, { teamId: string; name: string; scopes: ApiScope[]; expiresAt?: string }>({
      query: ({ teamId, ...body }) => ({ url: `/teams/${teamId}/api-keys`, method: 'POST', body }),
      invalidatesTags: (_result, _error, { teamId }) => [{ type: 'ApiKeys', id: teamId }],
    }),
    revokeApiKey: build.mutation<ApiKey, { teamId: string; keyId: string }>({
      query: ({ teamId, keyId }) => ({ url: `/teams/${teamId}/api-keys/${keyId}`, method: 'DELETE' }),
      invalidatesTags: (_result, _error, { teamId }) => [{ type: 'ApiKeys', id: teamId }],
    }),
  }),
});

export const { useListApiKeysQuery, useCreateApiKeyMutation, useRevokeApiKeyMutation } = apiKeysApi;
