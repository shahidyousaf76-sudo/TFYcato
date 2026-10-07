import { assertFails, assertSucceeds, initializeTestEnvironment, RulesTestEnvironment } from '@firebase/rules-unit-testing';
import { doc, getDoc, getDocs, collection, query, where, setDoc, updateDoc } from 'firebase/firestore';
import { setLogLevel } from 'firebase/firestore';
setLogLevel('error'); // expected permission-denied writes would otherwise flood the output
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { newPlayer } from '../../src/domain/player';

let env: RulesTestEnvironment;
const anon = (uid: string) => env.authenticatedContext(uid, { firebase: { sign_in_provider: 'anonymous' } }).firestore();
const admin = () => env.authenticatedContext('admin1', { role: 'admin', email: 'a@tfy.test', firebase: { sign_in_provider: 'password' } }).firestore();

beforeAll(async () => {
  env = await initializeTestEnvironment({
    projectId: 'demo-tfy-cat',
    firestore: { rules: readFileSync(resolve(__dirname, '../../../firestore.rules'), 'utf8'), host: '127.0.0.1', port: 8080 },
  });
});
afterAll(async () => { await env.cleanup(); });
beforeEach(async () => {
  await env.clearFirestore();
  await env.withSecurityRulesDisabled(async (ctx) => {
    const db = ctx.firestore();
    await setDoc(doc(db, 'players/kid1'), newPlayer('kid1', 'Kitty4821', 1));
    await setDoc(doc(db, 'players/kid2'), newPlayer('kid2', 'JumpCat245', 1));
    await setDoc(doc(db, 'levels/level_01'), { number: 1 });
    await setDoc(doc(db, 'levelStats/level_01'), { completions: 1 });
    await setDoc(doc(db, 'leaderboards/level_01/entries/kid1'), { uid: 'kid1', username: 'Kitty4821', score: 10, hidden: false });
    await setDoc(doc(db, 'leaderboards/level_01/entries/kid2'), { uid: 'kid2', username: 'JumpCat245', score: 99, hidden: true });
  });
});

const settings = { music: false, sound: true, vibration: true, graphics: 'high', language: 'da', musicVolume: 0.4, soundVolume: 1, reduceMotion: false };

describe('players', () => {
  it('a child can read only their own profile', async () => {
    await assertSucceeds(getDoc(doc(anon('kid1'), 'players/kid1')));
    await assertFails(getDoc(doc(anon('kid1'), 'players/kid2')));
    await assertFails(getDocs(collection(anon('kid1'), 'players')));
  });
  it('a child can change settings but never currency or progress', async () => {
    const db = anon('kid1');
    await assertSucceeds(updateDoc(doc(db, 'players/kid1'), { settings, updatedAt: 2 }));
    await assertFails(updateDoc(doc(db, 'players/kid1'), { coins: 999999, updatedAt: 3 }));
    await assertFails(updateDoc(doc(db, 'players/kid1'), { currentLevel: 10, updatedAt: 3 }));
    await assertFails(updateDoc(doc(db, 'players/kid1'), { 'unlockedCats.space_cat': true, updatedAt: 3 }));
    await assertFails(updateDoc(doc(db, 'players/kid1'), { username: 'Emma2015', updatedAt: 3 }));
  });
  it('rejects malformed settings', async () => {
    const db = anon('kid1');
    await assertFails(updateDoc(doc(db, 'players/kid1'), { settings: { ...settings, graphics: 'extreme' }, updatedAt: 2 }));
    await assertFails(updateDoc(doc(db, 'players/kid1'), { settings: { ...settings, extra: 1 }, updatedAt: 2 }));
    await assertFails(updateDoc(doc(db, 'players/kid1'), { settings: { ...settings, musicVolume: 3 }, updatedAt: 2 }));
    await assertFails(updateDoc(doc(db, 'players/kid1'), { settings: { ...settings, gameSpeed: 'turbo' }, updatedAt: 2 }));
    await assertSucceeds(updateDoc(doc(db, 'players/kid1'), { settings: { ...settings, gameSpeed: 'slow' }, updatedAt: 3 }));
  });
  it('profiles can only be created by the server', async () => {
    await assertFails(setDoc(doc(anon('kid3'), 'players/kid3'), newPlayer('kid3', 'Kitty1234', 1)));
  });
  it('admins do not get to browse children\'s profiles', async () => {
    await assertFails(getDoc(doc(admin(), 'players/kid1')));
  });
});

describe('content and admin data', () => {
  it('game content is public and read-only', async () => {
    await assertSucceeds(getDoc(doc(env.unauthenticatedContext().firestore(), 'levels/level_01')));
    await assertFails(setDoc(doc(anon('kid1'), 'levels/level_01'), { number: 1, enabled: false }));
    await assertFails(setDoc(doc(admin(), 'levels/level_01'), { number: 1, enabled: false })); // admins write through audited functions
  });
  it('level statistics are admin-only; a role claim on an anonymous account does not count', async () => {
    await assertSucceeds(getDoc(doc(admin(), 'levelStats/level_01')));
    await assertFails(getDoc(doc(anon('kid1'), 'levelStats/level_01')));
    const sneaky = env.authenticatedContext('kid1', { role: 'admin', firebase: { sign_in_provider: 'anonymous' } }).firestore();
    await assertFails(getDoc(doc(sneaky, 'levelStats/level_01')));
  });
  it('hidden leaderboard entries are invisible to players', async () => {
    const db = anon('kid1');
    await assertSucceeds(getDocs(query(collection(db, 'leaderboards/level_01/entries'), where('hidden', '==', false))));
    await assertFails(getDocs(collection(db, 'leaderboards/level_01/entries')));
    await assertFails(getDoc(doc(db, 'leaderboards/level_01/entries/kid2')));
    await assertSucceeds(getDoc(doc(admin(), 'leaderboards/level_01/entries/kid2')));
  });
  it('server-only collections are closed', async () => {
    await assertFails(getDoc(doc(anon('kid1'), 'usernames/kitty4821')));
    await assertFails(getDoc(doc(anon('kid1'), 'runs/kid1/results/x')));
  });
});

