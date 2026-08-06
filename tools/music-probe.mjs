// Each region sings its own Alfar track, and the authored spawns stand where
// the maps put them.
import { chromium } from 'playwright-core';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5173';
let passed = 0, failed = 0;
const check = (name, ok, detail = '') => {
  console.log(`${ok ? '  ok  ' : ' FAIL '} ${name}${detail ? ` — ${detail}` : ''}`);
  ok ? passed++ : failed++;
};
const browser = await chromium.launch({ channel: 'chrome', args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
const settle = (ms) => page.waitForTimeout(ms);
const hud = () => page.evaluate(async () => (await import('/js/game.js')).hudSnapshot());
const song = () => page.evaluate(async () => (await import('/js/game.js')).currentMusic());
const clearLevelUp = async () => {
  for (let i = 0; i < 6 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 3000 }).catch(() => { });
    await settle(500);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(400); }
  }
};
const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });

await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Music Tester');
await page.fill('#signup-email', `music.${Date.now()}@example.com`);
await page.fill('#signup-password', 'a-long-enough-passphrase');
await page.fill('#signup-confirm', 'a-long-enough-passphrase');
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(5000);
await clearLevelUp();

console.log('=== each region sings its own ===');
const SONGS = [
  { id: 'r1_ashwold', want: '', playing: /ballad-of-the-wandering/ , label: 'Ashwold keeps the ballad' },
  { id: 'r2_whispering_wood', want: 'cleric', playing: /the-cleric-game/, label: 'the wood sings the Cleric\'s theme' },
  { id: 'r3_deepdelve', want: 'deep-intro', playing: /crystal-deep-intro/, label: 'Deepdelve plays the approach to the deep' },
  { id: 'r4_kae_ychel_road', want: '', playing: /ballad-of-the-wandering/, label: 'the road keeps the ballad' },
];
for (const s of SONGS) {
  await page.evaluate(async r => (await import('/js/game.js')).debugLoadRegion(r), s.id);
  await settle(1800); await clearLevelUp();
  const m = await song();
  check(s.label, (m?.want ?? '') === s.want && s.playing.test(m?.playing ?? ''),
    `want="${m?.want}" playing="${m?.playing}"`);
}

console.log('=== and the cave answers with the crystal deep ===');
await page.evaluate(async () => (await import('/js/game.js')).debugEnterCave());
await settle(2000);
await page.click('.aq-cutscene-skip', { timeout: 3000 }).catch(() => { });
await settle(1500); await clearLevelUp();
const cave = await song();
check('the cavern track resolves from its map word', cave?.want === 'cavern' && /into-the-crystal-deep/.test(cave?.playing ?? ''),
  `want="${cave?.want}" playing="${cave?.playing}"`);
await page.evaluate(async () => (await import('/js/game.js')).debugLeaveCave());
await settle(1500); await clearLevelUp();

console.log('=== the authored spawns stand their ground ===');
const near = async (tx, ty, ids, radiusTiles = 8) => {
  const ents = await page.evaluate(async () => (await import('/js/game.js')).lastEnts()) ?? [];
  return ents.some(e => ids.includes(e.name) && Math.abs(e.x / 32 - tx) < radiusTiles && Math.abs(e.y / 32 - ty) < radiusTiles);
};
const SPAWNS = [
  { id: 'r1_ashwold', tx: 60, ty: 48, ids: ['mobSlime', 'mobBeast'], label: 'slimes past the boundary stone' },
  { id: 'r2_whispering_wood', tx: 12, ty: 47, ids: ['mobBeast'], label: 'beasts den in the hollow' },
  { id: 'r3_deepdelve', tx: 35, ty: 41, ids: ['mobWalker', 'mobBat'], label: 'walkers and bats hold the tips' },
  { id: 'r4_kae_ychel_road', tx: 37, ty: 42, ids: ['mobSpider', 'mobWorm'], label: 'the fallen city keeps its own' },
];
for (const s of SPAWNS) {
  await page.evaluate(async r => (await import('/js/game.js')).debugLoadRegion(r), s.id);
  await settle(1500); await clearLevelUp();
  await page.evaluate(async a => (await import('/js/game.js')).debugWarp(a.tx, a.ty), { tx: s.tx, ty: s.ty });
  await settle(700);
  check(s.label, await near(s.tx, s.ty, s.ids), s.ids.join('/'));
}

console.log('=== console ===');
check('the game logged no errors', errors.length === 0, errors.slice(0, 3).join(' | '));

console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed`);
await browser.close();
process.exit(failed === 0 ? 0 : 1);
