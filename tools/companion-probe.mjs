// Companions fight like people now: they reach for a skill sometimes — and
// never throw the player's fate dice when they do.
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
await page.fill('#signup-name', 'Companion Tester');
await page.fill('#signup-email', `comp.${Date.now()}@example.com`);
await page.fill('#signup-password', 'a-long-enough-passphrase');
await page.fill('#signup-confirm', 'a-long-enough-passphrase');
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(5000); await clearLevelUp();

// Levels, so skills are unlocked for everyone.
await page.evaluate(async () => (await import('/js/game.js')).debugGrantXp(20000));
await settle(800); await clearLevelUp();

const before = (await dice()).length;
await page.evaluate(async () => (await import('/js/game.js')).debugEncounter());
await settle(600); await clearLevelUp();

// Watch the fight: a companion cast shows as a non-active hero in the channel
// pose (abl > 0). Sample fast for up to 35s.
let sawCast = false;
for (let i = 0; i < 120 && !sawCast; i++) {
  const ents = await page.evaluate(async () => (await import('/js/game.js')).lastEnts()) ?? [];
  if (ents.some(e => e.t === 'hero' && !e.active && !e.dead && (e.abl ?? 0) > 0.1)) sawCast = true;
  await settle(300);
  if (i % 20 === 19) await clearLevelUp();
}
check('a companion reached for a skill of their own', sawCast);

const surges = (await dice()).slice(before).filter(d => d.kind === 'surge');
check('and threw NO fate die doing it — the table is the player\'s', surges.length === 0,
  `${surges.length} surge rolls with no key pressed`);

check('the game logged no errors', errors.length === 0, errors.slice(0, 3).join(' | '));
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed`);
await browser.close();
process.exit(failed === 0 ? 0 : 1);
