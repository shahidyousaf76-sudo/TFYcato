import { ChallengeId, Difficulty, LevelDef, ObstacleId, SectionDef, TimeOfDay } from './types';

interface LevelSpec {
  n: number; name: string; world: string; difficulty: Difficulty; time: TimeOfDay;
  length: number; speed: [number, number];
  easy: ObstacleId[]; main: ObstacleId[]; medium: ObstacleId[];
  challenge: ChallengeId; challengeObstacles: ObstacleId[];
  density: [number, number, number];   // easy, obstacles, medium
  twoLane: number;
  fish: number; gems: number; box: number; powerUps: number;
  checkpoints: 2 | 3; tutorial?: boolean; tip: string;
}

const sec = (kind: SectionDef['kind'], length: number, o: Partial<SectionDef> = {}): SectionDef => ({
  kind, length: Math.round(length), obstacles: [], density: 0, twoLaneChance: 0,
  coins: 0, fish: 0, gems: 0, starTokens: 0, mysteryBoxes: 0, powerUps: 0, checkpointAfter: false, ...o,
});

/**
 * Every level follows the same shape:
 * START → easy → collectibles → obstacles → 🚩 → medium → (🚩 on long levels) → special challenge → 🚩 final → finish.
 */
function level(s: LevelSpec): LevelDef {
  const L = s.length;
  const fishSplit = [Math.ceil(s.fish / 2), Math.floor(s.fish / 2)];
  const sections: SectionDef[] = [
    sec('start', L * 0.06, { coins: 5 }),
    sec('easy', L * 0.15, { obstacles: s.easy, density: s.density[0], coins: 15 }),
    sec('collectibles', L * 0.13, { obstacles: s.easy.slice(0, 1), density: 0.25, coins: 30, fish: fishSplit[0], starTokens: 1, powerUps: 1 }),
    sec('obstacles', L * 0.17, { obstacles: s.main, density: s.density[1], twoLaneChance: s.twoLane * 0.6, coins: 15, checkpointAfter: true }),
    sec('medium', L * 0.2, {
      obstacles: s.medium, density: s.density[2], twoLaneChance: s.twoLane, coins: 20, fish: fishSplit[1], starTokens: 1,
      gems: s.gems > 1 ? 1 : 0, powerUps: s.powerUps > 1 ? 1 : 0, checkpointAfter: s.checkpoints === 3,
    }),
    sec('challenge', L * 0.21, {
      obstacles: s.challengeObstacles, density: 0.75, twoLaneChance: s.twoLane, coins: 20, starTokens: 1,
      gems: s.gems > 1 ? s.gems - 1 : s.gems, mysteryBoxes: s.box, challenge: s.challenge, checkpointAfter: true,
    }),
    sec('finish', L * 0.08, { coins: 10 }),
  ];
  const coins = sections.reduce((a, x) => a + x.coins, 0);
  return {
    id: `level_${String(s.n).padStart(2, '0')}`, number: s.n, name: s.name, world: s.world, difficulty: s.difficulty,
    timeOfDay: s.time, baseSpeed: s.speed[0], maxSpeed: s.speed[1],
    targetCoins: Math.round(coins * 0.55), threeStarCoins: Math.round(coins * 0.8), targetStars: 3,
    checkpointCount: s.checkpoints, seed: 1000 + s.n * 7919, enabled: true, tutorial: !!s.tutorial, tip: s.tip, sections,
  };
}

