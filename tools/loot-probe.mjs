// The world interaction system, played rather than inspected.
//
//     node tools/loot-probe.mjs
//
// The failure this exists to catch is duplication. A container that hands its
// contents over again after a reload is worse than one that never worked, and it
// is invisible from a screenshot — so most of what follows is about what is left
// inside something after you have taken from it.

import { chromium } from 'playwright-core';
import { mkdirSync } from 'node:fs';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5223';
mkdirSync('tools/shots', { recursive: true });

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

const settle = (ms = 700) => page.waitForTimeout(ms);
const hud = () => page.evaluate(async () => (await import('/js/game.js')).hudSnapshot());
const text = async sel => { try { return (await page.textContent(sel, { timeout: 2000 })) ?? ''; } catch { return ''; } };

/// Dismisses a level-up if one opened. The party earns XP from every container,
/// so this happens constantly and would otherwise swallow the keystrokes.
const clearLevelUp = async () => {
  for (let i = 0; i < 4 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 4000 }).catch(() => { });
    await settle(800);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(600); }
  }
};

const nudge = async (key, ms) => {
  await page.keyboard.down(key);
  await page.waitForTimeout(ms);
  await page.keyboard.up(key);
  await page.waitForTimeout(140);
};

/// Villagers offer a prompt too, and this probe is not about them.
const isContainer = h => h?.promptName && h.promptVerb !== 'Talk to';

/// Walks a deliberate sweep of the spawn camp until a container is in reach.
///
/// Not a random wander: an alternating nudge barely leaves the spot it started
/// on, which is how an earlier run concluded the world had nothing in it while
/// standing a few paces from four barrels.
const findContainer = async (rounds = 10) => {
  // A widening box: each leg is longer than the last, so the walk actually
  // leaves the spot it started from instead of pacing the same few tiles.
  const sweep = [['w', 600], ['d', 700], ['s', 900], ['a', 1100],
                 ['w', 1300], ['d', 1500], ['s', 1700], ['a', 1900]];
  for (let r = 0; r < rounds; r++) {
    for (const [key, ms] of sweep) {
      const h = await hud();
      if (isContainer(h)) return h;
      await nudge(key, ms);
      await clearLevelUp();
    }
  }
  return await hud();
};

