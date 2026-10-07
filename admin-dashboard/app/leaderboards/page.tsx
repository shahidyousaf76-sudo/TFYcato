'use client';
import { collection, getDocs, limit, orderBy, query } from 'firebase/firestore';
import { useState } from 'react';
import { api } from '@/lib/api';
import { db } from '@/lib/firebase';
import { DEFAULTS, levelTotals } from '@/lib/types';
import { Card, date, fmt, Status, useAction, useLoad } from '@/components/ui';

interface Entry { uid: string; username: string; score: number; stars: number; updatedAt: number; hidden: boolean; flagged: boolean }

/** Review leaderboard entries. Scores are computed by the server, so outliers are rare; hide anything that looks wrong. */
export default function Leaderboards() {
  const [levelId, setLevelId] = useState(DEFAULTS.levels[0].id);
  const action = useAction();
  const { data, reload } = useLoad(async () => {
    const snap = await getDocs(query(collection(db, `leaderboards/${levelId}/entries`), orderBy('score', 'desc'), limit(100)));
    return snap.docs.map((d) => d.data() as Entry);
  }, [levelId]);
  const level = DEFAULTS.levels.find((l) => l.id === levelId)!;
  const t = levelTotals(level);
  const maxScore = t.length + t.coins * 2 * 10 + t.fish * 50 + t.gems * 150 + t.starTokens * 300 + 5 * 200;

  return (
    <>
      <h1>Leaderboards</h1>
      <p className="muted">Leaderboards are off by default (Remote Config ▸ leaderboard_enabled). Entries show generated usernames only.</p>
      <select value={levelId} onChange={(e) => setLevelId(e.target.value)}>{DEFAULTS.levels.map((l) => <option key={l.id} value={l.id}>{l.number}. {l.name}</option>)}</select>
      <Card title={`Top 100 — highest possible score ≈ ${fmt(maxScore)}`}>
        <table>
          <thead><tr><th>#</th><th>Name</th><th>Score</th><th>Stars</th><th>Updated</th><th>Status</th><th /></tr></thead>
          <tbody>{data?.map((e, i) => (
            <tr key={e.uid} className={e.hidden ? 'muted' : e.score > maxScore ? 'hot' : ''}>
              <td>{i + 1}</td><td>{e.username}</td><td>{fmt(e.score)}</td><td>{e.stars}</td><td>{date(e.updatedAt)}</td><td>{e.hidden ? 'hidden' : e.flagged ? 'flagged' : 'visible'}</td>
              <td><button disabled={action.busy} onClick={() => action.run(async () => { await api.reviewLeaderboard({ levelId, uid: e.uid, hidden: !e.hidden }); await reload(); })}>{e.hidden ? 'Restore' : 'Hide'}</button></td>
            </tr>
          ))}</tbody>
        </table>
        {data?.length === 0 && <p className="muted">No entries.</p>}
      </Card>
      <Status msg={action.msg} />
    </>
  );
}
