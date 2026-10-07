import { claimDaily, dailyStatus, localDayKey } from '../../src/domain/daily';
import { sanitizeSettings } from '../../src/domain/player';
import { DEFAULT_RUNTIME, sanitizeRuntime } from '../../src/domain/remoteConfig';
import { equip, openMysteryBox, purchase } from '../../src/domain/shop';
import { generateUsername, isValidUsername } from '../../src/domain/username';
import { C, player } from './helpers';

const H = 3_600_000;
const T0 = Date.UTC(2026, 8, 30, 10, 0, 0); // 30 Sep 2026 10:00 UTC

describe('daily rewards', () => {
  it('claims day 1, then blocks a second claim the same day', () => {
    const r = claimDaily(player(), C.dailyRewards, C.shop, C.achievements, T0, 120);
    if (!r.ok) throw new Error(r.message);
    expect(r.claim.coins).toBe(100);
    expect(r.player.daily.nextDay).toBe(2);
    expect(claimDaily(r.player, C.dailyRewards, C.shop, C.achievements, T0 + H, 120).ok).toBe(false);
  });

  it('cannot be double-claimed by changing the time zone', () => {
    const r = claimDaily(player(), C.dailyRewards, C.shop, C.achievements, T0, 0);
    if (!r.ok) throw new Error();
    // +14h offset makes it "tomorrow" locally, but the 18h cooldown still applies
    expect(claimDaily(r.player, C.dailyRewards, C.shop, C.achievements, T0 + 2 * H, 14 * 60).ok).toBe(false);
  });

  it('missing days never resets the cycle (no pressure)', () => {
    let p = player();
    for (let d = 0; d < 3; d++) { const r = claimDaily(p, C.dailyRewards, C.shop, C.achievements, T0 + d * 24 * H, 0); if (!r.ok) throw new Error(); p = r.player; }
    const s = dailyStatus(p, C.dailyRewards, T0 + 10 * 24 * H, 0);
    expect(s.day).toBe(4);
    expect(s.canClaim).toBe(true);
  });

  it('day 5 gives the scarf item, or 200 coins if already owned; day 7 wraps to day 1', () => {
    const p = player({ daily: { lastClaimDay: null, lastClaimAt: 0, nextDay: 5 } });
    const r = claimDaily(p, C.dailyRewards, C.shop, C.achievements, T0, 0);
    if (!r.ok) throw new Error();
    expect(r.claim.itemId).toBe('blue_scarf');
    expect(r.player.unlockedItems.blue_scarf).toBe(true);
    const owned = claimDaily({ ...p, unlockedItems: { blue_scarf: true } }, C.dailyRewards, C.shop, C.achievements, T0, 0);
    if (!owned.ok) throw new Error();
    expect(owned.claim).toMatchObject({ itemId: null, coins: 200 });
    const seven = claimDaily(player({ daily: { lastClaimDay: null, lastClaimAt: 0, nextDay: 7 } }), C.dailyRewards, C.shop, C.achievements, T0, 0);
    if (!seven.ok) throw new Error();
    expect(seven.player.daily.nextDay).toBe(1);
  });

  it('local day key clamps silly offsets', () => {
    expect(localDayKey(T0, 99999)).toBe(localDayKey(T0, 14 * 60));
  });
});

describe('shop', () => {
  it('buys with coins and marks the cat unlocked', () => {
    const r = purchase(player({ coins: 600 }), C.shop, 'white_cat', C.achievements, 1);
    if (!r.ok) throw new Error(r.message);
    expect(r.player.coins).toBe(100 + 50); // 600 - 500 + "Looking Good" achievement
    expect(r.player.unlockedCats.white_cat).toBe(true);
    expect(r.achievements.map(a => a.id)).toEqual(['first_style']);
  });
  it('refuses when short of coins, already owned, level-locked or reward-only', () => {
    expect(purchase(player({ coins: 10 }), C.shop, 'white_cat', C.achievements, 1)).toMatchObject({ ok: false, code: 'resource-exhausted' });
    expect(purchase(player({ coins: 9999 }), C.shop, 'orange_cat', C.achievements, 1)).toMatchObject({ ok: false });
    expect(purchase(player({ coins: 9999 }), C.shop, 'pirate_cat', C.achievements, 1)).toMatchObject({ ok: false, code: 'failed-precondition' });
    expect(purchase(player({ coins: 9999 }), C.shop, 'rainbow_trail', C.achievements, 1)).toMatchObject({ ok: false, code: 'failed-precondition' });
    expect(purchase(player({ coins: 9999 }), C.shop, 'nope', C.achievements, 1)).toMatchObject({ ok: false, code: 'not-found' });
  });
  it('gem items spend gems, not coins', () => {
    const p = player({ gems: 20, coins: 5, levels: { level_05: { completed: true } as never } });
    const r = purchase(p, C.shop, 'superhero_cat', C.achievements, 1);
    if (!r.ok) throw new Error(r.message);
    expect(r.player.gems).toBe(5);
  });
  it('equips only owned items in the right slot', () => {
    const p = player({ unlockedItems: { red_hat: true, sunglasses: true } });
    expect(equip(p, C.shop, { equipped: { hat: 'red_hat', glasses: 'sunglasses' } }, 1).ok).toBe(true);
    expect(equip(p, C.shop, { equipped: { hat: 'sunglasses' } }, 1).ok).toBe(false);
    expect(equip(p, C.shop, { equipped: { hat: 'party_hat' } }, 1).ok).toBe(false);
    expect(equip(p, C.shop, { selectedCat: 'white_cat' }, 1).ok).toBe(false);
    expect(equip(p, C.shop, { equipped: { tail: 'x' } }, 1).ok).toBe(false);
  });
});

