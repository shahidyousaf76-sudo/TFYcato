'use client';
import { collection, getDocs, orderBy, query } from 'firebase/firestore';
import { db } from '@/lib/firebase';
import { Card, date, fmt, useLoad } from '@/components/ui';

interface Issue { code: string; count: number; lastSeen: number; versions?: Record<string, number>; platforms?: Record<string, number> }

const MEANING: Record<string, string> = {
  SAVE_FAILED: 'The device could not write its local save (storage full?)',
  SAVE_CORRUPTED: 'A local save was unreadable and the player started fresh',
  SAVE_RECOVERED_FROM_BACKUP: 'A damaged save was restored from the backup copy',
  RUN_REJECTED: 'The server rejected a finished run (validation failed)',
};

/** Non-fatal issues reported by the game (codes only, no personal data). Crashes are in Firebase Crashlytics. */
export default function Errors() {
  const { data, error } = useLoad(async () => (await getDocs(query(collection(db, 'clientIssues'), orderBy('count', 'desc')))).docs.map((d) => d.data() as Issue));
  const project = process.env.NEXT_PUBLIC_FIREBASE_PROJECT_ID;
  return (
    <>
      <h1>Crashes & Errors</h1>
      <Card title="Crashes">
        <p>Crash reports (with stack traces, devices and app versions) are collected by Firebase Crashlytics.</p>
        {project && <a className="button" href={`https://console.firebase.google.com/project/${project}/crashlytics`} target="_blank" rel="noreferrer">Open Crashlytics</a>}
      </Card>
      <Card title="Reported issues">
        {error && <p className="error">{error}</p>}
        <table>
          <thead><tr><th>Code</th><th>Meaning</th><th>Count</th><th>Last seen</th><th>Versions</th><th>Platforms</th></tr></thead>
          <tbody>{data?.map((i) => (
            <tr key={i.code}><td><code>{i.code}</code></td><td>{MEANING[i.code] ?? ''}</td><td>{fmt(i.count)}</td><td>{date(i.lastSeen)}</td>
              <td>{Object.entries(i.versions ?? {}).map(([v, n]) => `${v.replace(/_/g, '.')}: ${n}`).join(', ')}</td>
              <td>{Object.entries(i.platforms ?? {}).map(([p, n]) => `${p}: ${n}`).join(', ')}</td></tr>
          ))}</tbody>
        </table>
        {data?.length === 0 && <p className="muted">Nothing reported. 🎉</p>}
      </Card>
    </>
  );
}
