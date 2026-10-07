// TFY Cat Adventure — game rules for the web prototype.
// Port of unity-game/Assets/Scripts/Domain (LevelPlanner, Rules, Progress) and firebase/functions/src/domain
// (daily rewards, shop, mystery box). Same PRNG (mulberry32) and same algorithm, so levels match the Unity game.
(function (global) {
  'use strict';
  const LANE_WIDTH = 2.2, LINE_SPACING = 2.0;
  // The C# planner uses 32-bit floats; f() rounds every step the same way so layouts are identical to the Unity game.
  const f = Math.fround;

  class Mulberry32 {
    constructor(seed) { this.s = seed >>> 0; }
    next() {
      this.s = (this.s + 0x6D2B79F5) >>> 0;
      let t = this.s;
      t = Math.imul(t ^ (t >>> 15), t | 1);
      t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    }
    range(min, max) { return min + Math.floor(this.next() * (max - min)); }
    pick(list) { return list[this.range(0, list.length)]; }
  }

  function totals(level) {
    const t = { length: 0, coins: 0, fish: 0, gems: 0, starTokens: 0, mysteryBoxes: 0, powerUps: 0 };
    for (const s of level.sections) { t.length += s.length; t.coins += s.coins; t.fish += s.fish; t.gems += s.gems; t.starTokens += s.starTokens; t.mysteryBoxes += s.mysteryBoxes; t.powerUps += s.powerUps; }
    return t;
  }

  function speedAt(level, distance, scale) {
    const len = f(Math.max(1, totals(level).length));
    let k = f(Math.min(1, Math.max(0, f(distance / len))));
    k = f(f(k * k) * f(3 - f(2 * k)));
    const base = f(level.baseSpeed), max = f(level.maxSpeed);
    return f(f(base + f(f(max - base) * k)) * f(scale));
  }

  const obstacleDef = (content, id) => content.obstacles.find((o) => o.id === id);

  // ------------------------------------------------------------------ planner (see LevelPlanner.cs for the fairness rules)
  function plan(level, content, runtime) {
    runtime = runtime || { run_speed_scale: 1, difficulty_density_scale: 1 };
    const rng = new Mulberry32(level.seed);
    const p = { level, items: [], rows: [], checkpoints: [], sectionStarts: [], length: 0, finishZ: 0 };
    let S = 0, prevEnd = -1; const easy = level.difficulty === 'easy';
    level.sections.forEach((sec, si) => {
      const E = f(S + sec.length);
      p.sectionStarts.push(S);
      const rows = buildRows(level, content, runtime, sec, si, S, E, easy, rng, prevEnd);
      p.rows.push(...rows);
      const lastObstacle = [...rows].reverse().find((r) => r.obstacleId);
      if (lastObstacle) prevEnd = nextRowStart(level, runtime, lastObstacle, easy);
      for (const r of rows) {
        if (!r.obstacleId) continue;
        if (r.fullWidth) {
          p.items.push({ kind: 'obstacle', id: r.obstacleId, lane: -1, z: r.z, length: r.depth, phase: 0, section: si });
          if (r.obstacleId === 'broken_bridge') p.items.push({ kind: 'longjump', id: 'long_jump', lane: -1, z: f(r.z - 7), length: 7, section: si });
        } else for (let l = 0; l < 3; l++) if (r.blocked[l]) p.items.push({ kind: 'obstacle', id: r.obstacleId, lane: l, z: r.z, length: r.depth, phase: f(rng.next()), section: si });
      }
      if (sec.checkpointAfter) { p.checkpoints.push(f(E - 5)); p.items.push({ kind: 'checkpoint', id: 'checkpoint', lane: -1, z: f(E - 5), section: si }); }
      S = E;
    });
    p.length = S; p.finishZ = S;
    p.items.push({ kind: 'finish', id: 'finish', lane: -1, z: S, section: level.sections.length - 1 });
    placeCollectibles(p, level, rng);
    const KIND = ['obstacle', 'coin', 'fish', 'gem', 'star', 'box', 'powerup', 'longjump', 'checkpoint', 'finish']; // same order as the C# enum
    p.items.sort((a, b) => a.z - b.z || KIND.indexOf(a.kind) - KIND.indexOf(b.kind) || a.lane - b.lane || (a.y || 0) - (b.y || 0));
    return p;
  }

  const rowEnd = (r) => f(r.z + r.depth);
  function openLane(r, preferred) {
    if (!r.blocked[preferred]) return preferred;
    for (let d = 1; d < 3; d++) { if (preferred - d >= 0 && !r.blocked[preferred - d]) return preferred - d; if (preferred + d < 3 && !r.blocked[preferred + d]) return preferred + d; }
    return 1;
  }

  /** Earliest start of the next row after r (speed-based reaction gap; extra room after bridges and walls). */
  function nextRowStart(level, rt, r, easy) {
    const speed = speedAt(level, r.z, rt.run_speed_scale);
    const gap = f(Math.max(11, f(speed * f(1.35))) + (easy ? 3 : 0));
    return f(f(f(rowEnd(r) + gap) + (r.obstacleId === 'broken_bridge' ? 4 : 0)) + (r.obstacleId === 'wall' ? 2 : 0));
  }

  function buildRows(level, content, rt, sec, si, S, E, easy, rng, prevEnd) {
    const rows = [];
    const noObstacles = sec.kind === 'start' || sec.kind === 'finish' || !sec.obstacles || sec.obstacles.length === 0;
    const marginIn = si === 0 ? 24 : 9, marginOut = sec.checkpointAfter ? 12 : 5;
    const density = f(Math.min(0.95, f(f(sec.density) * f(rt.difficulty_density_scale))));
    const isChallenge = sec.kind === 'challenge';
    let cycle = 0; const weave = { v: rng.range(0, 3) };
    let z = Math.max(f(S + marginIn), prevEnd); // sections never squeeze rows closer than the reaction gap
    for (;;) {
      const speed = speedAt(level, z, rt.run_speed_scale);
      const gap = f(Math.max(11, f(speed * f(1.35))) + (easy ? 3 : 0));
      const row = { z, depth: 1, blocked: [false, false, false], obstacleId: null, fullWidth: false, jumpable: false, section: si };
      let has = !noObstacles && (isChallenge || rng.next() < density);
      if (has) {
        const id = isChallenge ? sec.obstacles[cycle++ % sec.obstacles.length] : rng.pick(sec.obstacles);
        const def = obstacleDef(content, id);
        if (!def) has = false;
        else {
          row.obstacleId = id; row.depth = f(def.depth);
          if (id === 'broken_bridge') { row.z = f(z + 8); row.fullWidth = true; row.jumpable = true; row.blocked = [true, true, true]; }
          else chooseLanes(row, def, sec, isChallenge, rng, weave);
        }
      }
      if (f(rowEnd(row) + marginOut) > E) break;
      rows.push(row);
      z = row.obstacleId ? nextRowStart(level, rt, row, easy) : f(rowEnd(row) + gap);
    }
    return rows;
  }

  function chooseLanes(row, def, sec, isChallenge, rng, weave) {
    const id = def.id;
    const canFull = id === 'log' || id === 'branch';
    const fullChance = isChallenge && (sec.challenge === 'log_hop' || sec.challenge === 'branch_duck') ? 0.75 : 0.12;
    row.jumpable = def.clearance !== 'wall';
    if (canFull && rng.next() < fullChance) { row.fullWidth = true; row.blocked = [true, true, true]; return; }
    const moving = id === 'moving_critter';
    let blockedCount = !moving && rng.next() < f(sec.twoLaneChance) ? 2 : 1;
    let open;
    if (isChallenge && (sec.challenge === 'wall_weave' || sec.challenge === 'barrel_alley' || sec.challenge === 'combo_wall_critter')) { weave.v = (weave.v + 1 + rng.range(0, 2)) % 3; open = weave.v; blockedCount = 2; }
    else open = rng.range(0, 3);
    if (blockedCount === 2) { for (let l = 0; l < 3; l++) row.blocked[l] = l !== open; }
    else { const b = (open + 1 + rng.range(0, 2)) % 3; row.blocked[b] = true; }
  }

  function placeCollectibles(p, level, rng) {
    level.sections.forEach((sec, si) => {
      const S = p.sectionStarts[si], E = f(S + sec.length);
      const rows = p.rows.filter((r) => r.section === si).sort((a, b) => a.z - b.z);
      const regions = buildRegions(p, rows, si, S, E);
      const arcs = rows.filter((r) => r.obstacleId && r.jumpable).map((r) => ({ row: r, lane: arcLane(r, rng), coins: 0, section: si, hasItem: false }));
      const specials = [];
      for (let i = 0; i < sec.starTokens; i++) specials.push(['star', 'star']);
      for (let i = 0; i < sec.gems; i++) specials.push(['gem', 'gem']);
      for (let i = 0; i < sec.mysteryBoxes; i++) specials.push(['box', 'mystery_box']);
      for (let i = 0; i < sec.fish; i++) specials.push(['fish', 'fish']);
      for (let i = 0; i < sec.powerUps; i++) specials.push(['powerup', pickPowerUp(level.number, rng)]);
      const itemRegions = spread(regions, specials.length);
      specials.forEach(([kind, id], i) => {
        const arc = (kind === 'gem' || (kind === 'star' && sec.kind === 'challenge')) ? arcs.find((a) => !a.hasItem && a.row.obstacleId !== 'broken_bridge') : null;
        if (arc) { arc.hasItem = true; p.items.push({ kind, id, lane: arc.lane, z: f(arc.row.z + f(arc.row.depth * 0.5)), y: f(topOfArc(arc.row) + 0.5), section: si }); return; }
        const reg = itemRegions[i];
        if (reg) { reg.usedByItem = true; reg.itemZ = f(f(reg.start + reg.end) * 0.5); p.items.push({ kind, id, lane: reg.lane, z: reg.itemZ, y: 1.0, section: si }); }
        else p.items.push({ kind, id, lane: 1, z: f(S + f(f(sec.length * (i + 1)) / (specials.length + 1))), y: 2.0, section: si });
      });
      let remaining = sec.coins;
      for (let i = 0; i < arcs.length && remaining > 0; i += 2) { if (arcs[i].hasItem) continue; arcs[i].coins = Math.min(5, remaining); remaining -= arcs[i].coins; }
      for (const reg of regions) { if (remaining <= 0) break; const cap = capacity(reg); reg.coins = Math.min(Math.min(5, cap), remaining); remaining -= reg.coins; }
      for (const reg of regions) { if (remaining <= 0) break; const extra = Math.min(capacity(reg) - reg.coins, remaining); reg.coins += extra; remaining -= extra; }
      for (let i = 1; i < arcs.length && remaining > 0; i += 2) { if (arcs[i].hasItem || arcs[i].coins > 0) continue; arcs[i].coins = Math.min(5, remaining); remaining -= arcs[i].coins; }
      if (remaining > 0) { for (let i = 0; i < remaining; i++) p.items.push({ kind: 'coin', id: 'coin', lane: 1, z: f(f(S + 3) + f(i * f(1.2))), y: f(2.2), section: si }); remaining = 0; }
      arcs.forEach((a) => emitArc(p, a));
      regions.forEach((r) => { for (const z of linePositions(r, r.coins)) p.items.push({ kind: 'coin', id: 'coin', lane: r.lane, z, y: 0.8, section: r.section }); });
    });
  }

  function buildRegions(p, rows, si, S, E) {
    const regions = []; let cursor = f(S + (si === 0 ? 8 : 3)); let prevOpen = 1;
    for (const r of rows) {
      if (!r.obstacleId) continue;
      const end = f(r.z - (r.obstacleId === 'broken_bridge' ? 9 : 4.5));
      const lane = openLane(r, prevOpen);
      if (f(end - cursor) >= 4) regions.push({ start: cursor, end, lane, section: si, usedByItem: false, coins: 0, itemZ: 0 });
      cursor = f(rowEnd(r) + (r.obstacleId === 'broken_bridge' ? 5 : 3.5));
      prevOpen = lane;
    }
    let tailEnd = f(E - 3);
    for (const cp of p.checkpoints) if (cp > cursor && cp < tailEnd) tailEnd = Math.min(tailEnd, f(cp - 2));
    if (f(tailEnd - cursor) >= 4) regions.push({ start: cursor, end: tailEnd, lane: prevOpen, section: si, usedByItem: false, coins: 0, itemZ: 0 });
    return regions;
  }

  function linePositions(r, count) {
    const list = []; let z = r.start;
    if (!r.usedByItem) z = f(z + Math.max(0, f(f(f(r.end - r.start) - f((count - 1) * LINE_SPACING)) * 0.5)));
    while (list.length < count && z <= f(r.end + f(0.001))) {
      if (r.usedByItem && Math.abs(f(z - r.itemZ)) < 1.5) { z = f(r.itemZ + 1.5); continue; }
      list.push(z); z = f(z + LINE_SPACING);
    }
    return list;
  }
  const capacity = (r) => linePositions(r, 8).length;

  function spread(regions, n) {
    if (n === 0 || regions.length === 0) return [];
    const out = [];
    for (let i = 0; i < n && i < regions.length; i++) out.push(regions[Math.floor((i + 0.5) * regions.length / Math.min(n, regions.length))]);
    return [...new Set(out)];
  }

  function arcLane(r, rng) {
    if (r.fullWidth) return 1;
    const lanes = []; for (let l = 0; l < 3; l++) if (r.blocked[l]) lanes.push(l);
    return lanes.length ? rng.pick(lanes) : 1;
  }
  const topOfArc = (r) => (r.obstacleId === 'barrel' ? 2.8 : r.obstacleId === 'broken_bridge' ? 3.0 : 2.4);
  function emitArc(p, a) {
    if (a.coins <= 0) return;
    const mid = f(a.row.z + f(a.row.depth * 0.5)), top = f(topOfArc(a.row)), span = f(a.row.obstacleId === 'broken_bridge' ? 5 : 2.6);
    for (let i = 0; i < a.coins; i++) { const k = a.coins === 1 ? 0 : f(f(f(i / f(a.coins - 1)) * 2) - 1); p.items.push({ kind: 'coin', id: 'coin', lane: a.lane, z: f(mid + f(k * span)), y: f(f(0.9) + f(f(top - f(0.9)) * f(1 - f(k * k)))), section: a.section }); }
  }
  function pickPowerUp(levelNumber, rng) {
    const pool = levelNumber < 4 ? ['magnet', 'shield', 'double_coins'] : ['magnet', 'shield', 'super_jump', 'speed_boost', 'double_coins'];
    return rng.pick(pool);
  }

  // ------------------------------------------------------------------ rules (same as server)
  function stars(level, coins, starTokens) {
    const t = totals(level);
    if (coins >= level.threeStarCoins && starTokens >= t.starTokens) return 3;
    if (coins >= level.targetCoins) return 2;
    return 1;
  }
  function score(level, e, run) {
    return Math.round(totals(level).length * e.scorePerMetre + run.coinValue * e.scorePerCoin + run.fish * e.scorePerFish + run.gems * e.scorePerGem + run.starTokens * e.scorePerStarToken + run.heartsLeft * e.scorePerHeartLeft);
  }

  function newPlayer(username) {
    return {
      username, createdAt: Date.now(), currentLevel: 1, coins: 0, stars: 0, fish: 0, gems: 0, highScore: 0, selectedCat: 'orange_cat',
      equipped: { hat: '', glasses: '', shoes: '', backpack: '', cape: '', scarf: '', trail: '' },
      settings: { music: true, sound: true, vibration: true, graphics: 'auto', language: 'en', musicVolume: 0.8, soundVolume: 1, reduceMotion: false, gameSpeed: 'normal' },
      unlockedCats: { orange_cat: true }, unlockedItems: {}, achievements: {}, levels: {},
      stats: { runs: 0, jumps: 0, coinsCollected: 0, levelsCompleted: 0, fishCollected: 0, gemsFound: 0, itemsBought: 0, dailyClaims: 0, perfectLevels: 0 },
      daily: { lastClaimDay: null, lastClaimAt: 0, nextDay: 1 }, pendingBoxes: 0,
    };
  }
  const owns = (p, id) => !!(p.unlockedCats[id] || p.unlockedItems[id]);
  const statValue = (p, stat) => (stat === 'stars' ? p.stars : p.stats[stat] || 0);
  function awardAchievements(p, defs) {
    const earned = [];
    for (const a of defs) { if (p.achievements[a.id] || statValue(p, a.stat) < a.target) continue; p.achievements[a.id] = true; p.coins += a.rewardCoins; p.gems += a.rewardGems; earned.push(a); }
    return earned;
  }

  function applyLevelResult(p, level, run, content, multiplier) {
    const e = content.economy;
    const lp = Object.assign({ completed: false, bestStars: 0, bestScore: 0, bestCoins: 0, bestStarTokens: 0, gemsFound: 0, boxesFound: 0, attempts: 0, completions: 0, bestTimeSec: 0 }, p.levels[level.id]);
    const st = stars(level, run.coins, run.starTokens), sc = score(level, e, run), first = !lp.completed, newStars = Math.max(0, st - lp.bestStars);
    const m = Math.min(3, Math.max(0.5, multiplier || 1));
    const coinsEarned = Math.round(run.coinValue * m);
    const bonus = Math.round(((first ? e.firstClearBonusBase + e.firstClearBonusPerLevel * level.number : 0) + newStars * e.newStarBonus) * m);
    const gemsEarned = Math.max(0, run.gems - lp.gemsFound), boxesEarned = Math.max(0, run.mysteryBoxes - lp.boxesFound);
    lp.attempts += 1 + (run.failedAttempts || 0); lp.completions++; lp.completed = true;
    lp.bestStars = Math.max(lp.bestStars, st); const newBest = sc > lp.bestScore; lp.bestScore = Math.max(lp.bestScore, sc);
    lp.bestCoins = Math.max(lp.bestCoins, run.coins); lp.bestStarTokens = Math.max(lp.bestStarTokens, run.starTokens);
    lp.gemsFound = Math.max(lp.gemsFound, run.gems); lp.boxesFound = Math.max(lp.boxesFound, run.mysteryBoxes);
    lp.bestTimeSec = lp.bestTimeSec > 0 ? Math.min(lp.bestTimeSec, run.durationSec) : run.durationSec;
    p.levels[level.id] = lp;
    p.coins += coinsEarned + bonus; p.fish += run.fish; p.gems += gemsEarned; p.pendingBoxes += boxesEarned; p.highScore = Math.max(p.highScore, sc);
    p.stars = Object.values(p.levels).reduce((a, l) => a + (l.bestStars || 0), 0);
    const next = content.levels.filter((l) => l.enabled && l.number > level.number).sort((a, b) => a.number - b.number)[0];
    let unlocked = null; if (next && next.number > p.currentLevel) { p.currentLevel = next.number; unlocked = next.number; }
    p.stats.runs++; p.stats.jumps += run.jumps; p.stats.coinsCollected += run.coins; p.stats.fishCollected += run.fish; p.stats.gemsFound += gemsEarned;
    p.stats.levelsCompleted = Object.values(p.levels).filter((l) => l.completed).length;
    p.stats.perfectLevels = Object.values(p.levels).filter((l) => l.bestStars >= 3).length;
    const achievements = awardAchievements(p, content.achievements);
    return { stars: st, score: sc, firstCompletion: first, newBestScore: newBest, coinsEarned, bonusCoins: bonus, gemsEarned, mysteryBoxesEarned: boxesEarned, unlockedLevel: unlocked, achievements, best: lp.bestScore };
  }

  const dayKey = (d = new Date()) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  function dailyStatus(p, rewards) {
    const today = dayKey(); const claimedToday = p.daily.lastClaimDay === today;
    const cooldown = p.daily.lastClaimAt + 18 * 3600000;
    const day = ((p.daily.nextDay - 1) % rewards.length) + 1;
    return { today, claimedToday, canClaim: !claimedToday && Date.now() >= cooldown, day, reward: rewards[day - 1] };
  }
  function claimDaily(p, rewards, content) {
    const s = dailyStatus(p, rewards); if (!s.canClaim) return null;
    const r = s.reward; const claim = { day: s.day, label: r.label, coins: 0, gems: 0, itemId: null };
    if (r.type === 'coins') claim.coins = r.amount; else if (r.type === 'gems') claim.gems = r.amount;
    else { const it = content.shop.find((i) => i.id === r.itemId); if (!it || owns(p, it.id)) { claim.coins = 200; claim.label = '200 Coins'; } else { claim.itemId = it.id; (it.slot === 'cat' ? p.unlockedCats : p.unlockedItems)[it.id] = true; } }
    p.coins += claim.coins; p.gems += claim.gems; p.daily = { lastClaimDay: s.today, lastClaimAt: Date.now(), nextDay: (s.day % rewards.length) + 1 }; p.stats.dailyClaims++;
    claim.achievements = awardAchievements(p, content.achievements);
    return claim;
  }
  function purchase(p, content, itemId) {
    const it = content.shop.find((i) => i.id === itemId);
    if (!it || !it.enabled) return { ok: false, message: 'Item not found.' };
    if (it.source !== 'shop') return { ok: false, message: 'This item is a special reward.' };
    if (owns(p, it.id)) return { ok: false, message: 'You already have this!' };
    if (it.unlockLevel > 0 && !(p.levels[`level_${String(it.unlockLevel).padStart(2, '0')}`] || {}).completed) return { ok: false, message: `Finish level ${it.unlockLevel} first!` };
    const gems = it.priceGems > 0;
    if (gems ? p.gems < it.priceGems : p.coins < it.priceCoins) return { ok: false, message: gems ? 'Not enough gems yet — look for gems in levels!' : 'Not enough coins yet — keep running!' };
    if (gems) p.gems -= it.priceGems; else p.coins -= it.priceCoins;
    (it.slot === 'cat' ? p.unlockedCats : p.unlockedItems)[it.id] = true; p.stats.itemsBought++;
    return { ok: true, item: it, achievements: awardAchievements(p, content.achievements) };
  }
  function openMysteryBox(p, content) {
    if (p.pendingBoxes <= 0) return null;
    p.pendingBoxes--;
    const table = content.mysteryBox, total = table.reduce((a, e) => a + e.weight, 0);
    let roll = Math.random() * total, entry = table[table.length - 1];
    for (const e of table) { roll -= e.weight; if (roll < 0) { entry = e; break; } }
    let reward = { type: entry.type, amount: entry.amount, label: entry.label, itemId: null };
    if (entry.type === 'item') {
      const cand = (entry.pool || []).filter((id) => !owns(p, id));
      if (!cand.length) reward = { type: 'coins', amount: 150, label: '150 Coins', itemId: null };
      else { const id = cand[Math.floor(Math.random() * cand.length)]; const it = content.shop.find((i) => i.id === id); (it.slot === 'cat' ? p.unlockedCats : p.unlockedItems)[id] = true; reward = { type: 'item', amount: 1, label: it.name, itemId: id }; }
    }
    if (reward.type === 'coins') p.coins += reward.amount; if (reward.type === 'gems') p.gems += reward.amount;
    return reward;
  }
  function generateUsername(prefixes) { return prefixes[Math.floor(Math.random() * prefixes.length)] + (100 + Math.floor(Math.random() * 9900)); }

  global.TFYRules = { LANE_WIDTH, Mulberry32, totals, speedAt, plan, stars, score, newPlayer, owns, applyLevelResult, dailyStatus, claimDaily, purchase, openMysteryBox, generateUsername, statValue };
  if (typeof module !== 'undefined') module.exports = global.TFYRules;
})(typeof window !== 'undefined' ? window : globalThis);
