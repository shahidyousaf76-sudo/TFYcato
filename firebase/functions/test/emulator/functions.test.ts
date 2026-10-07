// End-to-end against the emulators: anonymous sign-in → createPlayer → play → rewards → shop → daily reward.
import { deleteApp, initializeApp, FirebaseApp } from 'firebase/app';
import { connectAuthEmulator, getAuth, signInAnonymously, signOut } from 'firebase/auth';
import { connectFirestoreEmulator, doc, getDoc, getFirestore, updateDoc } from 'firebase/firestore';
import { setLogLevel } from 'firebase/firestore';
setLogLevel('error'); // expected permission-denied writes would otherwise flood the output
import { connectFunctionsEmulator, getFunctions, httpsCallable } from 'firebase/functions';
import { DEFAULT_CONTENT } from '../../src/content';
import { levelTotals } from '../../src/domain/levels';

let app: FirebaseApp;
const call = <T = any>(name: string, data?: unknown) => httpsCallable<unknown, T>(getFunctions(app, 'europe-west1'), name)(data).then(r => r.data);
const L = (n: number) => DEFAULT_CONTENT.levels[n - 1];
const runFor = (n: number, runId: string, o: Record<string, unknown> = {}) => {
  const t = levelTotals(L(n));
  return { runId, levelId: L(n).id, coins: t.coins, coinValue: t.coins, fish: t.fish, gems: t.gems, starTokens: 3, mysteryBoxes: t.mysteryBoxes, heartsLeft: 3, durationSec: t.length / L(n).baseSpeed, jumps: 20, checkpointsUsed: 0, failedAttempts: 1, ...o };
};

beforeAll(() => {
  app = initializeApp({ projectId: 'demo-tfy-cat', apiKey: 'demo-key' }, 'e2e');
  connectAuthEmulator(getAuth(app), 'http://127.0.0.1:9099', { disableWarnings: true });
  connectFirestoreEmulator(getFirestore(app), '127.0.0.1', 8080);
  connectFunctionsEmulator(getFunctions(app, 'europe-west1'), '127.0.0.1', 5001);
});
afterAll(async () => { await deleteApp(app); });

describe('player journey', () => {
  let uid = '';
  it('creates a player automatically and never twice', async () => {
    uid = (await signInAnonymously(getAuth(app))).user.uid;
    const first = await call('createPlayer', { username: 'Kitty4821' });
    expect(first.created).toBe(true);
    expect(first.player.username).toMatch(/^[A-Za-z]+\d{3,4}$/);
    expect(first.player).toMatchObject({ currentLevel: 1, selectedCat: 'orange_cat', unlockedCats: { orange_cat: true } });
    const again = await call('createPlayer', { username: 'JumpCat245' });
    expect(again.created).toBe(false);
    expect(again.player.username).toBe(first.player.username);
  });

  it('replaces an unsafe proposed username', async () => {
    const other = initializeApp({ projectId: 'demo-tfy-cat', apiKey: 'demo-key' }, 'other');
    connectAuthEmulator(getAuth(other), 'http://127.0.0.1:9099', { disableWarnings: true });
    connectFunctionsEmulator(getFunctions(other, 'europe-west1'), '127.0.0.1', 5001);
    await signInAnonymously(getAuth(other));
    const r = await httpsCallable<unknown, any>(getFunctions(other, 'europe-west1'), 'createPlayer')({ username: 'Emma Hansen' });
    expect(r.data.player.username).not.toContain('Emma');
    await deleteApp(other);
  });

  it('refuses a locked level', async () => {
    await expect(call('completeLevel', runFor(2, 'run_locked_2'))).rejects.toMatchObject({ code: 'functions/permission-denied' });
  });

  it('completes level 1, unlocks level 2 and grants rewards once (idempotent retry)', async () => {
    const first = await call('completeLevel', runFor(1, 'run_level1_a'));
    expect(first.replayed).toBe(false);
    expect(first.result).toMatchObject({ stars: 3, firstCompletion: true, unlockedLevel: 2 });
    const retry = await call('completeLevel', runFor(1, 'run_level1_a'));
    expect(retry.replayed).toBe(true);
    expect(retry.result.totals.coins).toBe(first.result.totals.coins);
    const snap = await getDoc(doc(getFirestore(app), `players/${uid}`));
    expect(snap.data()).toMatchObject({ currentLevel: 2, coins: first.result.totals.coins, stars: 3 });
  });

  it('rejects cheating and never trusts a sent score', async () => {
    await expect(call('completeLevel', runFor(1, 'run_cheat_1', { coins: 5000 }))).rejects.toMatchObject({ code: 'functions/invalid-argument' });
    await expect(call('completeLevel', runFor(1, 'run_cheat_2', { durationSec: 1 }))).rejects.toMatchObject({ code: 'functions/invalid-argument' });
    const r = await call('completeLevel', runFor(1, 'run_score_1', { score: 99999999 }));
    expect(r.result.score).toBeLessThan(10000);
  });

  it('syncs offline runs in order', async () => {
    const r = await call('syncOfflineRuns', { runs: [runFor(2, 'offline_l2_1'), runFor(3, 'offline_l3_1'), runFor(9, 'offline_l9_1')] });
    expect(r.results.map((x: any) => x.ok)).toEqual([true, true, false]); // level 9 is still locked
    expect(r.player.currentLevel).toBe(4);
    expect(r.player.pendingBoxes).toBe(levelTotals(L(3)).mysteryBoxes);
  });

  it('opens a mystery box on the server', async () => {
    const r = await call('openMysteryBox');
    expect(['coins', 'gems', 'item']).toContain(r.reward.type);
    await expect(call('openMysteryBox')).rejects.toMatchObject({ code: 'functions/failed-precondition' });
  });

  it('claims the daily reward once', async () => {
    const status = await call('getDailyReward', { utcOffsetMinutes: 120 });
    expect(status.status).toMatchObject({ canClaim: true, day: 1 });
    const claim = await call('claimDailyReward', { utcOffsetMinutes: 120 });
    expect(claim.claim.coins).toBe(100);
    await expect(call('claimDailyReward', { utcOffsetMinutes: 120 })).rejects.toMatchObject({ code: 'functions/failed-precondition' });
  });

  it('buys and equips from the Cat Shop', async () => {
    const bought = await call('purchaseItem', { itemId: 'white_cat' });
    expect(bought.paidCoins).toBe(500);
    await expect(call('purchaseItem', { itemId: 'white_cat' })).rejects.toMatchObject({ code: 'functions/already-exists' });
    await expect(call('updateLoadout', { selectedCat: 'space_cat' })).rejects.toMatchObject({ code: 'functions/permission-denied' });
    expect(await call('updateLoadout', { selectedCat: 'white_cat' })).toMatchObject({ selectedCat: 'white_cat' });
  });

  it('the client cannot give itself coins directly', async () => {
    await expect(updateDoc(doc(getFirestore(app), `players/${uid}`), { coins: 1e9, updatedAt: 1 })).rejects.toBeTruthy();
  });

  it('players cannot call admin functions', async () => {
    await expect(call('adminGetStats')).rejects.toMatchObject({ code: 'functions/permission-denied' });
    await expect(call('adminSetLevelEnabled', { levelId: 'level_01', enabled: false })).rejects.toMatchObject({ code: 'functions/permission-denied' });
  });

  it('deleteMyData removes the player', async () => {
    expect(await call('deleteMyData')).toEqual({ deleted: true });
    await signOut(getAuth(app));
  });
});
