// Negative check: with nobody playing, the cat must bump into obstacles, lose hearts and reach the Fail screen
// (proves collisions, hearts and failure work — the smoke test's autopilot never gets hit).
const path = require('path');
const puppeteer = require(process.env.PUPPETEER_PATH || 'puppeteer-core');
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
(async () => {
  const browser = await puppeteer.launch({ executablePath: process.argv[2], headless: 'new', args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] });
  const page = await browser.newPage(); await page.setViewport({ width: 430, height: 860 });
  const errors = []; page.on('pageerror', (e) => errors.push(e.message));
  await page.goto('file:///' + path.resolve(__dirname, '../index.html').replace(/\\/g, '/'));
  await sleep(4200);
  await page.evaluate(() => { localStorage.setItem('tfy.catadventure.hints.v1', 'jump,lane,branch,wall,bridge,rocks,critter'); TFYGame.fastForward = 4; TFYGame.player.currentLevel = 8; TFYGame.playLevel(8); });
  const start = Date.now(); let s;
  while (Date.now() - start < 240000) {
    await sleep(1000);
    s = await page.evaluate(() => ({ hearts: TFYGame.run.hearts, dist: Math.round(TFYGame.run.distance), fail: document.getElementById('fail').classList.contains('on'), flag: getComputedStyle(document.getElementById('flagBtn')).display, done: document.getElementById('complete').classList.contains('on'), hasCp: !!TFYGame.run.cp }));
    if (s.fail || s.done) break;
  }
  console.log('idle run on level 8:', s);
  await page.screenshot({ path: path.join(__dirname, 'shots', 'fail-screen.png') });
  if (s.fail && s.hasCp) {
    await page.evaluate(() => document.getElementById('flagBtn').click()); await sleep(2500);
    const r = await page.evaluate(() => ({ hearts: TFYGame.run.hearts, dist: Math.round(TFYGame.run.distance), playing: TFYGame.run.playing }));
    console.log('restart from flag:', r);
  }
  console.log('errors', errors.length ? errors : 'none');
  await browser.close();
  process.exit(s && s.fail ? 0 : 1);
})();