export const LEVELS: LevelDef[] = [
  // ---- Levels 1–3: EASY — logs, rocks, branches; teach the controls ----
  level({ n: 1, name: 'Sunny Park Stroll', world: 'park', difficulty: 'easy', time: 'morning', length: 460, speed: [7.5, 8.5],
    easy: ['log'], main: ['log', 'rock'], medium: ['log', 'rock', 'branch'], challenge: 'log_hop', challengeObstacles: ['log'],
    density: [0.45, 0.6, 0.6], twoLane: 0, fish: 3, gems: 0, box: 0, powerUps: 1, checkpoints: 2, tutorial: true,
    tip: 'Tip: Tap to jump over logs!' }),
  level({ n: 2, name: 'Flower Path', world: 'park', difficulty: 'easy', time: 'day', length: 500, speed: [8, 9],
    easy: ['log', 'rock'], main: ['log', 'rock', 'branch'], medium: ['rock', 'branch'], challenge: 'branch_duck', challengeObstacles: ['branch'],
    density: [0.5, 0.65, 0.65], twoLane: 0.1, fish: 4, gems: 1, box: 0, powerUps: 1, checkpoints: 2,
    tip: 'Tip: Swipe down to slide under branches!' }),
  level({ n: 3, name: 'Forest Gate', world: 'forest', difficulty: 'easy', time: 'day', length: 540, speed: [8.4, 9.4],
    easy: ['log', 'rock'], main: ['log', 'rock', 'branch'], medium: ['log', 'rock', 'branch'], challenge: 'coin_climb', challengeObstacles: ['log', 'rock'],
    density: [0.55, 0.7, 0.7], twoLane: 0.15, fish: 4, gems: 1, box: 1, powerUps: 1, checkpoints: 2,
    tip: 'Tip: Follow the coins — they show you where to jump!' }),
  // ---- Levels 4–6: MEDIUM — barrels, walls, faster, more coins ----
  level({ n: 4, name: 'Whispering Woods', world: 'forest', difficulty: 'medium', time: 'day', length: 600, speed: [9, 10],
    easy: ['log', 'rock'], main: ['log', 'barrel', 'branch'], medium: ['barrel', 'rock', 'branch'], challenge: 'barrel_alley', challengeObstacles: ['barrel'],
    density: [0.55, 0.7, 0.75], twoLane: 0.25, fish: 5, gems: 1, box: 0, powerUps: 2, checkpoints: 2,
    tip: 'Tip: Swipe left or right to go around barrels!' }),
  level({ n: 5, name: 'Busy Streets', world: 'city', difficulty: 'medium', time: 'day', length: 640, speed: [9.4, 10.6],
    easy: ['rock', 'barrel'], main: ['barrel', 'wall', 'branch'], medium: ['wall', 'barrel', 'log'], challenge: 'wall_weave', challengeObstacles: ['wall'],
    density: [0.6, 0.7, 0.75], twoLane: 0.3, fish: 5, gems: 2, box: 1, powerUps: 2, checkpoints: 2,
    tip: 'Tip: Double tap to jump higher over walls!' }),
  level({ n: 6, name: 'City Lights', world: 'city', difficulty: 'medium', time: 'sunset', length: 680, speed: [9.8, 11.2],
    easy: ['barrel', 'log'], main: ['wall', 'barrel', 'branch'], medium: ['wall', 'barrel', 'rock', 'branch'], challenge: 'mixed_medium', challengeObstacles: ['wall', 'barrel', 'branch'],
    density: [0.6, 0.75, 0.8], twoLane: 0.35, fish: 5, gems: 2, box: 0, powerUps: 2, checkpoints: 2,
    tip: 'Tip: Grab a shield to stay safe from one bump!' }),
  // ---- Levels 7–10: HARD — broken bridges, falling rocks, moving obstacles, longer jumps, faster ----
  level({ n: 7, name: 'Cliffside Bridges', world: 'mountains', difficulty: 'hard', time: 'day', length: 740, speed: [10.2, 11.6],
    easy: ['rock', 'barrel'], main: ['wall', 'barrel', 'broken_bridge'], medium: ['broken_bridge', 'rock', 'wall'], challenge: 'bridge_leap', challengeObstacles: ['broken_bridge', 'log'],
    density: [0.6, 0.72, 0.78], twoLane: 0.35, fish: 6, gems: 2, box: 1, powerUps: 2, checkpoints: 3,
    tip: 'Tip: Jump at the arrows to leap across broken bridges!' }),
  level({ n: 8, name: 'Rumbling Peaks', world: 'mountains', difficulty: 'hard', time: 'day', length: 780, speed: [10.6, 12],
    easy: ['rock', 'log'], main: ['falling_rocks', 'barrel', 'wall'], medium: ['falling_rocks', 'broken_bridge', 'branch'], challenge: 'rock_fall', challengeObstacles: ['falling_rocks'],
    density: [0.6, 0.72, 0.78], twoLane: 0.35, fish: 6, gems: 2, box: 0, powerUps: 2, checkpoints: 3,
    tip: 'Tip: Shaking rocks will fall — watch the shadow and move!' }),
  level({ n: 9, name: 'Crabby Coast', world: 'beach', difficulty: 'hard', time: 'sunset', length: 820, speed: [11, 12.4],
    easy: ['rock', 'barrel'], main: ['moving_critter', 'barrel', 'wall'], medium: ['moving_critter', 'broken_bridge', 'rock'], challenge: 'critter_parade', challengeObstacles: ['moving_critter'],
    density: [0.62, 0.74, 0.8], twoLane: 0.4, fish: 7, gems: 2, box: 1, powerUps: 2, checkpoints: 3,
    tip: 'Tip: Crabs walk side to side — wait, then dodge!' }),
  level({ n: 10, name: 'Treasure Beach', world: 'beach', difficulty: 'hard', time: 'sunset', length: 880, speed: [11.4, 12.8],
    easy: ['barrel', 'log'], main: ['falling_rocks', 'moving_critter', 'wall'], medium: ['broken_bridge', 'moving_critter', 'barrel', 'branch'], challenge: 'grand_finale', challengeObstacles: ['broken_bridge', 'falling_rocks', 'moving_critter'],
    density: [0.62, 0.76, 0.82], twoLane: 0.4, fish: 8, gems: 3, box: 1, powerUps: 3, checkpoints: 3,
    tip: 'Tip: Use everything you learned — you can do it!' }),
  // ---- Level 11+: combinations, raised gradually ----
  level({ n: 11, name: 'Night Park Parade', world: 'park', difficulty: 'combo', time: 'night', length: 900, speed: [11.6, 13],
    easy: ['log', 'branch'], main: ['wall', 'moving_critter', 'log'], medium: ['barrel', 'branch', 'moving_critter'], challenge: 'combo_wall_critter', challengeObstacles: ['wall', 'moving_critter'],
    density: [0.64, 0.76, 0.82], twoLane: 0.4, fish: 8, gems: 2, box: 1, powerUps: 3, checkpoints: 3,
    tip: 'Tip: Walls and critters together — look for the open lane!' }),
  level({ n: 12, name: 'Mountain Storm Run', world: 'mountains', difficulty: 'combo', time: 'evening', length: 940, speed: [11.8, 13.2],
    easy: ['barrel', 'branch'], main: ['broken_bridge', 'falling_rocks', 'barrel'], medium: ['barrel', 'branch', 'falling_rocks'], challenge: 'combo_bridge_rocks', challengeObstacles: ['broken_bridge', 'falling_rocks'],
    density: [0.64, 0.76, 0.84], twoLane: 0.42, fish: 9, gems: 3, box: 1, powerUps: 3, checkpoints: 3,
    tip: 'Tip: Bridges and falling rocks — jump at the arrows, dodge the shadows!' }),
  ...comboLevels(),
  ...expertLevels(),
];

