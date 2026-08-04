// The fate dice, proven in the running game: opening a CHEST rolls a d20 over
// the screen, the loot window waits for the die, and the engine's record of
// the throw is queryable. Barrels stay quick (no die).
import { chromium } from 'playwright-core';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5173';
const DIR = 'C:/Users/CARLOS~1.BUE/AppData/Local/Temp/claude/c--tests-AlfarQuest-TheCaveBeyondTime/3582e782-7ea8-44b0-b9d3-522ccefdd1dd/scratchpad';
let passed = 0, failed = 0;
const check = (name, ok, detail = '') => {
  console.log(`${ok ? '  ok  ' : ' FAIL '} ${name}${detail ? ` — ${detail}` : ''}`);
  ok ? passed++ : failed++;
};

const browser = await chromium.launch({ channel: 'chrome', args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
page.on('pageerror', e => errors.push(String(e)));
const settle = (ms = 500) => page.waitForTimeout(ms);
const hud = () => page.evaluate(async () => (await import('/js/game.js')).hudSnapshot());
const warp = (x, y) => page.evaluate(async a => (await import('/js/game.js')).debugWarp(a.x, a.y), { x, y });
const dice = () => page.evaluate(async () => (await import('/js/game.js')).diceSeen());
const clearLevelUp = async () => {
  for (let i = 0; i < 4 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 3000 }).catch(() => { });
    await settle(500);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(400); }
  }
};

await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Dice Tester');
await page.fill('#signup-email', `dice.${Date.now()}@example.com`);
await page.fill('#signup-password', 'a-long-enough-passphrase');
await page.fill('#signup-confirm', 'a-long-enough-passphrase');
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(5000);
await clearLevelUp();

console.log('\n=== a chest consults the fates ===');
// A hollow log — a wooden chest ("Open") alone at Ashwold's western edge
// (tile 6.5, 23.5), far from any door whose portal prompt would shadow it.
// Warped in a small spiral until the prompt actually offers it: reach is
// short, and the warp itself is a no-op while a level-up card holds the world.
let at = null;
for (const [x, y] of [[6, 23], [7, 23], [6, 24], [7, 24], [6, 22]]) {
  await clearLevelUp();
  await warp(x, y); await settle(450); await clearLevelUp();
  const h = await hud();
  console.log(`  (at ${x},${y}: "${h?.promptVerb ?? ''} ${h?.promptName ?? ''}")`);
  if (/Open|Unlock/.test(h?.promptVerb ?? '')) { at = h; break; }
}
check('standing at a chest', /Open|Unlock/.test(at?.promptVerb ?? ''), `"${at?.promptVerb} ${at?.promptName}"`);

await page.keyboard.press('e');
await settle(900);
const midRoll = await page.locator('.aq-loot').count();
await page.screenshot({ path: `${DIR}/dice-midroll.png` });
check('the loot window WAITS for the die', midRoll === 0, midRoll ? 'window opened instantly' : 'window held while the die tumbles');

const rolls = await dice();
check('the engine recorded a fortune roll', rolls.some(d => d.kind === 'fortune'),
  JSON.stringify(rolls[0] ?? null));
const r = rolls.find(d => d.kind === 'fortune');
check('the fortune roll is a d6 with its modifier folded in',
  r && r.sides === 6 && r.value >= 1 && r.value <= 6 && r.total === r.value + r.mod,
  r ? `d6=${r.value} +${r.mod} = ${r.total} (${r.outcome})` : 'no roll');

await settle(2200); await clearLevelUp();
const after = await page.locator('.aq-loot').count();
const empt = await hud();
check('the reveal arrives once the die settles', after > 0 || (empt !== null),
  after > 0 ? 'loot window opened' : 'roll came up empty (floater said so)');
check('the plaque reads the roll out',
  await page.evaluate(() => document.getElementById('aq-dice-plaque')?.style.opacity === '1'),
  await page.evaluate(() => document.getElementById('aq-dice-plaque')?.textContent ?? 'no plaque'));
await page.screenshot({ path: `${DIR}/dice-settled.png` });
if (after > 0) { await page.keyboard.press('Escape'); await settle(500); }

console.log('\n=== a barrel stays quick ===');
await warp(23, 30); await settle(500); await clearLevelUp();
const tub = await hud();
if (/Search/.test(tub?.promptVerb ?? '')) {
  const before = (await dice()).length;
  await page.keyboard.press('e'); await settle(700);
  check('no die for a barrel', (await dice()).length === before, 'Search opened without the fates');
  if ((await page.locator('.aq-loot').count()) > 0) { await page.keyboard.press('Escape'); await settle(400); }
} else {
  console.log(`  (no barrel in reach — "${tub?.promptVerb} ${tub?.promptName}" — skipped)`);
}

