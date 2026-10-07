import { computeScore, computeStars, levelTotals, minimumDurationSec } from '../../src/domain/levels';
import { applyLevelResult } from '../../src/domain/progress';
import { validateRun } from '../../src/domain/runValidation';
import { C, level, player, run } from './helpers';

const ctx = { levels: C.levels, economy: C.economy, achievements: C.achievements, coinMultiplier: 1, now: 5_000 };

describe('stars', () => {
  const l = level(1); const t = levelTotals(l);
  it('1 star for finishing', () => expect(computeStars(l, { ...run(1), coins: 0 })).toBe(1));
  it('2 stars with enough coins', () => expect(computeStars(l, { ...run(1), coins: l.targetCoins })).toBe(2));
  it('3 stars needs most coins and all big stars', () => {
    expect(computeStars(l, { ...run(1), coins: l.threeStarCoins, starTokens: 2 })).toBe(2);
    expect(computeStars(l, { ...run(1), coins: l.threeStarCoins, starTokens: t.starTokens })).toBe(3);
  });
});

describe('score', () => {
  it('is computed on the server from counts only', () => {
    const l = level(1); const t = levelTotals(l);
    const s = computeScore(l, { coins: 10, coinValue: 20, fish: 2, gems: 1, starTokens: 3, heartsLeft: 3 }, C.economy);
    expect(s).toBe(t.length + 20 * 10 + 2 * 50 + 150 + 3 * 300 + 3 * 200);
  });
});

describe('validateRun', () => {
  const p = player();
  it('accepts a normal run', () => expect(validateRun(run(1), level(1), p, C.economy).ok).toBe(true));
  it('rejects a locked level', () => {
    const v = validateRun(run(2), level(2), p, C.economy);
    expect(v).toMatchObject({ ok: false, code: 'permission-denied' });
  });
  it('rejects more coins than the level has', () => {
    const t = levelTotals(level(1));
    expect(validateRun(run(1, { coins: t.coins + 1, coinValue: t.coins + 1 }), level(1), p, C.economy).ok).toBe(false);
  });
  it('rejects coinValue above the Double Coins maximum', () => {
    expect(validateRun(run(1, { coins: 10, coinValue: 21 }), level(1), p, C.economy).ok).toBe(false);
    expect(validateRun(run(1, { coins: 10, coinValue: 20 }), level(1), p, C.economy).ok).toBe(true);
  });
  it('rejects impossible speed', () => {
    expect(validateRun(run(1, { durationSec: minimumDurationSec(level(1)) - 1 }), level(1), p, C.economy).ok).toBe(false);
  });
  it('rejects fractional, negative or missing values', () => {
    expect(validateRun(run(1, { fish: 1.5 }), level(1), p, C.economy).ok).toBe(false);
    expect(validateRun(run(1, { gems: -1 }), level(1), p, C.economy).ok).toBe(false);
    expect(validateRun({ ...run(1), heartsLeft: undefined }, level(1), p, C.economy).ok).toBe(false);
    expect(validateRun({ ...run(1), runId: 'x' }, level(1), p, C.economy).ok).toBe(false);
  });
  it('rejects a disabled level', () => {
    expect(validateRun(run(1), { ...level(1), enabled: false }, p, C.economy)).toMatchObject({ ok: false, code: 'failed-precondition' });
  });
});

describe('applyLevelResult', () => {
  it('first clear: rewards, unlocks level 2, updates stats, does not mutate input', () => {
    const before = player();
    const r = run(1, { coins: 40, coinValue: 40, fish: 3, jumps: 5 });
    const { player: p, result } = applyLevelResult(before, level(1), r, ctx);
    expect(before.coins).toBe(0);
    expect(result.firstCompletion).toBe(true);
    expect(result.unlockedLevel).toBe(2);
    expect(p.currentLevel).toBe(2);
    expect(result.coinsEarned).toBe(40);
    expect(result.bonusCoins).toBe(C.economy.firstClearBonusBase + C.economy.firstClearBonusPerLevel * 1 + result.stars * C.economy.newStarBonus);
    expect(p.fish).toBe(3);
    expect(p.stats.levelsCompleted).toBe(1);
    expect(result.achievements.map(a => a.id)).toContain('first_jump');
    expect(p.achievements.first_jump).toBe(true);
    expect(p.stars).toBe(result.stars);
  });

  it('replay: coins again, but no first-clear bonus, and gems/boxes only once', () => {
    const l = level(3); const t = levelTotals(l);
    const start = player({ currentLevel: 3 });
    const first = applyLevelResult(start, l, run(3, { gems: t.gems, mysteryBoxes: t.mysteryBoxes }), ctx).player;
    expect(first.gems).toBe(t.gems);
    expect(first.pendingBoxes).toBe(t.mysteryBoxes);
    const again = applyLevelResult(first, l, run(3, { gems: t.gems, mysteryBoxes: t.mysteryBoxes }), ctx);
    expect(again.result.firstCompletion).toBe(false);
    expect(again.result.gemsEarned).toBe(0);
    expect(again.result.mysteryBoxesEarned).toBe(0);
    expect(again.result.bonusCoins).toBe(0);
    expect(again.player.levels[l.id].completions).toBe(2);
  });

  it('improving from 1 to 3 stars pays the new-star bonus for two stars and keeps the best', () => {
    const l = level(1); const t = levelTotals(l);
    const one = applyLevelResult(player(), l, run(1, { coins: 0, coinValue: 0 }), ctx).player;
    const three = applyLevelResult(one, l, run(1, { coins: l.threeStarCoins, coinValue: l.threeStarCoins, starTokens: t.starTokens }), ctx);
    expect(three.result.stars).toBe(3);
    expect(three.result.bonusCoins).toBe(2 * C.economy.newStarBonus);
    expect(three.player.stars).toBe(3);
    const worse = applyLevelResult(three.player, l, run(1, { coins: 0, coinValue: 0 }), ctx);
    expect(worse.player.levels[l.id].bestStars).toBe(3);
    expect(worse.player.stars).toBe(3);
  });

  it('skips disabled levels when unlocking the next one', () => {
    const levels = C.levels.map(l => (l.number === 2 ? { ...l, enabled: false } : l));
    const { player: p } = applyLevelResult(player(), level(1), run(1), { ...ctx, levels });
    expect(p.currentLevel).toBe(3);
  });

  it('never lowers currentLevel when replaying an early level', () => {
    const { player: p } = applyLevelResult(player({ currentLevel: 6 }), level(1), run(1), ctx);
    expect(p.currentLevel).toBe(6);
  });

  it('applies the coin multiplier (clamped) to coins and bonuses', () => {
    const { result } = applyLevelResult(player(), level(1), run(1, { coins: 10, coinValue: 10 }), { ...ctx, coinMultiplier: 10 });
    expect(result.coinsEarned).toBe(30);
  });
});
