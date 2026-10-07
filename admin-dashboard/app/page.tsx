'use client';
import { api } from '@/lib/api';
import { DEFAULTS } from '@/lib/types';
import { Card, fmt, Stat, Status, useAction, useLoad } from '@/components/ui';
import { useAdmin } from '@/components/AuthGate';

/** Aggregate statistics only — no individual player data is shown (privacy by design). */
export default function Overview() {
  const { data, error, loading, reload } = useLoad(() => api.stats());
  const { role } = useAdmin();
  const seed = useAction();
  const levels = DEFAULTS.levels;

  return (
    <>
      <h1>Overview</h1>
      {error && <p className="error">{error}</p>}
      {loading && <p className="muted">Loading…</p>}
      {data && (
        <>
          <div className="stats">
            <Stat label="Players" value={fmt(data.players.total)} hint={`+${fmt(data.players.new24h)} today · +${fmt(data.players.new7d)} this week`} />
            <Stat label="Average level reached" value={data.players.avgLevel.toFixed(1)} />
            <Stat label="Coins held (all players)" value={fmt(data.economy.coins)} />
            <Stat label="Gems held" value={fmt(data.economy.gems)} />
            <Stat label="Stars earned" value={fmt(data.economy.stars)} />
            <Stat label="Flagged leaderboard entries" value={fmt(data.leaderboardFlagged)} />
          </div>
          <Card title="Level difficulty" actions={<button onClick={reload}>Refresh</button>}>
            <p className="muted">Fail rate = failures ÷ (failures + completions). Levels above 60% are highlighted: consider lowering density or speed, or adding a checkpoint.</p>
            <table>
              <thead><tr><th>Level</th><th>Completions</th><th>Failures</th><th>Fail rate</th><th>Avg stars</th><th>Checkpoint restarts</th><th>Players currently here</th><th>Rejected runs</th><th>Synced offline</th></tr></thead>
              <tbody>
                {levels.map((l) => {
                  const s = data.levels.find((x) => x.id === l.id) ?? { id: l.id };
                  const c = s.completions ?? 0, f = s.failures ?? 0, rate = c + f > 0 ? f / (c + f) : 0;
                  const here = data.playersAtLevel.find((p) => p.level === l.number)?.players ?? 0;
                  return (
                    <tr key={l.id} className={rate > 0.6 ? 'hot' : ''}>
                      <td>{l.number}. {l.name}</td><td>{fmt(c)}</td><td>{fmt(f)}</td><td>{(rate * 100).toFixed(0)}%</td>
                      <td>{c ? ((s.starsTotal ?? 0) / c).toFixed(2) : '—'}</td><td>{fmt(s.checkpointRestarts)}</td><td>{fmt(here)}</td><td>{fmt(s.rejectedRuns)}</td><td>{fmt(s.offlineSynced)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </Card>
          <Card title="Top client issues (non-fatal)">
            {data.topIssues.length === 0 ? <p className="muted">No issues reported.</p> : (
              <ul>{data.topIssues.map((i) => <li key={i.id}><code>{i.code}</code> × {fmt(i.count)}</li>)}</ul>
            )}
          </Card>
        </>
      )}
      {role === 'super_admin' && (
        <Card title="Content setup">
          <p className="muted">Writes the bundled default content (worlds, 12 levels, shop, achievements, rewards, runtime config) into Firestore. Existing documents are kept unless you choose overwrite.</p>
          <button disabled={seed.busy} onClick={() => seed.run(async () => { const r = await api.seedContent({ overwrite: false }); seed.setMsg({ ok: true, text: `Seeded ${r.written} documents.` }); })}>Seed missing content</button>{' '}
          <button className="danger" disabled={seed.busy} onClick={() => confirm('Overwrite all content with the defaults?') && seed.run(() => api.seedContent({ overwrite: true }))}>Reset all content to defaults</button>
          <Status msg={seed.msg} />
        </Card>
      )}
    </>
  );
}