/**
 * Levels 31–100: the long adventure. Same section shape and fairness rules; difficulty keeps rising very slowly
 * (speed +0.0095 m/s and length +5 m per level, capped so the top speed stays ≤ 16 m/s) while worlds, times of day,
 * obstacle mixes and challenges rotate so every level feels different. Gems and fish grow a little as a reward for sticking with it.
 */
function expertLevels(): LevelDef[] {
  const worlds = ['park', 'forest', 'city', 'mountains', 'beach', 'snow'];
  const times: TimeOfDay[] = ['morning', 'day', 'sunset', 'day', 'evening', 'night', 'sunrise'];
  const names: Record<string, [string[], string[]]> = {
    park: [['Sunny', 'Breezy', 'Tulip', 'Kite', 'Puddle', 'Picnic', 'Daisy', 'Bunny'], ['Lane', 'Meadow', 'Garden', 'Hill']],
    forest: [['Mossy', 'Acorn', 'Fern', 'Hollow', 'Pinecone', 'Misty', 'Owl', 'Berry'], ['Trail', 'Grove', 'Woods', 'Glade']],
    city: [['Lantern', 'Taxi', 'Rooftop', 'Subway', 'Bakery', 'Clock', 'Bridge', 'Market'], ['Street', 'Avenue', 'Square', 'Plaza']],
    mountains: [['Frosty', 'Eagle', 'Crystal', 'Echo', 'Snowy', 'Boulder', 'Cloud', 'Glacier'], ['Pass', 'Peak', 'Ridge', 'Valley']],
    beach: [['Seashell', 'Coral', 'Lagoon', 'Surf', 'Starfish', 'Pirate', 'Sandy', 'Dolphin'], ['Shore', 'Bay', 'Cove', 'Dunes']],
    snow: [['Snowflake', 'Igloo', 'Penguin', 'Icicle', 'Sledge', 'Polar', 'Frosty', 'Aurora'], ['Slope', 'Fjord', 'Village', 'Lake']],
  };
  const combos: [ChallengeId, ObstacleId[]][] = [
    ['grand_finale', ['broken_bridge', 'falling_rocks', 'moving_critter', 'wall']], ['combo_wall_critter', ['wall', 'moving_critter']],
    ['combo_bridge_rocks', ['broken_bridge', 'falling_rocks']], ['critter_parade', ['moving_critter', 'barrel']],
    ['rock_fall', ['falling_rocks', 'wall']], ['bridge_leap', ['broken_bridge', 'log']], ['wall_weave', ['wall', 'barrel']],
    ['barrel_alley', ['barrel', 'branch']], ['mixed_medium', ['branch', 'moving_critter', 'barrel']], ['log_hop', ['log', 'branch']],
  ];
  const pools: ObstacleId[][] = [
    ['log', 'barrel', 'wall', 'moving_critter'], ['wall', 'moving_critter', 'branch', 'falling_rocks'], ['barrel', 'branch', 'falling_rocks', 'log'],
    ['broken_bridge', 'falling_rocks', 'barrel', 'wall'], ['moving_critter', 'log', 'wall', 'broken_bridge'], ['falling_rocks', 'moving_critter', 'branch', 'barrel'],
  ];
  const tips = [
    'Tip: Coins show the safest path — follow them!', 'Tip: Double tap over walls, slide under branches!', 'Tip: Jump at the arrows to cross bridges!',
    'Tip: Shaking rocks? Change lane early!', 'Tip: Critters move side to side — wait, then go!', 'Tip: Grab shields before the hard parts!',
    'Tip: Big stars are often high up — jump for them!', 'Tip: Flags save your progress — keep going!',
  ];
  const used: Record<string, number> = {};
  const levels: LevelDef[] = [];
  for (let n = 31; n <= 100; n++) {
    const k = n - 31, world = worlds[k % worlds.length];
    const i = (used[world] = (used[world] ?? -1) + 1);
    const [adj, place] = names[world];
    const name = n === 100 ? 'The Great Cat Adventure' : `${adj[i % adj.length]} ${place[Math.floor(i / adj.length) % place.length]}`;
    const [challenge, challengeObstacles] = n === 100 ? combos[0] : combos[k % combos.length];
    const base = Math.round(Math.min(14.6, 13.94 + (n - 30) * 0.0095) * 100) / 100;
    levels.push(level({
      n, name, world, difficulty: 'combo', time: times[k % times.length], length: 1205 + (n - 30) * 5,
      speed: [base, Math.round((base + 1.4) * 100) / 100],
      easy: ['log', 'rock', 'branch', 'barrel'].slice(k % 2, (k % 2) + 3) as ObstacleId[],
      main: pools[k % pools.length], medium: pools[(k + 3) % pools.length],
      challenge, challengeObstacles,
      density: [0.82 + (k % 3) * 0.01, 0.88 + (k % 2) * 0.02, 0.9 + (k % 3) * 0.01],
      twoLane: Math.min(0.55, 0.5 + (k % 5) * 0.01),
      fish: Math.min(16, 15 + Math.floor(k / 35)), gems: Math.min(6, 4 + Math.floor(k / 25)), box: 1, powerUps: 3, checkpoints: 3,
      tip: n === 100 ? 'Tip: The very last run — show everything you have learned!' : tips[k % tips.length],
    }));
  }
  return levels;
}

