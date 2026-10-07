import { AchievementDef, DailyRewardDef, EconomyConfig, MysteryEntry, PowerUpDef, ShopItem } from './types';

const item = (id: string, name: string, slot: ShopItem['slot'], category: ShopItem['category'], priceCoins: number, priceGems: number,
  colors: string[], description: string, o: Partial<ShopItem> = {}): ShopItem =>
  ({ id, name, slot, category, priceCoins, priceGems, unlockLevel: 0, source: 'shop', colors, description, enabled: true, ...o });

export const SHOP: ShopItem[] = [
  // Cats (colors: body, stripes/spots, belly, eyes)
  item('orange_cat', 'Orange Cat', 'cat', 'CATS', 0, 0, ['#F59A3C', '#D9731F', '#FFE3C2', '#3BAA5C'], 'Your first friend — brave and bouncy!', { source: 'default' }),
  item('white_cat', 'White Cat', 'cat', 'CATS', 500, 0, ['#F7F4EF', '#E6DED3', '#FFFFFF', '#4AA3E0'], 'Soft as a cloud.'),
  item('black_cat', 'Black Cat', 'cat', 'CATS', 800, 0, ['#2B2B33', '#1C1C22', '#4A4A55', '#F5D14B'], 'Quiet paws, shiny eyes.'),
  item('grey_cat', 'Grey Cat', 'cat', 'CATS', 800, 0, ['#9EA3AB', '#7A7F88', '#D9DCE1', '#62B36B'], 'Calm and clever.'),
  item('spotted_cat', 'Spotted Cat', 'cat', 'CATS', 1200, 0, ['#F4E2C6', '#8B5A2B', '#FFF6E8', '#3F8FD6'], 'Covered in happy spots.', { unlockLevel: 2 }),
  item('pirate_cat', 'Pirate Cat', 'cat', 'CATS', 2000, 0, ['#C9864A', '#8C5427', '#F2D3B0', '#2E8B57'], 'Ahoy! Comes with a bandana.', { unlockLevel: 4 }),
  item('ninja_cat', 'Ninja Cat', 'cat', 'CATS', 2500, 0, ['#3A3F58', '#252838', '#5A607D', '#FF5A5A'], 'Super sneaky runner.', { unlockLevel: 6 }),
  item('superhero_cat', 'Superhero Cat', 'cat', 'CATS', 0, 15, ['#F2F2F2', '#D0D0D0', '#FFFFFF', '#2F80ED'], 'Mask and a red cape!', { unlockLevel: 5 }),
  item('space_cat', 'Space Cat', 'cat', 'CATS', 0, 25, ['#B9C7FF', '#7D8FE0', '#E8ECFF', '#00D1FF'], 'Wears a tiny space helmet.', { unlockLevel: 8 }),
  // Hats (crowns share the head slot)
  item('red_hat', 'Red Hat', 'hat', 'HATS', 150, 0, ['#E63946'], 'A cosy red cap.'),
  item('party_hat', 'Party Hat', 'hat', 'HATS', 250, 0, ['#FF7AB6', '#FFD166'], 'Every run is a party!'),
  item('cowboy_hat', 'Cowboy Hat', 'hat', 'HATS', 400, 0, ['#A0673A'], 'Yee-haw!'),
  item('gold_crown', 'Gold Crown', 'hat', 'HATS', 0, 10, ['#FFD700'], 'For the ruler of the park.'),
  item('flower_crown', 'Flower Crown', 'hat', 'HATS', 0, 0, ['#FF9EC7', '#FFFFFF', '#7BD389'], 'A daily reward treat.', { source: 'daily' }),
  // Glasses
  item('sunglasses', 'Sunglasses', 'glasses', 'GLASSES', 200, 0, ['#1D1D1D'], 'Too cool for school.'),
  item('star_glasses', 'Star Glasses', 'glasses', 'GLASSES', 350, 0, ['#FF5DA2'], 'Twinkle twinkle!'),
  item('round_glasses', 'Round Glasses', 'glasses', 'GLASSES', 300, 0, ['#6B4E2E'], 'Smart and round.'),
  // Shoes
  item('blue_shoes', 'Blue Shoes', 'shoes', 'SHOES', 200, 0, ['#3A86FF'], 'Speedy-looking sneakers.'),
  item('red_sneakers', 'Red Sneakers', 'shoes', 'SHOES', 300, 0, ['#FF3B3B', '#FFFFFF'], 'Bouncy red kicks.'),
  item('gold_boots', 'Gold Boots', 'shoes', 'SHOES', 0, 8, ['#FFC300'], 'Shiny golden boots.'),
  // Backpacks (capes share the back slot)
  item('school_backpack', 'School Backpack', 'backpack', 'BACKPACKS', 300, 0, ['#4CC9F0', '#F72585'], 'Ready for adventure.'),
  item('rocket_backpack', 'Rocket Backpack', 'backpack', 'BACKPACKS', 0, 12, ['#C0C0C0', '#FF6B35'], 'Looks fast (just for fun!).', { unlockLevel: 7 }),
  item('hero_cape', 'Hero Cape', 'cape', 'CAPES_SCARVES', 600, 0, ['#E63946'], 'Flaps in the wind.'),
  item('star_cape', 'Star Cape', 'cape', 'CAPES_SCARVES', 0, 6, ['#3A0CA3', '#FFD166'], 'Covered in little stars.'),
  item('blue_scarf', 'Blue Scarf', 'scarf', 'CAPES_SCARVES', 0, 0, ['#4895EF'], 'A daily reward gift.', { source: 'daily' }),
  item('rainbow_scarf', 'Rainbow Scarf', 'scarf', 'CAPES_SCARVES', 450, 0, ['#FF595E', '#FFCA3A', '#8AC926', '#1982C4', '#6A4C93'], 'All the colours!'),
  // Trails
  item('sparkle_trail', 'Sparkle Trail', 'trail', 'TRAILS', 400, 0, ['#FFF3B0'], 'Leaves little sparkles.'),
  item('bubble_trail', 'Bubble Trail', 'trail', 'TRAILS', 500, 0, ['#A0E7FF'], 'Pop pop pop!'),
  item('rainbow_trail', 'Rainbow Trail', 'trail', 'TRAILS', 0, 0, ['#FF595E', '#FFCA3A', '#8AC926', '#1982C4', '#6A4C93'], 'Special 7-day reward.', { source: 'daily' }),
];

