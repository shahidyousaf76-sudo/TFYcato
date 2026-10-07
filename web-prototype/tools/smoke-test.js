// Headless smoke test for the web prototype: boots the game, visits every screen, and lets an autopilot play
// levels to the finish line. Screenshots go to web-prototype/tools/shots/.
//   npm i puppeteer-core   (in any folder)   then:   node smoke-test.js "<path to chrome.exe>"
const path = require('path');
const fs = require('fs');
const puppeteer = require(process.env.PUPPETEER_PATH || 'puppeteer-core');

const chrome = process.argv[2] || 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const page_url = 'file:///' + path.resolve(__dirname, '../index.html').replace(/\\/g, '/');
const shots = path.join(__dirname, 'shots');
fs.mkdirSync(shots, { recursive: true });
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

(async () => {
  const browser = await puppeteer.launch({ executablePath: chrome, headless: 'new', args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--autoplay-policy=no-user-gesture-required'] });
  const page = await browser.newPage();
  await page.setViewport({ width: 430, height: 860, deviceScaleFactor: 1, isMobile: true, hasTouch: true });
  const errors = [];
  page.on('pageerror', (e) => errors.push('pageerror: ' + e.message));
  page.on('console', (m) => { if (m.type() === 'error') errors.push('console: ' + m.text()); });
  page.on('error', (e) => { console.log('PAGE CRASHED:', e.message); });
  await page.goto(page_url);
  await sleep(4200);
  await page.screenshot({ path: path.join(shots, '01-home.png') });
  if (process.env.SPEED) await page.evaluate((v) => { TFYGame.player.settings.gameSpeed = v; }, process.env.SPEED); // Slow / Normal / Fast
  const player = await page.evaluate(() => ({ name: TFYGame.player.username, level: TFYGame.player.currentLevel }));
  console.log('player', player);

  for (const [go, file] of [['levels', '02-levels'], ['achievements', '03-achievements'], ['rewards', '04-rewards'], ['settings', '05-settings']]) {
    await page.evaluate((g) => document.querySelector(`#home [data-go="${g}"]`).click(), go);
    await sleep(500); await page.screenshot({ path: path.join(shots, file + '.png') });
    await page.evaluate((g) => document.querySelector(`#${g} [data-go="home"]`).click(), go); await sleep(400);
  }
  await page.evaluate(() => document.querySelector('#home [data-go="shop"]').click()); await sleep(600);
  await page.evaluate(() => document.querySelectorAll('#shopGrid .item')[1].click()); await sleep(900);
  await page.screenshot({ path: path.join(shots, '06-shop-preview.png') });
  await page.evaluate(() => document.querySelector('#shop [data-go="home"]').click()); await sleep(500);

  // Autopilot: reads the level plan and reacts like a (very good) child would.
  await page.evaluate(() => {
    const acted = new Set();
    const fpsStart = performance.now(); let frames = 0; (function count() { frames++; if (performance.now() - fpsStart < 3000) requestAnimationFrame(count); else window.__fps = frames / 3; })();
    window.TFYGame.fastForward = 4; // same physics, several steps per frame (headless rendering is slow)
    window.TFYGame.onStep = () => {
      const g = window.TFYGame, r = g.run, c = g.cat; if (!r || !r.playing || !c || r.startDelay > 0) return;
      const d = r.distance;
      const row = r.plan.rows.find((x) => x.obstacleId && x.z + x.depth > d - 0.2 && x.z - d < 16); if (!row) return;
      const ahead = row.z - d, key = row.z + ':' + c.lane;
      const blocked = row.fullWidth || row.blocked[c.lane] || row.obstacleId === 'moving_critter';
      if (row.obstacleId === 'broken_bridge') { if (ahead < 4.5 && !acted.has('b' + row.z)) { acted.add('b' + row.z); g.doInput('tap'); } return; }
      if (!blocked) return;
      if (!row.fullWidth && row.obstacleId !== 'moving_critter') {
        const open = [0, 1, 2].filter((l) => !row.blocked[l]).sort((a, b) => Math.abs(a - c.lane) - Math.abs(b - c.lane))[0];
        if (open !== undefined && ahead < 13 && !acted.has(key)) { acted.add(key); g.doInput(open < c.lane ? 'left' : 'right'); }
        return;
      }
      if (row.obstacleId === 'branch') { if (ahead < 2.6 && !acted.has('s' + row.z)) { acted.add('s' + row.z); g.doInput('down'); } return; }
      if (ahead < 3.3 && !acted.has('j' + row.z)) { acted.add('j' + row.z); g.doInput('tap'); }
    };
  });

  async function play(n, midShot) {
    await page.evaluate((lv) => { TFYGame.player.currentLevel = Math.max(TFYGame.player.currentLevel, lv); TFYGame.playLevel(lv); }, n);
    const start = Date.now(); let shot = false, res = null;
    while (Date.now() - start < 150000) {
      await sleep(500);
      const s = await page.evaluate(() => ({ mode: TFYGame.mode, dist: TFYGame.run ? TFYGame.run.distance : 0, len: TFYGame.run ? TFYGame.run.plan.length : 1, hearts: TFYGame.run ? TFYGame.run.hearts : 0, playing: TFYGame.run ? TFYGame.run.playing : false, complete: document.getElementById('complete').classList.contains('on'), fail: document.getElementById('fail').classList.contains('on') }));
      if (midShot && !shot && s.dist > midShot) { shot = true; await page.screenshot({ path: path.join(shots, `level${n}-play.png`) }); }
      if (s.complete || s.fail) { res = s; break; }
    }
    await sleep(1800);
    await page.screenshot({ path: path.join(shots, `level${n}-end.png`) });
    const p = await page.evaluate((lv) => ({ stars: (TFYGame.player.levels['level_' + String(lv).padStart(2, '0')] || {}).bestStars, coins: TFYGame.player.coins, current: TFYGame.player.currentLevel }), n);
    const mem = await page.evaluate(() => TFYGame.memory);
    console.log(`level ${n}:`, res && res.complete ? 'COMPLETE' : res && res.fail ? 'failed' : 'timeout', `hearts left ${res ? res.hearts : '?'}`, p, 'memory', JSON.stringify(mem));
    return res && res.complete;
  }

  await sleep(3200); console.log('headless fps', await page.evaluate(() => window.__fps));
  const results = [];
  for (const [n, mid] of (process.env.LEVELS ? JSON.parse(process.env.LEVELS) : [[1, 140], [5, 300], [7, 250], [9, 330]])) results.push([n, await play(n, mid)]);
  await page.evaluate(() => TFYGame.goHome()); await sleep(1200);
  await page.screenshot({ path: path.join(shots, '09-home-after.png') });
  console.log('results', JSON.stringify(results));
  console.log('errors', errors.length ? errors : 'none');
  await browser.close();
})().catch((e) => { console.error(e); process.exit(1); });
