import { validateAnnouncement, validateLevel, validateShopItem } from '../../src/domain/adminValidation';
import { C, level } from './helpers';

const obstacleIds = C.obstacles.map(o => o.id);

describe('admin level validation', () => {
  it('rejects a level whose id does not match its number', () => {
    expect(validateLevel({ ...level(1), id: 'level_09' }, C.worlds, obstacleIds).join()).toMatch(/id must be level_01/);
  });
  it('rejects unknown obstacles, worlds and bad speeds', () => {
    const l = level(2);
    const bad = { ...l, world: 'moon', maxSpeed: 1, sections: l.sections.map((s, i) => (i === 1 ? { ...s, obstacles: ['lava'] } : s)) };
    const e = validateLevel(bad, C.worlds, obstacleIds).join(' ');
    expect(e).toMatch(/world does not exist/);
    expect(e).toMatch(/speeds/);
    expect(e).toMatch(/unknown obstacle/);
  });
  it('rejects a checkpoint count that does not match the sections', () => {
    expect(validateLevel({ ...level(1), checkpointCount: 5 }, C.worlds, obstacleIds).join()).toMatch(/checkpointCount/);
  });
  it('rejects star thresholds above the coins available', () => {
    expect(validateLevel({ ...level(1), threeStarCoins: 9999 }, C.worlds, obstacleIds).join()).toMatch(/threeStarCoins/);
  });
});

describe('admin content validation', () => {
  it('rejects an item priced in both coins and gems', () => {
    expect(validateShopItem({ ...C.shop[1], priceCoins: 10, priceGems: 1 }).join()).toMatch(/either/);
  });
  it('announcements to children cannot contain links or contact details', () => {
    const base = { title: 'Hello', message: 'New world soon!', active: true, startsAt: 1, endsAt: 2 };
    expect(validateAnnouncement(base)).toEqual([]);
    expect(validateAnnouncement({ ...base, message: 'visit https://x.com' }).join()).toMatch(/no links/);
    expect(validateAnnouncement({ ...base, message: 'mail me@x.com' }).join()).toMatch(/no links/);
  });
});