/**
 * Levels 13–30: combinations of everything learned, raised gradually (spec: "increase challenge gradually rather than suddenly").
 * Each step adds a little speed (+0.12 m/s), length (+15 m) and density; the planner's fairness rules still apply to every row.
 */
function comboLevels(): LevelDef[] {
  const pools: ObstacleId[][] = [
    ['log', 'barrel', 'wall'], ['wall', 'moving_critter', 'branch'], ['barrel', 'branch', 'falling_rocks'],
    ['broken_bridge', 'falling_rocks', 'barrel'], ['moving_critter', 'log', 'wall'], ['falling_rocks', 'moving_critter', 'branch'],
  ];
  const specs: [string, string, TimeOfDay, ChallengeId, ObstacleId[], string][] = [
    ['Firefly Forest', 'forest', 'night', 'log_hop', ['log', 'branch'], 'Tip: Follow the coin arcs over the logs!'],
    ['Rush Hour', 'city', 'day', 'combo_wall_critter', ['wall', 'moving_critter'], 'Tip: Walls and toy cars — find the open lane!'],
    ['Sunset Pier', 'beach', 'sunset', 'mixed_medium', ['barrel', 'branch'], 'Tip: Barrels: go around. Branches: slide under!'],
    ['Snowy Summit', 'mountains', 'morning', 'combo_bridge_rocks', ['broken_bridge', 'falling_rocks'], 'Tip: Jump at the arrows, dodge the shadows!'],
    ['Rainbow Meadow', 'park', 'day', 'critter_parade', ['moving_critter', 'log'], 'Tip: Watch the bouncy balls, then go!'],
    ['Owl Hollow', 'forest', 'evening', 'wall_weave', ['wall', 'barrel'], 'Tip: Weave left and right through the walls!'],
    ['Neon Avenue', 'city', 'night', 'grand_finale', ['broken_bridge', 'moving_critter', 'wall'], 'Tip: The city never sleeps — stay sharp!'],
    ['Coconut Cove', 'beach', 'day', 'rock_fall', ['falling_rocks'], 'Tip: Shaking palms drop coconuts — change lane!'],
    ['Glacier Gaps', 'mountains', 'day', 'bridge_leap', ['broken_bridge', 'barrel'], 'Tip: Long jumps all the way!'],
    ['Picnic Panic', 'park', 'sunset', 'barrel_alley', ['barrel', 'branch'], 'Tip: Swipe fast through the barrel alley!'],
    ['Mossy Maze', 'forest', 'day', 'combo_wall_critter', ['wall', 'moving_critter'], 'Tip: Hedgehogs and walls — wait for your moment!'],
    ['Skyline Sprint', 'city', 'sunset', 'combo_bridge_rocks', ['broken_bridge', 'falling_rocks'], 'Tip: Jump the gaps between the rooftops!'],
    ['Moonlit Bay', 'beach', 'night', 'critter_parade', ['moving_critter', 'barrel'], 'Tip: Crabs dance in the moonlight — time your jumps!'],
    ['Avalanche Alley', 'mountains', 'evening', 'rock_fall', ['falling_rocks', 'wall'], 'Tip: Listen for the rumble and move!'],
    ['Festival Park', 'park', 'night', 'grand_finale', ['broken_bridge', 'falling_rocks', 'moving_critter'], 'Tip: Everything at once — you can do it!'],
    ['Ancient Grove', 'forest', 'sunrise', 'mixed_medium', ['broken_bridge', 'branch', 'moving_critter'], 'Tip: Jump, slide and dodge in the old forest!'],
    ['Storm Harbour', 'beach', 'evening', 'combo_bridge_rocks', ['broken_bridge', 'falling_rocks', 'moving_critter'], 'Tip: Bridges, coconuts and crabs!'],
    ['Cat Hero Finale', 'mountains', 'sunrise', 'grand_finale', ['broken_bridge', 'falling_rocks', 'moving_critter', 'wall'], 'Tip: The final run — become a true Cat Hero!'],
  ];
  return specs.map(([name, world, time, challenge, challengeObstacles, tip], k) => {
    const n = 13 + k;
    const base = Math.round((11.9 + k * 0.12) * 100) / 100;
    return level({
      n, name, world, difficulty: 'combo', time, length: 950 + k * 15, speed: [base, Math.round((base + 1.4) * 100) / 100],
      easy: ['log', 'rock', 'branch', 'barrel'].slice(k % 2, (k % 2) + 3) as ObstacleId[],
      main: pools[k % pools.length], medium: [...new Set([...pools[(k + 2) % pools.length], ...pools[(k + 4) % pools.length].slice(0, 1)])] as ObstacleId[],
      challenge, challengeObstacles,
      density: [Math.min(0.85, 0.64 + k * 0.01), Math.min(0.9, 0.77 + k * 0.008), Math.min(0.92, 0.85 + k * 0.005)],
      twoLane: Math.min(0.52, 0.42 + k * 0.006),
      fish: 9 + Math.floor(k / 3), gems: 3 + (k >= 9 ? 1 : 0), box: 1, powerUps: 3, checkpoints: 3, tip,
    });
  });
}
