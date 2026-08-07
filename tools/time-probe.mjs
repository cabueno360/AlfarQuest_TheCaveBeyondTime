// The Cave is Beyond Time: the world clock runs honest above ground and fast
// below — ×2 in the Cistern, ×3 on the coral shore. Kazzat knows why.
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
const clearLevelUp = async () => {
  for (let i = 0; i < 8 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 3000 }).catch(() => { });
    await settle(500);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(400); }
  }
};
// Hours the clock gains over a fixed real wait, with midnight wrap.
const gainOver = async (ms) => {
  const a = (await hud())?.timeOfDay ?? 0;
  await settle(ms);
  const b = (await hud())?.timeOfDay ?? 0;
  return ((b - a) + 24) % 24;
};
const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });

await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Time Tester');
await page.fill('#signup-email', `time.${Date.now()}@example.com`);
await page.fill('#signup-password', 'a-long-enough-passphrase');
await page.fill('#signup-confirm', 'a-long-enough-passphrase');
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(5000); await clearLevelUp();

console.log('=== honest time above, stolen time below ===');
const surface = await gainOver(5000);
check('the surface keeps honest time', surface > 0.02 && surface < 0.12, `${surface.toFixed(3)}h in 5s`);

await page.evaluate(async () => (await import('/js/game.js')).debugEnterCave());
await settle(1800);
await page.click('.aq-cutscene-skip', { timeout: 3000 }).catch(() => { });
await settle(1200); await clearLevelUp();
const cistern = await gainOver(5000);
const r1 = cistern / surface;
check('the Cistern spends the hours double', r1 > 1.5 && r1 < 2.6, `×${r1.toFixed(2)}`);

await page.evaluate(async () => (await import('/js/game.js')).debugClaim('kazzat_key'));
await page.evaluate(async () => (await import('/js/game.js')).debugGrantXp(60000));
await settle(800); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugDive());
await settle(10000); await clearLevelUp();
const shore = await gainOver(5000);
const r2 = shore / surface;
check('the coral shore leans harder still', r2 > 2.3 && r2 < 3.8, `×${r2.toFixed(2)}`);

console.log('=== the reckoning at the mouth ===');
// A real day of slippage takes ~36 real minutes even at depth 2; the probe
// cannot sit that out, so it verifies the mechanism's pieces instead: the
// slipped-hours ledger accrues (the ratios above ARE the accrual), and the
// surfacing line only speaks when a full day was stolen — here it stays
// silent, which is itself the correct behaviour for a short delve.
await page.evaluate(async () => (await import('/js/game.js')).debugLeaveCave());
await settle(1500); await clearLevelUp();
const back = await hud();
check('a short delve surfaces without ceremony', back?.stage === 1 && !/older than we left/.test(back?.speechLine ?? ''),
  `stage ${back?.stage}`);
const above = await gainOver(5000);
check('and the surface is honest again', above / surface > 0.6 && above / surface < 1.6, `×${(above / surface).toFixed(2)}`);

console.log('=== Kazzat knows ===');
await page.evaluate(async () => (await import('/js/game.js')).debugEnterCave());
await settle(1800);
await page.click('.aq-cutscene-skip', { timeout: 3000 }).catch(() => { });
await settle(1200); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugTalkTo('kazzat'));
await settle(900);
const topics = await page.locator('.aq-talk-topic').allTextContents().catch(() => []);
check('he will answer why the world ages', topics.some(t => /older when we go back up/i.test(t)),
  `${topics.length} topics`);

check('the game logged no errors', errors.length === 0, errors.slice(0, 3).join(' | '));
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed`);
await browser.close();
process.exit(failed === 0 ? 0 : 1);
