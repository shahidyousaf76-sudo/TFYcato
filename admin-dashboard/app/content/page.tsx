'use client';
import { collection, getDocs } from 'firebase/firestore';
import { useState } from 'react';
import { api } from '@/lib/api';
import { db } from '@/lib/firebase';
import { AchievementDef, DEFAULTS, ShopItem, WorldDef } from '@/lib/types';
import { Card, JsonEditor, Status, useAction, useLoad } from '@/components/ui';

type Tab = 'worlds' | 'catalog' | 'achievements';
type Doc = WorldDef | ShopItem | AchievementDef;
const DEFAULT: Record<Tab, Doc[]> = { worlds: DEFAULTS.worlds, catalog: DEFAULTS.shop, achievements: DEFAULTS.achievements };

/** Game content configuration: worlds (colours, fog, weather, music), Cat Shop items (prices, unlock level) and achievements. */
export default function Content() {
  const [tab, setTab] = useState<Tab>('catalog');
  const [editing, setEditing] = useState<Doc | null>(null);
  const action = useAction();
  const { data, reload } = useLoad(async () => {
    const snap = await getDocs(collection(db, tab));
    return snap.empty ? { docs: DEFAULT[tab], live: false } : { docs: snap.docs.map((d) => d.data() as Doc), live: true };
  }, [tab]);

  const label = (d: Doc) => ('name' in d ? d.name : (d as { id: string }).id);
  const detail = (d: Doc) => {
    if (tab === 'catalog') { const s = d as ShopItem; return `${s.category} · ${s.priceGems ? `${s.priceGems} gems` : s.priceCoins ? `${s.priceCoins} coins` : s.source}${s.unlockLevel ? ` · after level ${s.unlockLevel}` : ''}${s.enabled ? '' : ' · disabled'}`; }
    if (tab === 'worlds') { const w = d as WorldDef; return `${w.difficultyLabel} · ${w.enabled ? 'enabled' : w.comingSoon ? 'coming soon' : 'disabled'}`; }
    const a = d as AchievementDef; return `${a.stat} ≥ ${a.target} · +${a.rewardCoins} coins${a.rewardGems ? ` +${a.rewardGems} gems` : ''}`;
  };

  return (
    <>
      <h1>Worlds, Shop & Achievements</h1>
      <div className="tabs">{(['catalog', 'worlds', 'achievements'] as Tab[]).map((t) => <button key={t} className={t === tab ? 'primary' : ''} onClick={() => { setTab(t); setEditing(null); }}>{t === 'catalog' ? 'Cat Shop' : t[0].toUpperCase() + t.slice(1)}</button>)}</div>
      {data && !data.live && <p className="warn">Showing bundled defaults (not seeded yet). Saving an item writes it to Firestore.</p>}
      <Card title={tab === 'catalog' ? 'Items' : tab} actions={<button onClick={() => setEditing(structuredClone(DEFAULT[tab][0]))}>+ New (from template)</button>}>
        <div className="grid">
          {data?.docs.map((d) => (
            <button key={(d as { id: string }).id} className="tile" onClick={() => setEditing(d)}>
              {'colors' in d && <span className="swatches">{(d as ShopItem).colors.slice(0, 4).map((c) => <i key={c} style={{ background: c }} />)}</span>}
              {'palette' in d && <span className="swatches">{Object.values((d as WorldDef).palette).slice(0, 5).map((c, i) => <i key={i} style={{ background: c }} />)}</span>}
              <strong>{label(d)}</strong><small>{detail(d)}</small>
            </button>
          ))}
        </div>
      </Card>
      {editing && (
        <Card title={`Edit ${(editing as { id: string }).id}`} actions={<button onClick={() => setEditing(null)}>Close</button>}>
          {tab === 'catalog' && <p className="muted">Use either a coin price or a gem price. Items with source “daily”/“mystery” are rewards and can’t be bought. Cosmetics never change gameplay.</p>}
          <JsonEditor value={editing} busy={action.busy} onSave={(v) => action.run(async () => { await api.upsertContent({ collection: tab, doc: v }); await reload(); })} />
          <Status msg={action.msg} />
        </Card>
      )}
    </>
  );
}
