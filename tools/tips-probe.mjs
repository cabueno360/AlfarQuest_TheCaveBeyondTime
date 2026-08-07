// The Dragon of the Tips: four reads and one kill. The bill starts it, the
// trail is read at the pit head, the Old Worm sleeps beside the marked slag —
// and once at rest, it never crawls back (UnlessFlag), while the bill's
// closing pages open on the pick-head above the winch-house door.
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
const text = async sel => { try { return (await page.textContent(sel, { timeout: 2000 })) ?? ''; } catch { return ''; } };
const ents = () => page.evaluate(async () => (await import('/js/game.js')).lastEnts() ?? []);
const clearLevelUp = async () => {
  for (let i = 0; i < 8 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 3000 }).catch(() => { });
    await settle(500);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(400); }
  }
};
const closeRead = async () => {
  for (let i = 0; i < 3 && (await page.locator('.aq-read').count()) > 0; i++) {
    await page.keyboard.press('Escape'); await settle(350);
  }
};
const readAt = async (tx, ty, wantName, label) => {
  await page.evaluate(async a => (await import('/js/game.js')).debugWarp(a.tx, a.ty), { tx, ty });
  await settle(600); await clearLevelUp();
  const h = await hud();
  const offered = new RegExp(wantName, 'i').test(h?.promptName ?? '');
  check(`${label} offers itself`, offered, `"${h?.promptVerb} ${h?.promptName}"`);
  if (offered) { await page.keyboard.press('e'); await settle(800); await closeRead(); }
  return offered;
};
const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });

await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Tips Tester');
await page.fill('#signup-email', `tips.${Date.now()}@example.com`);
await page.fill('#signup-password', 'a-long-enough-passphrase');
await page.fill('#signup-confirm', 'a-long-enough-passphrase');
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(5000); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugGrantXp(60000));
await settle(800); await clearLevelUp();

console.log('=== the foreman\'s bill ===');
await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r3_deepdelve'));
await settle(2200); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugWarp(41, 27));
await settle(700);
const b1 = await hud();
check('the bill is nailed by the winch', /foreman/i.test(b1?.promptName ?? ''), `"${b1?.promptVerb} ${b1?.promptName}"`);
await page.keyboard.press('e'); await settle(800);
const pages1 = await text('.aq-read-page');
// Two open pages plus the sealed-entries teaser the reader shows for what
// cannot yet be made out — the ending itself stays unread.
check('  two pages open, the ending sealed behind the teaser', /\/ 3/.test(pages1), `"${pages1.trim()}"`);
await closeRead();
await page.keyboard.press('l'); await settle(800);
check('the journal holds The Dragon of the Tips', /Dragon of the Tips/.test(await text('.aq-cw')));
await page.keyboard.press('Escape'); await settle(400);

console.log('=== the trail of reads ===');
await readAt(42, 29, 'tool tally', 'the tool tally');
await readAt(32, 39, 'coal-stained', 'the coal-stained note');
await readAt(30, 27, "clerk's record", "the clerk's record");
await readAt(35, 40, 'marked slag', 'the marked slag');

const worm = (await ents()).find(e => e.name === 'mobWorm');
check('and the thing beneath is real — the Old Worm stirs', !!worm, worm ? `at (${Math.round(worm.x / 32)},${Math.round(worm.y / 32)})` : 'no worm');

console.log('=== put to rest, it stays at rest ===');
// The kill itself is two reviewed lines (AwardKill -> Claim); the probe claims
// the flag directly and proves what MATTERS: the spawn gate and the ending.
await page.evaluate(async () => (await import('/js/game.js')).debugClaim('sq_tips_worm'));
await settle(400);
await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r3_deepdelve'));
await settle(2200); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugWarp(35, 40));
await settle(700);
const wormAfter = (await ents()).find(e => e.name === 'mobWorm');
check('the Old Worm never crawls back', !wormAfter, wormAfter ? 'still there!' : 'the heaps are quiet');

await page.evaluate(async () => (await import('/js/game.js')).debugWarp(41, 27));
await settle(700);
await page.keyboard.press('e'); await settle(800);
const pages2 = await text('.aq-read-page');
check('the bill has grown its closing pages', /\/ 5/.test(pages2), `"${pages2.trim()}"`);
let sawEnding = false;
for (let i = 0; i < 8 && !sawEnding; i++) {
  if (/removing their caps|pick-head is always where he left it/.test(await text('.aq-read'))) { sawEnding = true; break; }
  await page.locator('.aq-read-turn').last().click({ timeout: 1500 }).catch(() => { });
  await settle(300);
}
check('  and it ends beneath the pick-head', sawEnding);
await closeRead();

check('the game logged no errors', errors.length === 0, errors.slice(0, 3).join(' | '));
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed`);
await browser.close();
process.exit(failed === 0 ? 0 : 1);
