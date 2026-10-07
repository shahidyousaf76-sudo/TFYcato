'use client';
import { collection, getDocs } from 'firebase/firestore';
import { useState } from 'react';
import { Announcement, api, GameEvent } from '@/lib/api';
import { db } from '@/lib/firebase';
import { DEFAULTS } from '@/lib/types';
import { Card, date, Status, useAction, useLoad } from '@/components/ui';

const toInput = (ms: number) => new Date(ms - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16);
const fromInput = (s: string) => new Date(s).getTime();

/** Game events (e.g. double coins in the Beach world this weekend) and short announcements for the home screen. */
export default function Events() {
  const { data, reload } = useLoad(async () => {
    const [ev, an] = await Promise.all([getDocs(collection(db, 'events')), getDocs(collection(db, 'announcements'))]);
    return { events: ev.docs.map((d) => ({ id: d.id, ...(d.data() as GameEvent) })), announcements: an.docs.map((d) => ({ id: d.id, ...(d.data() as Announcement) })) };
  });
  const now = Date.now();
  const [ev, setEv] = useState<GameEvent>({ title: '', description: '', active: true, startsAt: now, endsAt: now + 3 * 86400000, coinMultiplier: 2, worldId: '' });
  const [an, setAn] = useState<Announcement>({ title: '', message: '', active: true, startsAt: now, endsAt: now + 7 * 86400000 });
  const action = useAction();

  return (
    <>
      <h1>Events & Announcements</h1>
      <Card title="Create or edit an event">
        <div className="form">
          <label>Title<input value={ev.title} maxLength={40} onChange={(e) => setEv({ ...ev, title: e.target.value })} /></label>
          <label>Description<input value={ev.description} maxLength={200} onChange={(e) => setEv({ ...ev, description: e.target.value })} /></label>
          <label>Coin multiplier (1–3)<input type="number" min={1} max={3} step={0.5} value={ev.coinMultiplier} onChange={(e) => setEv({ ...ev, coinMultiplier: Number(e.target.value) })} /></label>
          <label>World (empty = all)<select value={ev.worldId} onChange={(e) => setEv({ ...ev, worldId: e.target.value })}><option value="">All worlds</option>{DEFAULTS.worlds.filter((w) => w.enabled).map((w) => <option key={w.id} value={w.id}>{w.name}</option>)}</select></label>
          <label>Starts<input type="datetime-local" value={toInput(ev.startsAt)} onChange={(e) => setEv({ ...ev, startsAt: fromInput(e.target.value) })} /></label>
          <label>Ends<input type="datetime-local" value={toInput(ev.endsAt)} onChange={(e) => setEv({ ...ev, endsAt: fromInput(e.target.value) })} /></label>
          <label className="check"><input type="checkbox" checked={ev.active} onChange={(e) => setEv({ ...ev, active: e.target.checked })} /> Active</label>
        </div>
        <button className="primary" disabled={action.busy} onClick={() => action.run(async () => { await api.upsertEvent({ event: ev }); await reload(); })}>Save event</button>
      </Card>
      <Card title="Events">
        <table><thead><tr><th>Title</th><th>Multiplier</th><th>World</th><th>From</th><th>To</th><th>Active</th><th /></tr></thead>
          <tbody>{data?.events.map((e) => <tr key={e.id}><td>{e.title}</td><td>×{e.coinMultiplier ?? 1}</td><td>{e.worldId || 'all'}</td><td>{date(e.startsAt)}</td><td>{date(e.endsAt)}</td><td>{e.active ? 'yes' : 'no'}</td><td><button onClick={() => setEv(e)}>Edit</button></td></tr>)}</tbody>
        </table>
      </Card>
      <Card title="Publish an announcement">
        <p className="muted">Shown to children on the home screen: short, friendly, plain text. Links, emails and contact details are rejected.</p>
        <div className="form">
          <label>Title<input value={an.title} maxLength={40} onChange={(e) => setAn({ ...an, title: e.target.value })} /></label>
          <label>Message<input value={an.message} maxLength={200} onChange={(e) => setAn({ ...an, message: e.target.value })} /></label>
          <label>Starts<input type="datetime-local" value={toInput(an.startsAt)} onChange={(e) => setAn({ ...an, startsAt: fromInput(e.target.value) })} /></label>
          <label>Ends<input type="datetime-local" value={toInput(an.endsAt)} onChange={(e) => setAn({ ...an, endsAt: fromInput(e.target.value) })} /></label>
          <label className="check"><input type="checkbox" checked={an.active} onChange={(e) => setAn({ ...an, active: e.target.checked })} /> Active</label>
        </div>
        <button className="primary" disabled={action.busy} onClick={() => action.run(async () => { await api.publishAnnouncement({ announcement: an }); await reload(); }, 'Announcement published.')}>Publish</button>
      </Card>
      <Card title="Announcements">
        <table><thead><tr><th>Title</th><th>Message</th><th>From</th><th>To</th><th>Active</th><th /></tr></thead>
          <tbody>{data?.announcements.map((a) => <tr key={a.id}><td>{a.title}</td><td>{a.message}</td><td>{date(a.startsAt)}</td><td>{date(a.endsAt)}</td><td>{a.active ? 'yes' : 'no'}</td><td><button onClick={() => setAn(a)}>Edit</button></td></tr>)}</tbody>
        </table>
      </Card>
      <Status msg={action.msg} />
    </>
  );
}