describe('mystery box', () => {
  it('needs a pending box and grants the rolled reward', () => {
    expect(openMysteryBox(player(), C.mysteryBox, C.shop, () => 0, 1).ok).toBe(false);
    const r = openMysteryBox(player({ pendingBoxes: 1 }), C.mysteryBox, C.shop, () => 0, 1);
    if (!r.ok) throw new Error();
    expect(r.reward).toMatchObject({ type: 'coins', amount: 100 });
    expect(r.player.pendingBoxes).toBe(0);
  });
  it('item rolls only give items you do not own, falling back to coins', () => {
    const itemRoll = () => 0.9; // lands in the weighted "item" entries
    const r = openMysteryBox(player({ pendingBoxes: 1 }), C.mysteryBox, C.shop, itemRoll, 1);
    if (!r.ok) throw new Error();
    expect(r.reward.type).toBe('item');
    const all = Object.fromEntries(C.shop.map(i => [i.id, true]));
    const r2 = openMysteryBox(player({ pendingBoxes: 1, unlockedItems: all, unlockedCats: all }), C.mysteryBox, C.shop, itemRoll, 1);
    if (!r2.ok) throw new Error();
    expect(r2.reward).toMatchObject({ type: 'coins', amount: 150 });
  });
});

describe('usernames', () => {
  const P = C.usernameWords.prefixes;
  it('generates only valid automatic names', () => {
    let seed = 1; const rng = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
    for (let i = 0; i < 500; i++) expect(isValidUsername(generateUsername(P, rng), P)).toBe(true);
  });
  it('rejects free text, real names and odd formats', () => {
    for (const bad of ['Emma2015', 'Kitty', 'Kitty12', 'Kitty12345', 'Kitty0123', 'kitty4821 ', '<b>Kitty4821</b>', 42, null]) expect(isValidUsername(bad, P)).toBe(false);
  });
});

describe('settings and runtime config sanitising', () => {
  it('accepts valid settings, rejects extras or bad values', () => {
    const ok = { music: true, sound: false, vibration: true, graphics: 'ultra', language: 'da', musicVolume: 0.5, soundVolume: 1, reduceMotion: false };
    expect(sanitizeSettings(ok)).toEqual({ ...ok, gameSpeed: 'normal' }); // older clients: defaults to normal
    expect(sanitizeSettings({ ...ok, gameSpeed: 'fast' })?.gameSpeed).toBe('fast');
    expect(sanitizeSettings({ ...ok, gameSpeed: 'turbo' })).toBeNull();
    expect(sanitizeSettings({ ...ok, graphics: 'extreme' })).toBeNull();
    expect(sanitizeSettings({ ...ok, musicVolume: 2 })).toBeNull();
  });
  it('clamps remote values and drops unknown keys', () => {
    const r = sanitizeRuntime({ run_speed_scale: 5, coin_reward_multiplier: -1, starting_hearts: 2.6, evil: 1, daily_rewards: [{ day: 2 }] });
    expect(r.run_speed_scale).toBe(1.2);
    expect(r.coin_reward_multiplier).toBe(0.5);
    expect(r.starting_hearts).toBe(3);
    expect(r.daily_rewards).toEqual(DEFAULT_RUNTIME.daily_rewards);
    expect((r as unknown as Record<string, unknown>).evil).toBeUndefined();
    expect(r.leaderboard_enabled).toBe(false);
  });
});
