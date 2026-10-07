'use client';
import { useEffect, useState } from 'react';
import { api } from '@/lib/api';
import { RuntimeConfig } from '@/lib/types';
import { Card, date, JsonEditor, Status, useAction, useLoad } from '@/components/ui';

const NUMBERS: [keyof RuntimeConfig, string, number, number, number][] = [
  ['run_speed_scale', 'Running speed ×', 0.8, 1.2, 0.01],
  ['difficulty_density_scale', 'Obstacle density ×', 0.6, 1.2, 0.01],
  ['coin_reward_multiplier', 'Coin rewards ×', 0.5, 3, 0.1],
  ['starting_hearts', 'Hearts per run', 1, 5, 1],
];
const FLAGS: [keyof RuntimeConfig, string][] = [
  ['daily_rewards_enabled', 'Daily rewards'], ['shop_enabled', 'Cat Shop'], ['mystery_boxes_enabled', 'Mystery boxes'],
  ['events_enabled', 'Events'], ['leaderboard_enabled', 'Leaderboards (generated names only — review child-safety rules before enabling)'],
];

/**
 * Publishes to Firebase Remote Config (read by the game) and mirrors to config/runtime (read by Cloud Functions).
 * Values are clamped on the server and in the game. Jumping, gravity and lanes are intentionally not remote-controlled.
 */
export default function RemoteConfigPage() {
  const { data, reload } = useLoad(() => api.getRemoteConfig());
  const [v, setV] = useState<RuntimeConfig | null>(null);
  const action = useAction();
  useEffect(() => { if (data) setV(data.values); }, [data]);
  if (!v) return <><h1>Remote Config</h1><p className="muted">Loading…</p></>;

  return (
    <>
      <h1>Remote Config</h1>
      <p className="muted">Last published: {date(data?.updatedAt)}</p>
      <Card title="Tuning">
        <div className="form">
          {NUMBERS.map(([k, label, min, max, step]) => (
            <label key={k}>{label}<input type="range" min={min} max={max} step={step} value={v[k] as number} onChange={(e) => setV({ ...v, [k]: Number(e.target.value) })} /><b>{String(v[k])}</b></label>
          ))}
          {(['magnet', 'super_jump', 'speed_boost', 'double_coins'] as const).map((p) => (
            <label key={p}>{p.replace('_', ' ')} (seconds)<input type="number" min={2} max={30} value={v.powerup_durations[p]} onChange={(e) => setV({ ...v, powerup_durations: { ...v.powerup_durations, [p]: Number(e.target.value) } })} /></label>
          ))}
        </div>
      </Card>
      <Card title="Features">
        {FLAGS.map(([k, label]) => (
          <label key={k} className="check"><input type="checkbox" checked={v[k] as boolean} onChange={(e) => setV({ ...v, [k]: e.target.checked })} /> {label}</label>
        ))}
        <label>Maintenance / info message (shown on the home screen, max 200 characters, no links)
          <input type="text" maxLength={200} value={v.maintenance_message} onChange={(e) => setV({ ...v, maintenance_message: e.target.value })} />
        </label>
      </Card>
      <Card title="Daily rewards (7-day cycle, never resets)">
        <JsonEditor value={v.daily_rewards} rows={12} onSave={(d) => setV({ ...v, daily_rewards: d })} />
        <p className="muted">“Validate & save” here only updates the form — press Publish below.</p>
      </Card>
      <button className="primary big" disabled={action.busy} onClick={() => action.run(async () => { await api.publishRemoteConfig({ values: v }); await reload(); }, 'Published. Games pick it up on next launch; functions within a minute.')}>Publish Remote Config</button>
      <Status msg={action.msg} />
    </>
  );
}
