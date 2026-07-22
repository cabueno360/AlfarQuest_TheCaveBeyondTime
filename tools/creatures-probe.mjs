// The bestiary and the AI, played rather than inspected.
//
//     node tools/creatures-probe.mjs
//
// Two things this catches. First, that the creatures are varied and lore-placed
// — read straight from the catalogue, so it does not depend on what happened to
// spawn. Second, that the AI is actually doing something: creatures that sleep
// until roused, hunt when near and patrol when not, cast bolts, and — the change
// this release turns on — pay their killer, not the whole party.

import { chromium } from 'playwright-core';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5223';

let passed = 0, failed = 0;
const check = (name, ok, detail = '') => {
  console.log(`${ok ? '  ok  ' : ' FAIL '} ${name}${detail ? ` — ${detail}` : ''}`);
  ok ? passed++ : failed++;
};

const browser = await chromium.launch({ channel: 'chrome', args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 860 } });

const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
page.on('pageerror', e => errors.push(String(e)));

const settle = (ms = 700) => page.waitForTimeout(ms);
const hud = () => page.evaluate(async () => (await import('/js/game.js')).hudSnapshot());
const bestiary = () => page.evaluate(async () => (await import('/js/game.js')).bestiary());

const clearLevelUp = async () => {
  for (let i = 0; i < 4 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 4000 }).catch(() => { });
    await settle(700);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(500); }
  }
};

const player = { email: `beasts.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };

console.log('\n=== into the world ===');
await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Beast Tester');
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]');
await settle(2600);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null,
                           null, { timeout: 25000 });
await settle(2500);
await clearLevelUp();

console.log('\n=== the bestiary is varied and lore-placed ===');

const beasts = await bestiary();
check('there is a bestiary to draw from', Array.isArray(beasts) && beasts.length >= 6,
  `${beasts?.length ?? 0} species`);
const demeanors = new Set(beasts.map(b => b.demeanor));
const abilities = new Set(beasts.map(b => b.ability).filter(a => a && a !== 'None'));
const biomes = new Set(beasts.map(b => b.biome));
check('creatures differ in temperament, not just stats',
  demeanors.size >= 3, [...demeanors].join(', '));
check('some carry a trick beyond biting', abilities.size >= 3, [...abilities].join(', '));
check('some break and run when hurt', beasts.some(b => b.fleeBelow > 0),
  beasts.filter(b => b.fleeBelow > 0).map(b => b.id).join(', '));
check('some defend their own', beasts.some(b => b.protective),
  beasts.filter(b => b.protective).map(b => b.id).join(', '));
check('the bestiary is spread across the biomes', biomes.size >= 4, [...biomes].join(', '));

console.log('\n=== the world is populated, and aggro is not global ===');

// Read straight from the spawn: the map is full of creatures, and none of them
// is hunting the party yet, because the party is in the village and nothing
// aggros from across the map. That is the "no map-wide chain" property, checked
// deterministically rather than by hoping a walk shows it.
const spawn = await hud();
check('creatures inhabit the map', (spawn?.enemies ?? 0) > 10, `${spawn?.enemies} on the map`);
check('several species are placed across the biomes',
  (spawn?.enemyKinds || '').split(',').filter(Boolean).length >= 4, spawn?.enemyKinds);
check('nothing hunts the party across the map — aggro is local',
  spawn?.chasing === 0, `${spawn?.chasing} chasing at rest`);
check('some creatures begin asleep, not patrolling', (spawn?.asleep ?? 0) > 0,
  `${spawn?.asleep} asleep`);

console.log('\n=== the AI, on a controlled stage ===');

// Headless navigation across fences and safe zones is unreliable, so the party
// is set down on open ground with a fixed cast around it, already hunting. What
// happens next is the real AI — observed on a deterministic stage.
const before = await hud();
await page.evaluate(async () => (await import('/js/game.js')).debugEncounter());
await settle(500);
const staged = await hud();
check('the encounter stood creatures up and hunting', staged?.chasing >= 2,
  `${staged?.chasing} chasing, ${staged?.enemies} present`);

// Stand and take it for a moment — attack released so the casters live long
// enough to fire. A chasing caster inside its reach spits on its own.
const castsBefore = staged?.castsFired ?? 0, boltsBefore = staged?.boltsFired ?? 0;
let sawBolt = false, sawCast = false;
for (let i = 0; i < 22 && !(sawBolt && sawCast); i++) {
  await settle(150);
  const h = await hud();
  if (!h) continue;
  if (h.castsFired > castsBefore) sawCast = true;
  if (h.boltsFired > boltsBefore) sawBolt = true;
  if (h.bolts > 0) sawBolt = true;
}
check('creatures hunt the party down', (await hud())?.chasing >= 1);
check('monsters use their abilities', sawCast, `casts ${castsBefore} → ${(await hud())?.castsFired}`);
check('and some are ranged bolts', sawBolt, `bolts ${boltsBefore} → ${(await hud())?.boltsFired}`);

console.log('\n=== experience is individual — the killing blow pays ===');

// Now kill the staged creatures with one hero in control, and watch only that
// hero's bar move. Party-wide XP would raise all three together; killing-blow XP
// concentrates it on whoever lands the blow.
await clearLevelUp();
await page.keyboard.down('j');
for (let i = 0; i < 16; i++) {
  await settle(400);
  const h = await hud();
  if (h && h.enemies === 0) break;
  // a little movement so the lead lands blows rather than only the companions
  if (i % 2 === 0) { await page.keyboard.down('w'); await settle(150); await page.keyboard.up('w'); }
  await clearLevelUp();
}
await page.keyboard.up('j');
await settle(400);
await clearLevelUp();

// Party-wide XP would leave the three heroes identical; killing-blow XP makes
// them diverge. That divergence is the whole change, and it is the cleanest
// thing to assert.
const party = (await hud())?.party ?? [];
const xps = party.map(p => p.xp);
const levels = party.map(p => `${p.key} L${p.level} ${p.xp}xp`);
check('the party carries per-hero experience', party.every(p => 'xp' in p), levels.join(' | '));
check('experience is not shared — the heroes have diverged',
  new Set(xps).size >= 2, `xp values: ${xps.join(', ')}`);
check('somebody earned something', xps.some(x => x > 0), `total ${xps.reduce((a, b) => a + b, 0)}`);

// And the HUD shows the steered hero's own bar, not a shared one.
const active = party.find(p => p.active);
const h2 = await hud();
check('the HUD experience bar is the active hero\'s own',
  active && Math.abs(h2.xp - active.xp) <= 1 && h2.heroLevel === active.level,
  `hud ${h2.xp}xp L${h2.heroLevel} vs ${active?.key} ${active?.xp}xp L${active?.level}`);

console.log('\n=== console ===');
const noisy = errors.filter(e => !/favicon|status of 401/.test(e));
check('the game logged no errors', noisy.length === 0, noisy.slice(0, 2).join(' | '));

await browser.close();
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed\n`);
process.exit(failed === 0 ? 0 : 1);
