// Compares the web prototype's planner with the Unity (C#) planner, level by level.
// Run: node web-prototype/tools/check-planner.js   (after `dotnet test` in unity-game/DomainTests~ wrote shared/plan-fixtures.json)
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '../..');
global.window = {};
require(path.join(root, 'web-prototype/content.js'));
const R = require(path.join(root, 'web-prototype/js/rules.js'));
const content = window.TFY_CONTENT;
const fixtures = JSON.parse(fs.readFileSync(path.join(root, 'shared/plan-fixtures.json'), 'utf8'));
const fmt = (n) => (Math.round((n || 0) * 100) / 100).toFixed(2);
let failures = 0;
for (const f of fixtures) {
  const level = content.levels.find((l) => l.id === f.level);
  const js = R.plan(level, content).items.map((i) => `${i.kind}:${i.id}:${i.lane}:${fmt(i.z)}:${fmt(i.y)}`);
  // Numbers are compared with a tolerance: .NET and JS round x.xx5 differently when printing.
  const eq = (a, b) => { const x = a.split(':'), y = b.split(':'); return x.length === y.length && x.every((v, k) => (k < 3 ? v === y[k] : Math.abs(Number(v) - Number(y[k])) < 0.011)); };
  const same = js.length === f.items.length && js.every((x, i) => eq(x, f.items[i]));
  const t = R.totals(level); const coins = js.filter((x) => x.startsWith('coin:')).length;
  console.log(`${f.level}: ${same ? 'identical' : 'DIFFERENT'} (${js.length} items, ${coins}/${t.coins} coins)`);
  if (!same) {
    failures++;
    const i = js.findIndex((x, k) => !eq(x, f.items[k] || ''));
    console.log(`  first difference at #${i}: js=${js[i]} c#=${f.items[i]}`);
  }
}
process.exit(failures ? 1 : 0);