export const ACHIEVEMENTS: AchievementDef[] = [
  { id: 'first_jump', name: 'First Jump', description: 'Jump for the first time.', stat: 'jumps', target: 1, rewardCoins: 20, rewardGems: 0, icon: '🦘' },
  { id: 'collect_100_coins', name: 'Coin Collector', description: 'Collect 100 coins.', stat: 'coinsCollected', target: 100, rewardCoins: 50, rewardGems: 0, icon: '🪙' },
  { id: 'collect_1000_coins', name: 'Super Collector', description: 'Collect 1,000 coins.', stat: 'coinsCollected', target: 1000, rewardCoins: 200, rewardGems: 1, icon: '💰' },
  { id: 'finish_5_levels', name: 'Adventurer', description: 'Complete 5 levels.', stat: 'levelsCompleted', target: 5, rewardCoins: 150, rewardGems: 1, icon: '🗺️' },
  { id: 'finish_10_levels', name: 'Cat Hero', description: 'Complete 10 levels.', stat: 'levelsCompleted', target: 10, rewardCoins: 300, rewardGems: 3, icon: '🦸' },
  { id: 'finish_20_levels', name: 'Super Explorer', description: 'Complete 20 levels.', stat: 'levelsCompleted', target: 20, rewardCoins: 400, rewardGems: 3, icon: '🧭' },
  { id: 'finish_30_levels', name: 'TFY Legend', description: 'Complete 30 levels.', stat: 'levelsCompleted', target: 30, rewardCoins: 1000, rewardGems: 5, icon: '👑' },
  { id: 'finish_50_levels', name: 'Adventure Master', description: 'Complete 50 levels.', stat: 'levelsCompleted', target: 50, rewardCoins: 1500, rewardGems: 8, icon: '🏅' },
  { id: 'finish_100_levels', name: 'Cat Champion', description: 'Complete all 100 levels!', stat: 'levelsCompleted', target: 100, rewardCoins: 3000, rewardGems: 20, icon: '🏆' },
  { id: 'collect_30_stars', name: 'Star Master', description: 'Collect 30 stars.', stat: 'stars', target: 30, rewardCoins: 300, rewardGems: 2, icon: '⭐' },
  { id: 'fish_friend', name: 'Fish Friend', description: 'Collect 25 fish.', stat: 'fishCollected', target: 25, rewardCoins: 100, rewardGems: 0, icon: '🐟' },
  { id: 'gem_hunter', name: 'Gem Hunter', description: 'Find 5 gems.', stat: 'gemsFound', target: 5, rewardCoins: 100, rewardGems: 1, icon: '💎' },
  { id: 'first_style', name: 'Looking Good', description: 'Unlock your first item in the Cat Shop.', stat: 'itemsBought', target: 1, rewardCoins: 50, rewardGems: 0, icon: '🎩' },
  { id: 'perfect_run', name: 'Purr-fect!', description: 'Earn 3 stars on a level.', stat: 'perfectLevels', target: 1, rewardCoins: 100, rewardGems: 0, icon: '🌟' },
];

