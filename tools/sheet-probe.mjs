// Does the redesigned character window behave the way the brief asks?
//
//     node tools/sheet-probe.mjs
//
// The brief's complaint was vertical scrolling, and its requirements are mostly
// behavioural — tabs that switch without closing, state that survives closing,
// drag and drop, keyboard navigation, and no overlap at four resolutions. All of
// those are invisible to a screenshot, so they are asserted here.

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
const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });

const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
page.on('pageerror', e => errors.push(String(e)));

const settle = (ms = 700) => page.waitForTimeout(ms);
const text = async sel => { try { return (await page.textContent(sel, { timeout: 2000 })) ?? ''; } catch { return ''; } };
const openTab = async key => { await page.click(`#aq-tab-${key}`); await settle(500); };
const openSheet = async () => {
  if ((await page.locator('.aq-cw.shown').count()) === 0) { await page.keyboard.press('c'); await settle(1100); }
};
// The ✕ button, not a key. C would be typed into the search box, and Escape
// there clears the search first — both deliberate, and both asserted below, but
// neither is a reliable way for a test to simply close the window.
const closeSheet = async () => {
  if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.click('.aq-cw-close'); await settle(900); }
};

const player = { email: `sheet.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };

console.log('\n=== into the game ===');
await page.goto(`${CLIENT}/signup`);
await settle(2500);
await page.fill('#signup-name', 'Sheet Tester');
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await settle(7000);
await openSheet();
check('the character window opens', (await page.locator('.aq-cw.shown').count()) === 1);

console.log('\n=== tabs ===');

const tabs = await page.locator('.aq-tab').allTextContents();
for (const name of ['Character', 'Equipment', 'Skills', 'Inventory', 'Stats', 'Crafting', 'Quests']) {
  check(`there is a ${name} tab`, tabs.some(t => t.includes(name)));
}
check('the unbuilt tab is disabled rather than hidden',
  await page.locator('#aq-tab-quests').isDisabled());

// Switching must not close the window — that was the whole point of tabs.
await openTab('equipment');
check('switching tabs keeps the window open', (await page.locator('.aq-cw.shown').count()) === 1);
check('the Equipment panel is showing', (await page.locator('.aq-doll').count()) === 1);
check('the open tab is marked for assistive tech',
  (await page.getAttribute('#aq-tab-equipment', 'aria-selected')) === 'true');

console.log('\n=== no long scrolling ===');

// The brief's actual complaint. Every tab has to fit the panel it is given.
for (const key of ['character', 'equipment', 'skills', 'inventory', 'stats', 'crafting']) {
  await openTab(key);
  const fit = await page.evaluate(() => {
    const host = document.querySelector('.aq-cw-panel-host');
    return { over: host.scrollHeight - host.clientHeight, h: host.clientHeight };
  });
  check(`${key} fits without scrolling`, fit.over <= 8, `${fit.over}px past ${fit.h}px`);
}

const framed = await page.evaluate(() => {
  const frame = document.querySelector('.aq-cw-frame');
  return frame.scrollHeight - frame.clientHeight;
});
check('the frame itself never scrolls — only the panel does', framed <= 0, `${framed}px`);

console.log('\n=== keyboard ===');

await openTab('character');
await page.keyboard.press('ArrowRight');
await settle(400);
check('the right arrow moves to the next tab',
  (await page.getAttribute('#aq-tab-equipment', 'aria-selected')) === 'true');
await page.keyboard.press('ArrowLeft');
await settle(400);
check('the left arrow moves back',
  (await page.getAttribute('#aq-tab-character', 'aria-selected')) === 'true');

await openTab('inventory');
await page.click('.aq-search input');
await page.keyboard.type('ring');
await settle(500);
check('a space can be typed into the search box',
  (await page.inputValue('.aq-search input')).length === 4);
check('typing in the search box does not change tabs',
  (await page.getAttribute('#aq-tab-inventory', 'aria-selected')) === 'true');
await page.keyboard.press('ArrowLeft');
await settle(400);
check('and neither do arrow keys while typing in it',
  (await page.getAttribute('#aq-tab-inventory', 'aria-selected')) === 'true');

console.log('\n=== the pack: search, filter, sort, views ===');

const tiles = async () => page.locator('.aq-pack-item').count();
const filtered = await tiles();
check('the search filters the pack', filtered === 1, `${filtered} shown for "ring"`);
check('it says what is being hidden', (await text('.aq-pack-note')).includes('of'), await text('.aq-pack-note'));

await page.click('.aq-search-clear');
await settle(500);
const all = await tiles();
check('clearing the search restores the pack', all > filtered, `${filtered} → ${all}`);

await page.click('.aq-viewtoggle button:nth-child(2)');
await settle(400);
check('the compact view is a list', (await page.locator('.aq-pack.compact').count()) === 1);
check('compact rows name the item', (await text('.aq-pack-name')).length > 0, await text('.aq-pack-name'));
await page.click('.aq-viewtoggle button:nth-child(1)');
await settle(400);
check('the grid view comes back', (await page.locator('.aq-pack.grid').count()) === 1);

const firstByRarity = await page.locator('.aq-pack-item').first().getAttribute('aria-label');
await page.selectOption('.aq-select', 'Name');
await settle(500);
const firstByName = await page.locator('.aq-pack-item').first().getAttribute('aria-label');
check('sorting reorders the pack', firstByRarity !== firstByName,
  `${firstByRarity?.split(',')[0]} → ${firstByName?.split(',')[0]}`);

console.log('\n=== Escape means the nearer thing first ===');

await page.click('.aq-search input');
await page.keyboard.type('ring');
await settle(400);
await page.keyboard.press('Escape');
await settle(500);
check('Escape in the search box clears the search',
  (await page.inputValue('.aq-search input')) === '', `"${await page.inputValue('.aq-search input')}"`);
check('and does not close the window while there was something to clear',
  (await page.locator('.aq-cw.shown').count()) === 1);

await page.keyboard.press('Escape');
await settle(700);
check('Escape again, with nothing to clear, closes the window',
  (await page.locator('.aq-cw.shown').count()) === 0);
await openSheet();

console.log('\n=== state survives closing ===');

await openTab('stats');
await closeSheet();
await openSheet();
check('it reopens on the tab it was left on',
  (await page.getAttribute('#aq-tab-stats', 'aria-selected')) === 'true');

await openTab('inventory');
await page.click('.aq-search input');
await page.keyboard.type('boot');
await settle(500);
await closeSheet();
await openSheet();
check('the typed search is still there', (await page.inputValue('.aq-search input')) === 'boot');
check('and is still applied', (await tiles()) === 1, `${await tiles()} shown`);
await page.click('.aq-search-clear');
await settle(400);

console.log('\n=== the paper doll ===');

await openTab('equipment');
const slots = await page.locator('.aq-doll .aq-slot').count();
check('every slot in the catalogue is on the doll', slots === 17, `${slots} slots`);
check('empty slots carry a silhouette and a name',
  (await text('.aq-doll .aq-slot.empty .aq-slot-tag')).length > 0);
check('the slots that have no system are locked',
  (await page.locator('.aq-doll .aq-slot.locked').count()) === 2,
  `${await page.locator('.aq-doll .aq-slot.locked').count()} locked`);

const wornBefore = await page.locator('.aq-doll .aq-slot.filled').count();
const packBefore = await page.locator('.aq-pack-item').count();

// Drag the first pack item onto the slot it belongs in.
const item = page.locator('.aq-pack-item').first();
const label = await item.getAttribute('aria-label');
const slotName = label.split(', ')[1]?.split(' ').slice(1).join(' ');
await item.dragTo(page.locator('.aq-doll .aq-slot').filter({ hasText: new RegExp(slotName ?? 'Helmet', 'i') }).first());
await settle(800);

const wornAfter = await page.locator('.aq-doll .aq-slot.filled').count();
const packAfter = await page.locator('.aq-pack-item').count();
check('dragging an item onto its slot wears it',
  wornAfter > wornBefore || packAfter < packBefore,
  `worn ${wornBefore}→${wornAfter}, pack ${packBefore}→${packAfter}`);

await page.locator('.aq-doll .aq-slot.filled').first().click();
await settle(600);
check('clicking a worn slot takes it off',
  (await page.locator('.aq-pack-item').count()) > packAfter,
  `pack ${packAfter} → ${await page.locator('.aq-pack-item').count()}`);

console.log('\n=== the numbers are the real ones ===');

await openTab('skills');
const ability = (await text('.aq-card.ability')).replace(/\s+/g, ' ');
check('the signature ability shows a cooldown', /Cooldown\s*6s/.test(ability), ability.slice(0, 120));
check('and the mana cost the engine charges', /Mana cost\s*40/.test(ability), ability.slice(0, 160));

await openTab('stats');
const general = (await text('.aq-tab-grid.three')).replace(/\s+/g, ' ');
check('stats the game does not have are shown as pending, not invented',
  /Mining Speed —/.test(general) && /Fishing Speed —/.test(general), general.slice(-160));
check('the ones it does have carry numbers', /Movement Speed \d+/.test(general));
const pending = await page.locator('.aq-stat.pending').count();
check('pending stats are visibly dimmed', pending === 4, `${pending} dimmed`);

console.log('\n=== resolutions ===');

for (const [w, h, name] of [[1920, 1080, '1080p'], [2560, 1440, '1440p'], [3440, 1440, 'ultrawide'], [1366, 768, 'small laptop']]) {
  await page.setViewportSize({ width: w, height: h });
  await settle(600);
  await openTab('equipment');

  const layout = await page.evaluate(() => {
    const frame = document.querySelector('.aq-cw-frame');
    const host = document.querySelector('.aq-cw-panel-host');
    const r = frame.getBoundingClientRect();
    // Does anything stick out of the frame? That is the brief's "never let
    // controls overlap", measured rather than eyeballed.
    const escapes = [...frame.querySelectorAll('.aq-card, .aq-tab, .aq-slot')].filter(el => {
      const b = el.getBoundingClientRect();
      return b.right > r.right + 1 || b.left < r.left - 1;
    }).length;
    return {
      w: Math.round(r.width), h: Math.round(r.height),
      inViewport: r.right <= window.innerWidth + 1 && r.bottom <= window.innerHeight + 1,
      over: host.scrollHeight - host.clientHeight,
      escapes,
    };
  });

  check(`${name}: the window fits the screen`, layout.inViewport, `${layout.w}×${layout.h}`);
  check(`${name}: nothing escapes the frame`, layout.escapes === 0, `${layout.escapes} elements`);
  check(`${name}: the paper doll fits without scrolling`, layout.over <= 8, `${layout.over}px past the panel`);
  await page.screenshot({ path: `tools/shots/sheet-res-${w}x${h}.png` });
}

await page.setViewportSize({ width: 1920, height: 1080 });
await settle(500);

console.log('\n=== console ===');
const noisy = errors.filter(e => !/favicon|status of 401/.test(e));
check('the window logged no errors', noisy.length === 0, noisy.slice(0, 2).join(' | '));

await browser.close();
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed\n`);
process.exit(failed === 0 ? 0 : 1);
