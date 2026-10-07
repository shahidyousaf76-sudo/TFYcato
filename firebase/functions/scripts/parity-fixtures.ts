// Generates shared/parity-fixtures.json: a sequence of runs applied with the server's applyLevelResult().
// The Unity domain tests replay the same runs with the C# port (Progress.Apply) and must get identical numbers.
import { writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { DEFAULT_CONTENT } from '../src/content';
import { levelTotals } from '../src/domain/levels';
import { newPlayer } from '../src/domain/player';
import { applyLevelResult } from '../src/domain/progress';
import { RunReport } from '../src/domain/runValidation';

const C = DEFAULT_CONTENT;
let seed = 12345;
const rnd = () => ((seed = (seed * 16807) % 2147483647) / 2147483647);
const int = (max: number) => Math.floor(rnd() * (max + 1));

const runs: (RunReport & { multiplier: number })[] = [];
let player = newPlayer('parity', 'Kitty4821', 1000);
const steps: unknown[] = [];

for (let i = 0; i < 250; i++) {
  const unlocked = C.levels.filter(l => l.number <= player.currentLevel);
  const level = rnd() < 0.7 ? unlocked[unlocked.length - 1] : unlocked[int(unlocked.length - 1)]; // mostly the newest level, so all 30 get covered
  const t = levelTotals(level);
  const coins = int(t.coins);
  const run = {
    runId: `parity_${i}`, levelId: level.id, coins, coinValue: coins + int(coins), fish: int(t.fish), gems: int(t.gems), starTokens: int(t.starTokens),
    mysteryBoxes: int(t.mysteryBoxes), heartsLeft: 1 + int(2), durationSec: t.length / level.baseSpeed, jumps: int(40), checkpointsUsed: int(2), failedAttempts: int(3), offline: false,
    multiplier: [1, 1, 1.5, 2, 0.75][int(4)],
  };
  const out = applyLevelResult(player, level, run, { levels: C.levels, economy: C.economy, achievements: C.achievements, coinMultiplier: run.multiplier, now: 2000 + i });
  player = out.player;
  runs.push(run);
  steps.push({ run, result: out.result, player: { coins: player.coins, gems: player.gems, fish: player.fish, stars: player.stars, currentLevel: player.currentLevel, highScore: player.highScore, pendingBoxes: player.pendingBoxes, achievements: Object.keys(player.achievements).sort() } });
}

const path = resolve(__dirname, '../../../shared/parity-fixtures.json');
writeFileSync(path, JSON.stringify({ steps }, null, 1) + '\n');
console.log('wrote', path, steps.length, 'steps; final level', player.currentLevel);