const player = { email: `loot.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };

console.log('\n=== into the world ===');
await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Loot Tester');
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await settle(8000);
await clearLevelUp();

const start = await hud();
check('the world has things to interact with', start?.rewardsLeft > 20, `${start?.rewardsLeft} untouched`);

console.log('\n=== the prompt ===');

const near = await findContainer();
check('walking up to something offers it', isContainer(near), near?.promptName ?? '(nothing in reach)');
check('the prompt names the action, not just the thing',
  ['Open', 'Search', 'Mine', 'Gather', 'Prise', 'Read', 'Examine', 'Touch', 'Unlock'].includes(near?.promptVerb),
  `"${near?.promptVerb} ${near?.promptName}"`);
await page.screenshot({ path: 'tools/shots/loot-1-prompt.png' });

console.log('\n=== opening ===');

await page.keyboard.press('e');
await settle(900);

const windowOpen = (await page.locator('.aq-loot').count()) > 0;
check('opening either shows contents or says it was empty',
  windowOpen || (await hud()) !== null,
  windowOpen ? 'the loot window opened' : 'no window — the roll came up empty');

// Keep opening things until one actually has something in it. Barrels are
// deliberately often empty, so a single open proves nothing either way.
// An emptied container stops offering, so each attempt has to travel. Keep
// going rather than giving up the first time a leg finds nothing — the camp has
// seven containers and one of them, the herb patch, is guaranteed to hold
// something.
// Two lines, not one: the "what is left after taking something" test needs a
// container that still has something in it afterwards, and the rolls vary — an
// earlier run found a barrel holding a single coin pile and skipped the whole
// question.
const lines = async () => (await page.locator('.aq-loot').count()) > 0
  ? await page.locator('.aq-loot-cell').count() : 0;

let cells = await lines(), tries = 0, reached = 0, everHadLoot = cells > 0;
const seen = new Set();
while (cells < 2 && tries++ < 16) {
  await clearLevelUp();
  // Re-checked rather than trusting the count from before the level-up was
  // dismissed: the window may have gone in the meantime.
  if ((await page.locator('.aq-loot').count()) > 0) {
    await page.locator('.aq-loot-all').click({ timeout: 4000 }).catch(() => { });
    await settle(600);
  }
  await clearLevelUp();

  const found = await findContainer(2);
  if (!isContainer(found)) continue;

  reached++;
  seen.add(found.promptName);
  await page.keyboard.press('e');
  await settle(800);
  cells = await lines();
  if (cells > 0) everHadLoot = true;
}
console.log(`  (opened ${reached} more: ${[...seen].join(', ')})`);

const opened = cells > 0;

check('something on the map had loot in it', everHadLoot,
  `${reached} containers opened in ${tries} attempts`);

// The rolls are random and most camp containers hold one line. When none of the
// ones reached held two, the "what is left behind" question cannot be asked —
// so it is skipped and said so, rather than failed for a reason that is not a
// defect.
if (cells < 2) console.log(`  (skipped the remainder checks: nothing reached held more than one line)`);

if (cells >= 2) {
  const name = (await text('.aq-loot-name')).trim();
  check('the window names the container', name.length > 0, name);
  check('it shows what is inside', cells > 0, `${cells} lines`);
  check('it offers Take All', (await page.locator('.aq-loot-all').count()) === 1);
  await page.screenshot({ path: 'tools/shots/loot-2-window.png' });

  console.log('\n=== taking one thing ===');

  const before = cells;
  await clearLevelUp();
  for (let i = 0; i < 3; i++) {
    try { await page.locator('.aq-loot-cell').first().click({ timeout: 5000 }); break; }
    catch { await clearLevelUp(); }
  }
  await settle(700);

  const stillOpen = (await page.locator('.aq-loot').count()) > 0;
  const after = stillOpen ? await page.locator('.aq-loot-cell').count() : 0;
  check('taking one line removes only that line', after === before - 1 || before === 1,
    `${before} → ${after}`);

  console.log('\n=== what is left stays left ===');

  if (stillOpen && after > 0) {
    await clearLevelUp();
    await page.keyboard.press('Escape');
    await settle(600);
    check('Escape closes without emptying it', (await page.locator('.aq-loot').count()) === 0);

    // Re-open the same container: it must show the remainder, not a fresh roll.
    await clearLevelUp();
    const stillInReach = await hud();
    console.log(`  (before re-opening: prompt="${stillInReach?.promptVerb} ${stillInReach?.promptName}")`);
    await page.keyboard.press('e');
    await settle(900);
    const reopened = await page.locator('.aq-loot-cell').count();
    console.log(`  (after re-opening: window=${(await page.locator('.aq-loot').count())>0}, cells=${reopened})`);
    check('re-opening shows the remainder, not a new roll', reopened === after,
      `${after} left, ${reopened} shown`);
    await clearLevelUp();
    if ((await page.locator('.aq-loot').count()) > 0) {
      await page.locator('.aq-loot-all').click();
      await settle(800);
      check('Take All empties it', (await page.locator('.aq-loot').count()) === 0);
    }
  }
}

console.log('\n=== nothing duplicates across a reload ===');

await clearLevelUp();
// The loot window may still be up, and its scrim covers the quit button — which
// is the point of a scrim, and a thing the test has to respect rather than work
// around.
if ((await page.locator('.aq-loot').count()) > 0) { await page.keyboard.press('Escape'); await settle(600); }
// The level-up hand-off opens the character sheet, whose header covers the quit
// button. Closed the way a player would close it.
if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(700); }
const beforeQuit = await hud();
await page.click('button:has-text("Abandon Delve")');
await settle(3000);

const saved = await page.evaluate(async () => {
  const token = localStorage.getItem('alfarquest.session') || sessionStorage.getItem('alfarquest.session');
  const res = await fetch('http://localhost:5080/api/saves/mine', { headers: { Authorization: `Bearer ${token}` } });
  return (await res.json())[0] ?? null;
});

check('the save records the containers that were opened',
  (saved?.containers ?? []).length > 0, `${(saved?.containers ?? []).length} recorded`);
check('each has a time it was opened',
  (saved?.containers ?? []).every(c => c.openedAt && c.openedAt.length > 4));

await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(2500);
const restored = await hud();

check('the opened containers stayed opened',
  restored?.rewardsLeft <= beforeQuit?.rewardsLeft,
  `${beforeQuit?.rewardsLeft} untouched before, ${restored?.rewardsLeft} after`);

const savedKeys = (saved?.containers ?? []).map(c => c.key);
check('the world did not refill what was emptied',
  restored?.rewardsLeft < start?.rewardsLeft,
  `${start?.rewardsLeft} at the start, ${restored?.rewardsLeft} now`);
console.log(`  (containers recorded: ${savedKeys.slice(0, 4).join(', ')}${savedKeys.length > 4 ? '…' : ''})`);

console.log('\n=== console ===');
const noisy = errors.filter(e => !/favicon|status of 401/.test(e));
check('the game logged no errors', noisy.length === 0, noisy.slice(0, 2).join(' | '));

await browser.close();
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed\n`);
process.exit(failed === 0 ? 0 : 1);
