'use client';
import { collection, getDocs } from 'firebase/firestore';
import { useState } from 'react';
import { api } from '@/lib/api';
import { db } from '@/lib/firebase';
import { DEFAULTS, LevelDef, levelTotals } from '@/lib/types';
import { Card, JsonEditor, Status, useAction, useLoad } from '@/components/ui';

/** Manage levels: enable/disable, edit sections and tuning, create new levels (11+ for combinations). The server validates every save. */
export default function Levels() {
  const { data, error, reload } = useLoad(async () => {
    const snap = await getDocs(collection(db, 'levels'));
    const live = snap.docs.map((d) => d.data() as LevelDef).sort((a, b) => a.number - b.number);
    return { live, seeded: live.length > 0 };
  });
  const [editing, setEditing] = useState<LevelDef | null>(null);
  const action = useAction();
  const levels = data?.seeded ? data.live : (DEFAULTS.levels as LevelDef[]);

  const newLevel = (): LevelDef => {
    const last = levels[levels.length - 1];
    const n = (last?.number ?? 0) + 1;
    return { ...structuredClone(last), id: `level_${String(n).padStart(2, '0')}`, number: n, name: `New Level ${n}`, seed: 1000 + n * 7919, enabled: false, tutorial: false };
  };

  return (
    <>
      <h1>Levels</h1>
      {error && <p className="error">{error}</p>}
      {data && !data.seeded && <p className="warn">Firestore has no levels yet — showing the bundled defaults. Seed content on the Overview page to edit them.</p>}
      <Card title={`${levels.length} levels`} actions={<button onClick={() => setEditing(newLevel())}>+ New level</button>}>
        <table>
          <thead><tr><th>#</th><th>Name</th><th>World</th><th>Difficulty</th><th>Length</th><th>Speed</th><th>Coins (2★ / 3★)</th><th>Checkpoints</th><th>Enabled</th><th /></tr></thead>
          <tbody>
            {levels.map((l) => {
              const t = levelTotals(l);
              return (
                <tr key={l.id}>
                  <td>{l.number}</td><td>{l.name}</td><td>{l.world}</td><td>{l.difficulty}</td><td>{t.length} m</td>
                  <td>{l.baseSpeed}–{l.maxSpeed} m/s</td><td>{t.coins} ({l.targetCoins} / {l.threeStarCoins})</td><td>{l.checkpointCount}</td>
                  <td>
                    <label className="switch">
                      <input type="checkbox" checked={l.enabled} disabled={!data?.seeded || action.busy}
                        onChange={(e) => action.run(async () => { await api.setLevelEnabled({ levelId: l.id, enabled: e.target.checked }); await reload(); }, e.target.checked ? 'Level enabled.' : 'Level disabled (players skip it).')} />
                      <span />
                    </label>
                  </td>
                  <td><button onClick={() => setEditing(l)}>Edit</button></td>
                </tr>
              );
            })}
          </tbody>
        </table>
        <Status msg={action.msg} />
      </Card>
      {editing && (
        <Card title={`Edit ${editing.id}`} actions={<button onClick={() => setEditing(null)}>Close</button>}>
          <p className="muted">
            Sections run in order: start → easy → collectibles → obstacles → 🚩 → medium → challenge → 🚩 → finish. Obstacles:{' '}
            {DEFAULTS.obstacles.map((o) => `${o.id} (from level ${o.minLevel})`).join(', ')}. The game places exactly the coins, fish, gems, stars,
            boxes and power-ups you set per section, and never blocks all three lanes unless every lane can be jumped.
          </p>
          <JsonEditor value={editing} busy={action.busy} rows={30}
            onSave={(v) => action.run(async () => { await api.upsertLevel({ level: v }); await reload(); setEditing(null); }, 'Level saved. Players get it on next launch.')} />
          <Status msg={action.msg} />
        </Card>
      )}
    </>
  );
}
