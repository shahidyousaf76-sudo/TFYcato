'use client';
import { doc, getDoc } from 'firebase/firestore';
import { useEffect, useState } from 'react';
import { api } from '@/lib/api';
import { db } from '@/lib/firebase';
import { DEFAULTS, MysteryEntry } from '@/lib/types';
import { Card, JsonEditor, Status, useAction, useLoad } from '@/components/ui';

const ECONOMY_HELP: Record<string, string> = {
  firstClearBonusBase: 'Coins for finishing a level the first time', firstClearBonusPerLevel: '+ this × level number',
  newStarBonus: 'Coins for each new star on a level', coinRewardMultiplier: '(use Remote Config for live tuning)',
  scorePerMetre: 'Score per metre run', scorePerCoin: 'Score per coin', scorePerFish: 'Score per fish', scorePerGem: 'Score per gem',
  scorePerStarToken: 'Score per big star', scorePerHeartLeft: 'Score per heart left at the finish',
};

/** Economy numbers and the mystery box table (daily rewards live in Remote Config so they can change without an app update). */
export default function Rewards() {
  const { data, reload } = useLoad(async () => (await getDoc(doc(db, 'config/rewards'))).data() as { economy?: Record<string, number>; mysteryBox?: MysteryEntry[] } | undefined);
  const [economy, setEconomy] = useState<Record<string, number>>(DEFAULTS.economy);
  const action = useAction();
  useEffect(() => { if (data?.economy) setEconomy({ ...DEFAULTS.economy, ...data.economy }); }, [data]);
  const box = data?.mysteryBox ?? DEFAULTS.mysteryBox;
  const totalWeight = box.reduce((a, e) => a + e.weight, 0);

  return (
    <>
      <h1>Rewards & Economy</h1>
      <Card title="Economy">
        <div className="form">
          {Object.keys(ECONOMY_HELP).filter((k) => k in economy).map((k) => (
            <label key={k}>{k}<input type="number" step="any" min={0} value={economy[k]} onChange={(e) => setEconomy({ ...economy, [k]: Number(e.target.value) })} /><small>{ECONOMY_HELP[k]}</small></label>
          ))}
        </div>
        <button className="primary" disabled={action.busy} onClick={() => action.run(async () => { await api.updateRewards({ economy }); await reload(); })}>Save economy</button>
      </Card>
      <Card title="Mystery box">
        <table>
          <thead><tr><th>Reward</th><th>Type</th><th>Amount</th><th>Chance</th><th>Pool</th></tr></thead>
          <tbody>{box.map((e, i) => <tr key={i}><td>{e.label}</td><td>{e.type}</td><td>{e.amount}</td><td>{((e.weight / totalWeight) * 100).toFixed(1)}%</td><td>{e.pool?.join(', ')}</td></tr>)}</tbody>
        </table>
        <p className="muted">Rewards are rolled on the server. Item rewards only give items the child doesn’t own yet (otherwise 150 coins). No real money is involved anywhere.</p>
        <JsonEditor value={box} busy={action.busy} rows={14} onSave={(v) => action.run(async () => { await api.updateRewards({ mysteryBox: v }); await reload(); })} />
      </Card>
      <Status msg={action.msg} />
    </>
  );
}