// No streak loss: missing a day simply continues from the next day in the cycle (no pressure).
export const DAILY_REWARDS: DailyRewardDef[] = [
  { day: 1, type: 'coins', amount: 100, label: '100 Coins' },
  { day: 2, type: 'coins', amount: 150, label: '150 Coins' },
  { day: 3, type: 'gems', amount: 1, label: '1 Gem' },
  { day: 4, type: 'coins', amount: 200, label: '200 Coins' },
  { day: 5, type: 'item', amount: 1, itemId: 'blue_scarf', label: 'Blue Scarf' },
  { day: 6, type: 'coins', amount: 300, label: '300 Coins' },
  { day: 7, type: 'item', amount: 1, itemId: 'rainbow_trail', label: 'Rainbow Trail' },
];

export const MYSTERY_BOX: MysteryEntry[] = [
  { type: 'coins', amount: 100, weight: 38, label: '100 Coins' },
  { type: 'coins', amount: 250, weight: 22, label: '250 Coins' },
  { type: 'gems', amount: 1, weight: 16, label: '1 Gem' },
  { type: 'gems', amount: 3, weight: 5, label: '3 Gems' },
  { type: 'item', amount: 1, weight: 14, pool: ['party_hat', 'star_glasses', 'red_sneakers', 'sparkle_trail', 'flower_crown'], label: 'Accessory' },
  { type: 'item', amount: 1, weight: 5, pool: ['white_cat', 'grey_cat'], label: 'Cat Skin' },
];

export const POWER_UPS: PowerUpDef[] = [
  { id: 'magnet', name: 'Coin Magnet', durationSec: 8, color: '#FF4D6D', description: 'Pulls nearby coins to you.' },
  { id: 'shield', name: 'Shield', durationSec: 0, color: '#4CC9F0', description: 'Protects you from one bump.' },
  { id: 'super_jump', name: 'Super Jump', durationSec: 8, color: '#80ED99', description: 'Jump extra high!' },
  { id: 'speed_boost', name: 'Speed Boost', durationSec: 5, color: '#FFB703', description: 'Zoom! Obstacles bounce away.' },
  { id: 'double_coins', name: 'Double Coins', durationSec: 10, color: '#FFD60A', description: 'Every coin counts twice.' },
];

export const ECONOMY: EconomyConfig = {
  firstClearBonusBase: 50,
  firstClearBonusPerLevel: 10,
  newStarBonus: 25,
  coinRewardMultiplier: 1,
  scorePerMetre: 1,
  scorePerCoin: 10,
  scorePerFish: 50,
  scorePerGem: 150,
  scorePerStarToken: 300,
  scorePerHeartLeft: 200,
  startingHearts: 3,
};

// Automatic usernames are always <prefix><digits>. Children never type a name, and the server rejects anything else.
export const USERNAME_PREFIXES = ['Kitty', 'JumpCat', 'HappyKitty', 'SuperCat', 'TFYCat', 'SunnyPaws', 'StarKitten', 'BouncyCat', 'LuckyPaws', 'ZoomyCat', 'FluffyTail', 'MagicMeow'];

export const TIPS = [
  'Tip: Tap to jump!',
  'Tip: Double tap to jump higher!',
  'Tip: Swipe down to slide under branches.',
  'Tip: Swipe left or right to change lanes.',
  'Tip: Coins show you where to jump.',
  'Tip: Shaking rocks are about to fall — move to another lane!',
  'Tip: Jump at the arrows to cross broken bridges.',
  'Tip: Grab a shield to stay safe from one bump.',
  'Tip: Checkpoint flags save your progress.',
  'Tip: Find all 3 big stars for a perfect score!',
];