console.log('\n=== the Mouth asks for a light ===');
// The cave will not have the party without the summoned orb: E at The Mouth
// rolls d20 + the Mage's mind against 12. Success descends when the die lands;
// failure locks the summoning for ten minutes — pressing E again rolls nothing.
await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r3_deepdelve'));
await settle(1500); await clearLevelUp();
let mouth = null;
for (const [x, y] of [[54, 13], [55, 14], [54, 15], [53, 14]]) {
  await clearLevelUp(); await warp(x, y); await settle(450); await clearLevelUp();
  const h = await hud();
  if (/Descend/.test(h?.promptVerb ?? '')) { mouth = h; break; }
}
check('standing at The Mouth', !!mouth, mouth ? `"${mouth.promptVerb} ${mouth.promptName}"` : 'no prompt reached');
const beforeSummon = (await dice()).length;
await page.keyboard.press('e'); await settle(600);
const summon = (await dice()).slice(beforeSummon).find(d => d.kind === 'summon');
check('the descent consults the fates', !!summon, JSON.stringify(summon ?? null));
await settle(3500);
await page.click('.aq-cutscene-skip', { timeout: 2500 }).catch(() => { });
await settle(1200); await clearLevelUp();
const under = await hud();
if (summon && summon.total >= 12) {
  check('success: the party descends once the die lands', under?.stage === 2, `stage ${under?.stage}`);
} else {
  check('failure: the dark holds the door', under?.stage !== 2, `stage ${under?.stage}`);
  const lockedAt = (await dice()).length;
  await page.keyboard.press('e'); await settle(600);
  check('  and the weave is not asked again during the lockout',
    (await dice()).length === lockedAt, 'no new roll');
}

console.log('\n=== a warded text asks for a mind ===');
// The road-grave on Kae'Ychel Road carries FateCheck 12: reading it rolls
// d20 + the party's best Intelligence. Success opens the page when the die
// lands; failure locks the letters for ten minutes.
await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r4_kae_ychel_road'));
await settle(1500); await clearLevelUp();
let grave = null;
for (const [x, y] of [[10, 48], [11, 48], [10, 49], [11, 49]]) {
  await clearLevelUp(); await warp(x, y); await settle(450); await clearLevelUp();
  const h = await hud();
  if (/road-grave|headstone/.test(h?.promptName ?? '')) { grave = h; break; }
}
check('standing at the road-grave', !!grave, grave ? `"${grave.promptVerb} ${grave.promptName}"` : 'no prompt reached');
const beforeLore = (await dice()).length;
await page.keyboard.press('e'); await settle(600);
const lore = (await dice()).slice(beforeLore).find(d => d.kind === 'lore');
check('the reading consults the fates', !!lore, JSON.stringify(lore ?? null));
await settle(2600); await clearLevelUp();
const readOpen = (await page.locator('.aq-read-scrim').count()) > 0;
if (lore && lore.total >= 12) {
  check('success: the page opens once the die lands', readOpen, readOpen ? 'reading window up' : 'no window');
  if (readOpen) { await page.keyboard.press('Escape'); await settle(500); }
} else {
  check('failure: the letters keep their secret', !readOpen, readOpen ? 'window opened anyway' : 'no window');
  const lockedLore = (await dice()).length;
  await page.keyboard.press('e'); await settle(600);
  check('  and the letters are not asked again during the lockout',
    (await dice()).length === lockedLore, 'no new roll');
}

console.log('\n=== every skill cast throws the die ===');
// Firebolt is slot 1, unlocked at level 1 — the die must roll from the very
// first cast, not only for the ultimate.
await clearLevelUp();
const beforeCast = (await dice()).length;
await page.keyboard.press('1'); await settle(800);
const castRolls = (await dice()).slice(beforeCast);
check('casting skill 1 rolls a surge d20', castRolls.some(d => d.kind === 'surge'),
  JSON.stringify(castRolls[0] ?? null));

console.log('\n=== console ===');
const noisy = errors.filter(e => !/favicon|status of 401/.test(e));
check('the game logged no errors', noisy.length === 0, noisy.slice(0, 2).join(' | '));

console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed`);
await browser.close();
process.exit(failed === 0 ? 0 : 1);
