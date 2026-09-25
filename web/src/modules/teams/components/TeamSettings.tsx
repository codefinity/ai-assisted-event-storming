'use client';

import { useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { toProblem } from '@/shared/api/problems';
import { CopyField } from '@/shared/ui/CopyField';
import { relativeTime } from '@/shared/ui/feedback';
import { FormProblem, SelectField, TextField } from '@/shared/ui/forms';
import { useAppSelector } from '@/store/hooks';
import { selectAccount } from '@/modules/identity/sessionSlice';
import {
  roleLabels,
  teamRoles,
  useChangeMemberRoleMutation,
  useCreateInvitationMutation,
  useListInvitationsQuery,
  useRemoveMemberMutation,
  useRevokeInvitationMutation,
  type Team,
  type TeamRole,
} from '../teamsApi';

const roleOptions = teamRoles.map((role) => ({ value: role, label: roleLabels[role] }));

export const roleHelp: Record<TeamRole, string> = {
  owner: 'Everything, including members, invitations and API keys.',
  editor: 'Create and change boards.',
  viewer: 'Open boards and follow along, without changing them.',
};

export function MembersSection({ team }: { team: Team }) {
  const me = useAppSelector(selectAccount);
  const router = useRouter();
  const [changeRole, changing] = useChangeMemberRoleMutation();
  const [removeMember, removing] = useRemoveMemberMutation();
  const problem = toProblem(changing.error ?? removing.error);
  const isOwner = team.myRole === 'owner';

  const remove = async (accountId: string, self: boolean) => {
    const question = self ? `Leave ${team.name}? You will lose access to its boards.` : 'Remove this member from the team?';
    if (!window.confirm(question)) return;
    const done = await removeMember({ teamId: team.id, accountId }).unwrap().then(() => true, () => false);
    if (done && self) router.replace('/teams');
  };

  return (
    <section className="card section" aria-labelledby="members-heading">
      <h2 id="members-heading">Members</h2>
      <FormProblem problem={problem} />
      <div className="table-wrap">
        <table className="table">
          <thead>
            <tr>
              <th scope="col">Name</th>
              <th scope="col">Email</th>
              <th scope="col">Role</th>
              <th scope="col">Joined</th>
              <th scope="col">
                <span className="visually-hidden">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {team.members.map((member) => {
              const self = member.accountId === me?.id;
              return (
                <tr key={member.accountId}>
                  <td>
                    {member.displayName}
                    {self && <span className="badge">You</span>}
                  </td>
                  <td>{member.email}</td>
                  <td>
                    {isOwner ? (
                      <select
                        className="select select-sm"
                        aria-label={`Role of ${member.displayName}`}
                        value={member.role}
                        onChange={(event) => void changeRole({ teamId: team.id, accountId: member.accountId, role: event.target.value as TeamRole })}
                      >
                        {roleOptions.map((option) => (
                          <option key={option.value} value={option.value}>
                            {option.label}
                          </option>
                        ))}
                      </select>
                    ) : (
                      roleLabels[member.role]
                    )}
                  </td>
                  <td>{relativeTime(member.joinedAt)}</td>
                  <td className="cell-actions">
                    {(isOwner || self) && (
                      <button type="button" className="btn btn-sm btn-danger" onClick={() => void remove(member.accountId, self)}>
                        {self ? 'Leave team' : 'Remove'}
                      </button>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}

export function InvitationsSection({ team }: { team: Team }) {
  const { data: invitations } = useListInvitationsQuery(team.id);
  const [createInvitation, created] = useCreateInvitationMutation();
  const [revokeInvitation] = useRevokeInvitationMutation();
  const [role, setRole] = useState<TeamRole>('editor');
  const [email, setEmail] = useState('');
  const [link, setLink] = useState<string | null>(null);
  const problem = toProblem(created.error);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const result = await createInvitation({ teamId: team.id, role, email: email.trim() || undefined }).unwrap().catch(() => null);
    if (result) {
      setLink(`${window.location.origin}/invitations/${result.token}`);
      setEmail('');
    }
  };

  return (
    <section className="card section" aria-labelledby="invitations-heading">
      <h2 id="invitations-heading">Invite people</h2>
      <p className="muted">Leave the email empty for a link anyone can use once. With an email, we send the link and only that person can accept it.</p>
      <form className="stack" onSubmit={submit} noValidate>
        <FormProblem problem={problem} fields={['email', 'role']} />
        <div className="row row-end row-wrap">
          <TextField label="Email (optional)" name="email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} problem={problem} placeholder="name@example.com" />
          <SelectField label="Role" name="role" value={role} onChange={(event) => setRole(event.target.value as TeamRole)} options={roleOptions} problem={problem} hint={roleHelp[role]} />
          <button type="submit" className="btn btn-primary" disabled={created.isLoading}>
            {email.trim() ? 'Send invitation' : 'Create link'}
          </button>
        </div>
      </form>
      {link && <CopyField label="Invitation link" value={link} hint="Shown once. Anyone with this link can join until it is used or expires." />}
      {invitations && invitations.length > 0 && (
        <div className="table-wrap">
          <table className="table">
            <caption className="visually-hidden">Open invitations</caption>
            <thead>
              <tr>
                <th scope="col">For</th>
                <th scope="col">Role</th>
                <th scope="col">Expires</th>
                <th scope="col">
                  <span className="visually-hidden">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {invitations.map((invitation) => (
                <tr key={invitation.id}>
                  <td>{invitation.email ?? 'Anyone with the link'}</td>
                  <td>{roleLabels[invitation.role]}</td>
                  <td>{new Date(invitation.expiresAt).toLocaleDateString()}</td>
                  <td className="cell-actions">
                    <button type="button" className="btn btn-sm btn-danger" onClick={() => void revokeInvitation({ teamId: team.id, invitationId: invitation.id })}>
                      Revoke
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
