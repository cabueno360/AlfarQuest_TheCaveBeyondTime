// The Now-Playing banner follows the player: the wood re-titles it, the cave
// re-titles it, and coming back up re-titles it again.
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
const banner = async () => (await page.textContent('.aq-nowplaying span', { timeout: 3000 }).catch(() => '')) ?? '';
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
await page.fill('#signup-name', 'Banner Tester');
await page.fill('#signup-email', `banner.${Date.now()}@example.com`);
await page.fill('#signup-password', 'a-long-enough-passphrase');
await page.fill('#signup-confirm', 'a-long-enough-passphrase');
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(5000); await clearLevelUp();

check('Ashwold: the banner names the ballad', /Ballad of the Wandering/.test(await banner()), await banner());

await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r2_whispering_wood'));
await settle(2500); await clearLevelUp();
check('the wood re-titles it — The Cleric', /The Cleric(?!, Pt)/.test(await banner()), await banner());

await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r3_deepdelve'));
await settle(2500); await clearLevelUp();
check('Deepdelve re-titles it — Crystal Deep (Intro)', /Crystal Deep \(Intro\)/.test(await banner()), await banner());

await page.evaluate(async () => (await import('/js/game.js')).debugEnterCave());
await settle(2000);
await page.click('.aq-cutscene-skip', { timeout: 3000 }).catch(() => { });
await settle(2000); await clearLevelUp();
check('the cave re-titles it — Into the Crystal Deep', /Into the Crystal Deep/.test(await banner()), await banner());

await page.evaluate(async () => (await import('/js/game.js')).debugLeaveCave());
await settle(2500); await clearLevelUp();
check('back above ground, the intro again', /Crystal Deep \(Intro\)/.test(await banner()), await banner());

check('the game logged no errors', errors.length === 0, errors.slice(0, 3).join(' | '));
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed`);
await browser.close();
process.exit(failed === 0 ? 0 : 1);
