'use client';

import { useState, type FormEvent } from 'react';
import { toProblem } from '@/shared/api/problems';
import { apiBaseUrl } from '@/shared/config';
import { CopyField } from '@/shared/ui/CopyField';
import { relativeTime } from '@/shared/ui/feedback';
import { FormProblem, TextField } from '@/shared/ui/forms';
import { useCreateApiKeyMutation, useListApiKeysQuery, useRevokeApiKeyMutation, type ApiKey, type ApiScope } from '../apiKeysApi';

function statusOf(key: ApiKey): string {
  if (key.revokedAt) return 'Revoked';
  if (key.expiresAt && Date.parse(key.expiresAt) < Date.now()) return 'Expired';
  return 'Active';
}

/** Keys let scripts, integrations and LLM agents use the public API (/api/v1) for this team. */
export function ApiKeysSection({ teamId }: { teamId: string }) {
  const { data: keys } = useListApiKeysQuery(teamId);
  const [createApiKey, created] = useCreateApiKeyMutation();
  const [revokeApiKey] = useRevokeApiKeyMutation();
  const [name, setName] = useState('');
  const [write, setWrite] = useState(false);
  const [expires, setExpires] = useState('');
  const [secret, setSecret] = useState<string | null>(null);
  const problem = toProblem(created.error);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const scopes: ApiScope[] = write ? ['read', 'write'] : ['read'];
    const result = await createApiKey({ teamId, name, scopes, expiresAt: expires ? new Date(`${expires}T23:59:59`).toISOString() : undefined })
      .unwrap()
      .catch(() => null);
    if (result) {
      setSecret(result.key);
      setName('');
    }
  };

  return (
    <section className="card section" aria-labelledby="api-keys-heading">
      <h2 id="api-keys-heading">API keys</h2>
      <p className="muted">
        For scripts, integrations and LLM agents using the public API. See the <a href={`${apiBaseUrl}/docs`}>API reference</a>.
      </p>
      <form className="stack" onSubmit={submit} noValidate>
        <FormProblem problem={problem} fields={['name', 'scopes', 'expiresAt']} />
        <div className="row row-end row-wrap">
          <TextField label="Name" name="name" value={name} onChange={(event) => setName(event.target.value)} problem={problem} placeholder="e.g. Modelling agent" required />
          <TextField label="Expires (optional)" name="expiresAt" type="date" value={expires} onChange={(event) => setExpires(event.target.value)} problem={problem} />
          <label className="checkbox">
            <input type="checkbox" checked={write} onChange={(event) => setWrite(event.target.checked)} />
            Can change boards
          </label>
          <button type="submit" className="btn btn-primary" disabled={created.isLoading || name.trim() === ''}>
            Create key
          </button>
        </div>
      </form>
      {secret && <CopyField label="Your new API key" value={secret} hint="Copy it now: it is shown this once. Send it as 'Authorization: Bearer <key>'." />}
      {keys && keys.length > 0 && (
        <div className="table-wrap">
          <table className="table">
            <caption className="visually-hidden">API keys</caption>
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">Key</th>
                <th scope="col">Access</th>
                <th scope="col">Last used</th>
                <th scope="col">Status</th>
                <th scope="col">
                  <span className="visually-hidden">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {keys.map((key) => (
                <tr key={key.id}>
                  <td>{key.name}</td>
                  <td>
                    <code>{key.prefix}…</code>
                  </td>
                  <td>{key.scopes.includes('write') ? 'Read and write' : 'Read only'}</td>
                  <td>{key.lastUsedAt ? relativeTime(key.lastUsedAt) : 'Never'}</td>
                  <td>{statusOf(key)}</td>
                  <td className="cell-actions">
                    {!key.revokedAt && (
                      <button
                        type="button"
                        className="btn btn-sm btn-danger"
                        onClick={() => {
                          if (window.confirm(`Revoke “${key.name}”? Anything using it stops working at once.`)) void revokeApiKey({ teamId, keyId: key.id });
                        }}
                      >
                        Revoke
                      </button>
                    )}
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
