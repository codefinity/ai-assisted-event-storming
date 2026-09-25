import { api } from '@/shared/api/baseApi';

export type TeamRole = 'owner' | 'editor' | 'viewer';

export const teamRoles: readonly TeamRole[] = ['owner', 'editor', 'viewer'];

export const roleLabels: Record<TeamRole, string> = { owner: 'Owner', editor: 'Editor', viewer: 'Viewer' };

export interface TeamSummary {
  id: string;
  name: string;
  myRole: TeamRole;
  memberCount: number;
}

export interface TeamMember {
  accountId: string;
  displayName: string;
  email: string;
  role: TeamRole;
  joinedAt: string;
}

export interface Team {
  id: string;
  name: string;
  myRole: TeamRole;
  members: TeamMember[];
}

export interface Invitation {
  id: string;
  kind: 'link' | 'email';
  email: string | null;
  role: TeamRole;
  createdAt: string;
  expiresAt: string;
}

export interface CreatedInvitation {
  invitation: Invitation;
  /** Goes in the link /invitations/{token}. It is shown this once. */
  token: string;
}

export interface InvitationPreview {
  teamName: string;
  invitedBy: string;
  role: TeamRole;
  kind: 'link' | 'email';
  email: string | null;
  expiresAt: string;
  status: 'valid' | 'expired' | 'revoked' | 'used';
}

export const teamsApi = api.injectEndpoints({
  endpoints: (build) => ({
    listMyTeams: build.query<TeamSummary[], void>({
      query: () => '/teams',
      providesTags: ['Teams'],
    }),
    createTeam: build.mutation<TeamSummary, { name: string }>({
      query: (body) => ({ url: '/teams', method: 'POST', body }),
      invalidatesTags: ['Teams'],
    }),
    getTeam: build.query<Team, string>({
      query: (teamId) => `/teams/${teamId}`,
      providesTags: (_result, _error, teamId) => [{ type: 'Team', id: teamId }],
    }),
    listInvitations: build.query<Invitation[], string>({
      query: (teamId) => `/teams/${teamId}/invitations`,
      providesTags: (_result, _error, teamId) => [{ type: 'Invitations', id: teamId }],
    }),
    createInvitation: build.mutation<CreatedInvitation, { teamId: string; role: TeamRole; email?: string }>({
      query: ({ teamId, ...body }) => ({ url: `/teams/${teamId}/invitations`, method: 'POST', body }),
      invalidatesTags: (_result, _error, { teamId }) => [{ type: 'Invitations', id: teamId }],
    }),
    revokeInvitation: build.mutation<void, { teamId: string; invitationId: string }>({
      query: ({ teamId, invitationId }) => ({ url: `/teams/${teamId}/invitations/${invitationId}`, method: 'DELETE' }),
      invalidatesTags: (_result, _error, { teamId }) => [{ type: 'Invitations', id: teamId }],
    }),
    changeMemberRole: build.mutation<void, { teamId: string; accountId: string; role: TeamRole }>({
      query: ({ teamId, accountId, role }) => ({ url: `/teams/${teamId}/members/${accountId}`, method: 'PATCH', body: { role } }),
      invalidatesTags: (_result, _error, { teamId }) => [{ type: 'Team', id: teamId }, 'Teams'],
    }),
    removeMember: build.mutation<void, { teamId: string; accountId: string }>({
      query: ({ teamId, accountId }) => ({ url: `/teams/${teamId}/members/${accountId}`, method: 'DELETE' }),
      invalidatesTags: (_result, _error, { teamId }) => [{ type: 'Team', id: teamId }, 'Teams'],
    }),
    previewInvitation: build.query<InvitationPreview, string>({
      query: (token) => ({ url: `/invitations/${encodeURIComponent(token)}`, anonymous: true }),
    }),
    acceptInvitation: build.mutation<TeamSummary, string>({
      query: (token) => ({ url: `/invitations/${encodeURIComponent(token)}/accept`, method: 'POST' }),
      invalidatesTags: ['Teams'],
    }),
  }),
});

export const {
  useListMyTeamsQuery,
  useCreateTeamMutation,
  useGetTeamQuery,
  useListInvitationsQuery,
  useCreateInvitationMutation,
  useRevokeInvitationMutation,
  useChangeMemberRoleMutation,
  useRemoveMemberMutation,
  usePreviewInvitationQuery,
  useAcceptInvitationMutation,
} = teamsApi;
