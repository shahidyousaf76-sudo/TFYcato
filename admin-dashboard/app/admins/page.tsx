'use client';
import { useState } from 'react';
import { api } from '@/lib/api';
import { useAdmin } from '@/components/AuthGate';
import { Card, Status, useAction, useLoad } from '@/components/ui';

/** Super admins grant or remove admin roles. The account must already exist in Firebase Authentication (email/password). */
export default function Admins() {
  const { role } = useAdmin();
  const { data, reload } = useLoad(() => (role === 'super_admin' ? api.listAdmins() : Promise.resolve({ admins: [] })), [role]);
  const [email, setEmail] = useState('');
  const [newRole, setNewRole] = useState<'admin' | 'super_admin'>('admin');
  const action = useAction();
  if (role !== 'super_admin') return <><h1>Admins</h1><p className="warn">Only super admins can manage roles.</p></>;

  return (
    <>
      <h1>Admins</h1>
      <Card title="Grant a role">
        <div className="form">
          <label>Email<input type="email" value={email} onChange={(e) => setEmail(e.target.value)} /></label>
          <label>Role<select value={newRole} onChange={(e) => setNewRole(e.target.value as 'admin' | 'super_admin')}><option value="admin">admin</option><option value="super_admin">super_admin</option></select></label>
        </div>
        <button className="primary" disabled={action.busy || !email} onClick={() => action.run(async () => { await api.setRole({ email, role: newRole }); await reload(); }, 'Role granted. They must sign out and in again.')}>Grant</button>
      </Card>
      <Card title="Current admins">
        <table><thead><tr><th>Email</th><th>Role</th><th>Status</th><th /></tr></thead>
          <tbody>{data?.admins.map((a) => (
            <tr key={a.email}><td>{a.email}</td><td>{a.role}</td><td>{a.disabled ? 'disabled' : 'active'}</td>
              <td><button className="danger" disabled={action.busy} onClick={() => a.email && confirm(`Remove ${a.role} from ${a.email}?`) && action.run(async () => { await api.setRole({ email: a.email!, role: null }); await reload(); }, 'Role removed.')}>Remove</button></td></tr>
          ))}</tbody>
        </table>
      </Card>
      <Status msg={action.msg} />
    </>
  );
}
