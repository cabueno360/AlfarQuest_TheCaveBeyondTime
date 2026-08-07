// The Four of the Feast Day: a quest read, not fought. The notice starts it,
// the ledger and the chalk carry it, the cave keeps its end — and the board's
// own last pages open only when the last words have been read.
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
const clearLevelUp = async () => {
  for (let i = 0; i < 6 && (await page.locator('.aq-levelup').count()) > 0; i++) {
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
const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });

await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Feast Tester');
await page.fill('#signup-email', `feast.${Date.now()}@example.com`);
await page.fill('#signup-password', 'a-long-enough-passphrase');
await page.fill('#signup-confirm', 'a-long-enough-passphrase');
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(5000); await clearLevelUp();

console.log('=== the notice on the board ===');
await page.evaluate(async () => (await import('/js/game.js')).debugWarp(40, 30));
await settle(700);
const b1 = await hud();
check('the notice post offers itself', /notice/i.test(b1?.promptName ?? ''), `"${b1?.promptVerb} ${b1?.promptName}"`);
await page.keyboard.press('e'); await settle(800);
check('the little book opens', (await page.locator('.aq-read').count()) > 0);
const before = await text('.aq-read');
check('  and the closing pages are still sealed', !/Mera climbed upon the bench/.test(before), 'ending unread');
await closeRead();

console.log('=== the quest is taken by reading ===');
await page.keyboard.press('l'); await settle(800);
const journal = await text('.aq-cw');
check('the journal holds The Four of the Feast Day', /Feast Day/.test(journal));
await page.keyboard.press('Escape'); await settle(400);

console.log('=== the forester\'s ledger ===');
await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r2_whispering_wood'));
await settle(2200); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugWarp(20, 14));
await settle(700);
const p2 = await hud();
check('the high seat offers the ledger', /high seat|platform/i.test(p2?.promptName ?? ''), `"${p2?.promptVerb} ${p2?.promptName}"`);
await page.keyboard.press('e'); await settle(800);
check('  the ledger reads', /nineteen years|tally/i.test(await text('.aq-read')) || (await page.locator('.aq-read').count()) > 0);
await closeRead();

console.log('=== the miner\'s chalk ===');
await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r3_deepdelve'));
await settle(2200); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugWarp(38, 24));
await settle(700);
const p3 = await hud();
check('the loaded cart offers the chalk', /cart/i.test(p3?.promptName ?? ''), `"${p3?.promptVerb} ${p3?.promptName}"`);
await page.keyboard.press('e'); await settle(800);
await closeRead();

console.log('=== what the cave kept ===');
await page.evaluate(async () => (await import('/js/game.js')).debugEnterCave());
await settle(1800);
await page.click('.aq-cutscene-skip', { timeout: 3000 }).catch(() => { });
await settle(1200); await clearLevelUp();

let campSeen = false;
for (const [tx, ty] of [[42, 48], [41, 47], [42, 47], [41, 48], [43, 48]]) {
  await page.evaluate(async a => (await import('/js/game.js')).debugWarp(a.tx, a.ty), { tx, ty });
  await settle(500);
  const h = await hud();
  if (/cold camp/i.test(h?.promptName ?? '')) { campSeen = true; break; }
}
check('the cold camp waits in the first chamber', campSeen);
if (campSeen) { await page.keyboard.press('e'); await settle(800); await closeRead(); }

let wallSeen = false;
for (const [tx, ty] of [[16, 28], [17, 28], [16, 27], [15, 28], [17, 29]]) {
  await page.evaluate(async a => (await import('/js/game.js')).debugWarp(a.tx, a.ty), { tx, ty });
  await settle(500); await clearLevelUp();
  const h = await hud();
  if (/words cut/i.test(h?.promptName ?? '')) { wallSeen = true; break; }
}
check('the chiselled words wait a room deeper', wallSeen);
if (wallSeen) { await page.keyboard.press('e'); await settle(800); await closeRead(); }

console.log('=== word comes back to the board ===');
await page.evaluate(async () => (await import('/js/game.js')).debugLeaveCave());
await settle(1800); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r1_ashwold'));
await settle(2200); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugWarp(40, 30));
await settle(700);
await page.keyboard.press('e'); await settle(800);
check('the board reopens', (await page.locator('.aq-read').count()) > 0);
const pageCount = await text('.aq-read-page');
check('  grown by its sealed pages — the village answers', /\/ 10/.test(pageCount), `"${pageCount.trim()}"`);
// Page to the end and read the closing.
let sawEnding = false;
for (let i = 0; i < 12 && !sawEnding; i++) {
  if (/Mera climbed upon the bench|knew the words by heart/.test(await text('.aq-read'))) { sawEnding = true; break; }
  await page.locator('.aq-read-turn').last().click({ timeout: 1500 }).catch(() => { });
  await settle(300);
}
check('  and the last page is Mera taking the notice down', sawEnding);
await closeRead();

check('the game logged no errors', errors.length === 0, errors.slice(0, 3).join(' | '));
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed`);
await browser.close();
process.exit(failed === 0 ? 0 : 1);
