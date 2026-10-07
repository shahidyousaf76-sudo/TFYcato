'use client';
import { collection, getDocs, limit, orderBy, query } from 'firebase/firestore';
import { db } from '@/lib/firebase';
import { Card, date, useLoad } from '@/components/ui';

interface AuditEntry { email: string | null; action: string; target: string; at: number; details?: Record<string, unknown> }

/** Every admin change, newest first. */
export default function Audit() {
  const { data, error, reload } = useLoad(async () => (await getDocs(query(collection(db, 'adminAudit'), orderBy('at', 'desc'), limit(200)))).docs.map((d) => ({ id: d.id, ...(d.data() as AuditEntry) })));
  return (
    <>
      <h1>Audit log</h1>
      <Card title="Last 200 changes" actions={<button onClick={reload}>Refresh</button>}>
        {error && <p className="error">{error}</p>}
        <table><thead><tr><th>When</th><th>Who</th><th>Action</th><th>Target</th><th>Details</th></tr></thead>
          <tbody>{data?.map((a) => <tr key={a.id}><td>{date(a.at)}</td><td>{a.email}</td><td><code>{a.action}</code></td><td>{a.target}</td><td><small>{a.details ? JSON.stringify(a.details).slice(0, 160) : ''}</small></td></tr>)}</tbody>
        </table>
      </Card>
    </>
  );
}
