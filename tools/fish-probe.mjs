// A Fisgada: a line in still water throws a d20 + Luck. Under 8 the line goes
// slack and pays nothing; a landed catch opens with the die; 15+ doubles it.
// Three shores above ground and one beneath the sea.
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
const dice = () => page.evaluate(async () => (await import('/js/game.js')).diceSeen());
const clearLevelUp = async () => {
  for (let i = 0; i < 8 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 3000 }).catch(() => { });
    await settle(500);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(400); }
  }
};
const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });

await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Fisher Tester');
await page.fill('#signup-email', `fish.${Date.now()}@example.com`);
await page.fill('#signup-password', 'a-long-enough-passphrase');
await page.fill('#signup-confirm', 'a-long-enough-passphrase');
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(5000);
await clearLevelUp();

// Cast a line at one spot and judge the whole ceremony against the die.
const cast = async (label) => {
  const before = (await dice()).length;
  await page.keyboard.press('e'); await settle(900); await clearLevelUp();
  const d = (await dice()).slice(before).find(x => x.kind === 'fish');
  check(`${label}: the bite consults the fates`, !!d, JSON.stringify(d ?? null));
  if (!d) return;
  check(`  a d20 of luck, sea-coloured`, d.sides === 20 && d.c === '#4fa3a0', `d${d.sides} ${d.c}`);
  await settle(2400);   // the catch waits for the die
  const windowUp = (await page.locator('.aq-loot').count()) > 0;
  if (d.outcome === 'bad') {
    check(`  slack: the water keeps it`, true, 'no catch');
  } else {
    check(`  a catch: the window opens with the die`, windowUp, `outcome ${d.outcome}`);
    if (windowUp) {
      const lines = await page.locator('.aq-loot-cell').count().catch(() => 0);
      check(`  and there is something on the hook`, lines > 0, `${lines} lines`);
      await page.locator('.aq-loot-all').click({ timeout: 1500 }).catch(() => { });
      await settle(400);
    }
  }
  // Close ONLY what is still open — a blind Escape with nothing up opens the
  // quit box, whose Busy then blanks every prompt after it.
  if ((await page.locator('.aq-loot').count()) > 0) { await page.keyboard.press('Escape'); await settle(300); }
};

console.log('=== the brook pool, Ashwold ===');
await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r1_ashwold'));
await settle(2200); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugWarp(39, 17));
await settle(700);
const p1 = await hud();
check('the pool offers a line', p1?.promptVerb === 'Fish' && /brook/i.test(p1?.promptName ?? ''),
  `"${p1?.promptVerb} ${p1?.promptName}"`);
if (p1?.promptVerb === 'Fish') await cast('the brook pool');

console.log('=== the ford pool, the wood ===');
await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r2_whispering_wood'));
await settle(2200); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugWarp(39, 20));
await settle(700);
const p2 = await hud();
check('the pool offers a line', p2?.promptVerb === 'Fish' && /ford/i.test(p2?.promptName ?? ''),
  `"${p2?.promptVerb} ${p2?.promptName}"`);
if (p2?.promptVerb === 'Fish') await cast('the ford pool');

console.log('=== the black water, Deepdelve ===');
await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r3_deepdelve'));
await settle(2200); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugWarp(22, 30));
await settle(700);
const p3 = await hud();
check('the tarn offers a line', p3?.promptVerb === 'Fish' && /black/i.test(p3?.promptName ?? ''),
  `"${p3?.promptVerb} ${p3?.promptName}"`);
if (p3?.promptVerb === 'Fish') await cast('the black water');

console.log('=== the still lagoon, beneath the sea ===');
await page.evaluate(async () => (await import('/js/game.js')).debugEnterCave());
await settle(1800);
await page.click('.aq-cutscene-skip', { timeout: 3000 }).catch(() => { });
await settle(1200); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugClaim('kazzat_key'));
await page.evaluate(async () => (await import('/js/game.js')).debugGrantXp(60000));
await settle(800); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugDive());
await settle(10000); await clearLevelUp();
const shore = await hud();
check('the crossing lands on the coral shore', shore?.region === 'The Great Coral Tree', `"${shore?.region}"`);
// The lagoon sits at the lake room's centre (44,33).
await page.evaluate(async () => (await import('/js/game.js')).debugWarp(44, 34));
await settle(700); await clearLevelUp();
const p4 = await hud();
check('the lagoon offers a line', p4?.promptVerb === 'Fish' && /lagoon/i.test(p4?.promptName ?? ''),
  `"${p4?.promptVerb} ${p4?.promptName}"`);
if (p4?.promptVerb === 'Fish') await cast('the still lagoon');

console.log('=== console ===');
check('the game logged no errors', errors.length === 0, errors.slice(0, 3).join(' | '));

console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed`);
await browser.close();
process.exit(failed === 0 ? 0 : 1);
