import { validateLevel } from '../../src/domain/adminValidation';
import { levelTotals } from '../../src/domain/levels';
import { isValidUsername } from '../../src/domain/username';
import { C } from './helpers';

describe('bundled content', () => {
  it('has at least 10 playable levels numbered 1..n', () => {
    const enabled = C.levels.filter(l => l.enabled);
    expect(enabled.length).toBeGreaterThanOrEqual(10);
    expect(C.levels).toHaveLength(100);
    expect(new Set(C.levels.map((l) => l.name)).size).toBe(100); // every level has its own name
    for (const l of C.levels) expect(l.maxSpeed).toBeLessThanOrEqual(16);
    for (const l of C.levels.filter((x) => x.number >= 11)) expect(l.difficulty).toBe('combo');
    C.levels.forEach((l, i) => expect(l.number).toBe(i + 1));
  });

  it('every level passes the same validation the admin dashboard uses', () => {
    for (const l of C.levels) expect(validateLevel(l, C.worlds, C.obstacles.map(o => o.id))).toEqual([]);
  });

  it('follows start → easy → collectibles → obstacles → medium → challenge → finish with checkpoints', () => {
    for (const l of C.levels) {
      expect(l.sections.map(s => s.kind)).toEqual(['start', 'easy', 'collectibles', 'obstacles', 'medium', 'challenge', 'finish']);
      expect(l.sections.filter(s => s.checkpointAfter).length).toBe(l.checkpointCount);
      expect(l.sections.find(s => s.kind === 'challenge')!.checkpointAfter).toBe(true); // final checkpoint before the finish
      expect(levelTotals(l).starTokens).toBe(3);
    }
  });

  it('respects the difficulty progression', () => {
    const uses = (n: number) => new Set(C.levels.find(l => l.number === n)!.sections.flatMap(s => s.obstacles));
    for (const n of [1, 2, 3]) for (const o of uses(n)) expect(['log', 'rock', 'branch']).toContain(o);
    for (const n of [4, 5, 6]) for (const o of uses(n)) expect(['log', 'rock', 'branch', 'barrel', 'wall']).toContain(o);
    expect([...uses(7), ...uses(8), ...uses(9), ...uses(10)]).toEqual(expect.arrayContaining(['broken_bridge', 'falling_rocks', 'moving_critter']));
    // obstacles never appear before their minimum level
    for (const l of C.levels) for (const o of l.sections.flatMap(s => s.obstacles)) expect(C.obstacles.find(x => x.id === o)!.minLevel).toBeLessThanOrEqual(l.number);
    // speed never drops from one level to the next
    for (let i = 1; i < C.levels.length; i++) expect(C.levels[i].baseSpeed).toBeGreaterThanOrEqual(C.levels[i - 1].baseSpeed);
  });

  it('has six playable worlds (incl. Snow World) plus seven future worlds', () => {
    expect(C.worlds.filter(w => w.enabled).map(w => w.id)).toEqual(['park', 'forest', 'city', 'mountains', 'beach', 'snow']);
    expect(C.worlds.filter(w => w.comingSoon)).toHaveLength(7);
    expect(C.levels.filter(l => l.world === 'snow').length).toBeGreaterThanOrEqual(10);
    for (const l of C.levels) expect(C.worlds.find(w => w.id === l.world)?.enabled).toBe(true);
  });

  it('has all cats, a free Orange Cat and every shop category', () => {
    const cats = C.shop.filter(i => i.slot === 'cat').map(i => i.id);
    expect(cats).toEqual(expect.arrayContaining(['orange_cat', 'white_cat', 'black_cat', 'grey_cat', 'spotted_cat', 'superhero_cat', 'pirate_cat', 'ninja_cat', 'space_cat']));
    expect(C.shop.find(i => i.id === 'orange_cat')!.source).toBe('default');
    expect(new Set(C.shop.map(i => i.category))).toEqual(new Set(['CATS', 'HATS', 'GLASSES', 'SHOES', 'BACKPACKS', 'CAPES_SCARVES', 'TRAILS']));
    for (const i of C.shop) expect(i.priceCoins > 0 && i.priceGems > 0).toBe(false);
  });

  it('daily and mystery-box items exist in the shop catalog', () => {
    for (const d of C.dailyRewards) if (d.type === 'item') expect(C.shop.some(i => i.id === d.itemId)).toBe(true);
    for (const m of C.mysteryBox) for (const id of m.pool ?? []) expect(C.shop.some(i => i.id === id)).toBe(true);
  });

  it('username prefixes produce valid names', () => {
    for (const p of C.usernameWords.prefixes) expect(isValidUsername(`${p}4821`, C.usernameWords.prefixes)).toBe(true);
  });
});
