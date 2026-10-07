import { ObstacleDef } from './types';

// Heights are tuned to the cat's jump: a single jump peaks at ~2.2 m, a double jump at ~3.6 m,
// sliding lowers the cat to 0.6 m. A long jump carries the cat ~12 m at level-10 speed.
export const OBSTACLES: ObstacleDef[] = [
  {
    id: 'log', name: 'Small log', emoji: '🪵', difficulty: 'easy', clearance: 'jump', requiredAction: 'Jump',
    height: 0.7, depth: 0.8, lanes: 1, warning: 'Bright log with a coin arc above it', warningDistance: 30, minLevel: 1,
  },
  {
    id: 'rock', name: 'Small rock', emoji: '🪨', difficulty: 'easy', clearance: 'jump', requiredAction: 'Jump',
    height: 0.8, depth: 1.0, lanes: 1, warning: 'Rounded rock, easy to spot', warningDistance: 30, minLevel: 1,
  },
  {
    id: 'branch', name: 'Tree branch', emoji: '🌳', difficulty: 'easy', clearance: 'jump_or_slide', requiredAction: 'Jump or slide',
    height: 1.0, topHeight: 1.35, depth: 0.6, lanes: 1, warning: 'Leafy branch on two posts — go over or under', warningDistance: 32, minLevel: 1,
  },
  {
    id: 'barrel', name: 'Barrel', emoji: '🛢️', difficulty: 'medium', clearance: 'high_jump', requiredAction: 'Higher jump or dodge',
    height: 1.3, depth: 1.0, lanes: 1, warning: 'Striped barrel with a glowing ring', warningDistance: 36, minLevel: 4,
  },
  {
    id: 'wall', name: 'Wall', emoji: '🧱', difficulty: 'medium', clearance: 'wall', requiredAction: 'Double jump or move around',
    height: 2.8, depth: 0.8, lanes: 1, warning: 'Tall, colourful wall visible from far away', warningDistance: 60, minLevel: 5,
  },
  {
    id: 'broken_bridge', name: 'Broken bridge', emoji: '🌉', difficulty: 'hard', clearance: 'gap', requiredAction: 'Long jump',
    height: 0, depth: 9, lanes: 3, warning: 'Cracked, broken boards and arrows on the ground where to jump', warningDistance: 45, minLevel: 7,
  },
  {
    id: 'falling_rocks', name: 'Falling rocks', emoji: '🪨', difficulty: 'hard', clearance: 'timed', requiredAction: 'Dodge and timing',
    height: 1.0, depth: 1.2, lanes: 1, warning: 'Rocks shake, dust falls, a warning sound plays and a shadow shows where it lands', warningDistance: 40, minLevel: 8,
  },
  {
    id: 'moving_critter', name: 'Moving obstacle', emoji: '🐍', difficulty: 'hard', clearance: 'timed', requiredAction: 'Timed jump or dodge',
    height: 0.8, depth: 0.9, lanes: 1, warning: 'Friendly critter moving side to side on a dotted path', warningDistance: 40, minLevel: 9,
  },
];
