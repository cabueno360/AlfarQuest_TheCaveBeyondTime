// The merchant & trading system, played rather than inspected.
//
//     node tools/shop-probe.mjs
//
// What this exists to catch is money that does not add up: a purchase that takes
// the gold and not the shelf, a sale that pays from the wrong purse, a merchant
// who buys what their trade says they never would. So most of what follows is
// arithmetic — gold before, gold after — and whose gold it was.

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

const settle = (ms = 600) => page.waitForTimeout(ms);
const game = () => import('/js/game.js');
const hud = () => page.evaluate(async () => (await import('/js/game.js')).hudSnapshot());
const facts = () => page.evaluate(async () => (await import('/js/game.js')).merchantFacts());
const markers = () => page.evaluate(async () => (await import('/js/game.js')).npcMarkers());
const trade = id => page.evaluate(async i => (await import('/js/game.js')).debugTradeWith(i), id);
const soundsSeen = () => page.evaluate(async () => (await import('/js/game.js')).soundFamiliesSeen());
const resetSounds = () => page.evaluate(async () => (await import('/js/game.js')).resetSoundFamilies());

const clearLevelUp = async () => {
  for (let i = 0; i < 3 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 3000 }).catch(() => { });
    await settle(500);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(400); }
  }
};

/// The gold number the shop header shows for the trading hero. The coin-fly
/// animation adds a second ◈, so the first run of digits is the amount.
const shopGold = async () => {
  const t = (await page.textContent('.aq-shop-gold').catch(() => '')) ?? '';
  const m = t.match(/(\d+)/);
  return m ? Number(m[1]) : null;
};

const rowsIn = col => page.locator(`.aq-shop-col:nth-child(${col}) .aq-shop-row`);
const openShop = async id => { await trade(id); await settle(700); };

const player = { email: `shop.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };

console.log('\n=== into the world ===');
await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Shop Tester');
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
// Wait for the world to be REALLY up, not just apparently: the interactive
// Blazor render restarts the game once at startup, so hudSnapshot flickers
// stage-1 for a frame while a fresh Snapshot is still empty. Gate on the
// authoritative thing — a fresh Snapshot with villagers in it. Every overworld
// region has NPCs, so this is true exactly when the world is built and stable.
// The world settles a few seconds after the page loads — the maps fetch and the
// world builds through a brief transient. A plain WAIT past that transient is
// more reliable than sampling through it: polling Snapshot hard during startup
// can itself catch a half-built frame. Clear the transient, then confirm the
// world has villagers with a slow, gentle poll.
await page.waitForTimeout(4500);
await page.waitForFunction(() => {
  try { return (JSON.parse(DotNet.invokeMethod('AlfarQuest.Client', 'Snapshot')).npcs ?? []).length > 0; }
  catch { return false; }
}, null, { timeout: 20000, polling: 1000 });
await settle(1500);
await clearLevelUp();

// ------------------------------------------------------------------
console.log('\n=== the trading model ===');

const f = await facts();
const ms = f?.merchants ?? [];
check('the game defines merchants', ms.length >= 5, `${ms.length} shopkeepers`);
check('every merchant is a shop-role villager', ms.length > 0 && ms.every(m => m.isShopNpc),
  ms.filter(m => !m.isShopNpc).map(m => m.npcId).join(', ') || 'all');
check('a merchant sells dearer than they buy back',
  ms.every(m => !m.sample || m.sample.buy > m.sample.sell),
  ms.map(m => m.sample && `${m.sample.id} ${m.sample.buy}/${m.sample.sell}`).filter(Boolean).slice(0, 3).join(' · '));
check('every merchant keeps stock', ms.every(m => m.stockLines > 0));
check('one is a rare/special merchant', ms.some(m => m.special), ms.filter(m => m.special).map(m => m.name).join(', '));
check('one trader buys anything, others are selective',
  ms.some(m => m.buysAnything) && ms.some(m => !m.buysAnything));
check('restock is architecture, switched off', ms.every(m => m.restock === 'None'));

// ------------------------------------------------------------------
console.log('\n=== a merchant is distinguishable from anyone else ===');

const marks = await markers();
const shops = marks.filter(n => n.merchant);
const folk = marks.filter(n => !n.merchant);
check('some villagers are marked as merchants', shops.length >= 3, shops.map(n => n.name).join(', '));
check('and some are plainly not', folk.length >= 3, `${folk.length} non-merchants`);

// ------------------------------------------------------------------
console.log('\n=== opening the blacksmith ===');

await openShop('smith');
check('pressing trade opens the shop window', (await page.locator('.aq-shop').count()) > 0);
check('the window names the merchant', (await page.textContent('.aq-shop-name')).includes('Dagna'),
  (await page.textContent('.aq-shop-name').catch(() => '')).trim());
check('and their profession', (await page.textContent('.aq-shop-role')).toLowerCase().includes('blacksmith'));
check('the greeting offers Buy, Sell, Ask, Goodbye', (await page.locator('.aq-shop-choice').count()) === 4);
await page.screenshot({ path: 'tools/shots/shop-1-greeting.png' });

// ------------------------------------------------------------------
console.log('\n=== buying ===');

await page.locator('.aq-shop-choice', { hasText: 'Buy' }).first().click();
await settle(400);
check('Buy opens the two-panel counter', (await page.locator('.aq-shop-col').count()) === 2);
const shelfCount = await rowsIn(1).count();
check('the merchant has wares on the shelf', shelfCount > 0, `${shelfCount} lines`);
check('a category filter is offered', (await page.locator('.aq-shop-filter').count()) > 1);
await page.screenshot({ path: 'tools/shots/shop-2-buy.png' });

const goldBefore = await shopGold();
check('the trading hero starts with gold', goldBefore > 0, `◈${goldBefore}`);

// Select the first ware (the iron sword) and read its price off the Buy button.
await rowsIn(1).first().click();
await settle(300);
const boughtName = (await page.textContent('.aq-shop-detail-name').catch(() => '')).trim();
const buyLabel = (await page.textContent('.aq-btn.buy').catch(() => '')) ?? '';
const buyPrice = Number((buyLabel.match(/(\d+)/) ?? [])[1]);
const firstQtyBefore = (await rowsIn(1).first().textContent()).match(/×(\d+)/)?.[1];

await resetSounds();
await page.locator('.aq-btn.buy').click();
await settle(500);

const goldAfter = await shopGold();
check('buying subtracts exactly the price', goldBefore - goldAfter === buyPrice,
  `◈${goldBefore} → ◈${goldAfter}, price ◈${buyPrice} (${boughtName})`);

const firstQtyAfter = (await rowsIn(1).first().textContent()).match(/×(\d+)/)?.[1];
check('the shelf loses one of what was bought', Number(firstQtyAfter) === Number(firstQtyBefore) - 1,
  `×${firstQtyBefore} → ×${firstQtyAfter}`);

const packHas = await rowsIn(2).filter({ hasText: boughtName }).count();
check('the item lands in the buying hero\'s pack', packHas > 0);

check('a coin sounds on the purchase', (await soundsSeen()).includes('coin'));

check('the shop notes the sale', (await page.textContent('.aq-shop-notice').catch(() => '')).includes('Bought'));

// ------------------------------------------------------------------
console.log('\n=== the spread: sell it back for less ===');

// The sword is now in the pack. Sell it back to the same smith (who buys blades).
await page.locator('.aq-shop-col:nth-child(2) .aq-shop-row', { hasText: boughtName }).first().click();
await settle(300);
const sellLabel = (await page.textContent('.aq-btn.sell').catch(() => '')) ?? '';
const sellPrice = Number((sellLabel.match(/(\d+)/) ?? [])[1]);
check('the buy-back price is below the sale price', sellPrice < buyPrice, `sell ◈${sellPrice} < buy ◈${buyPrice}`);

const goldBeforeSell = await shopGold();
await page.locator('.aq-btn.sell').click();
await settle(500);
const goldAfterSell = await shopGold();
check('selling pays exactly the buy-back price', goldAfterSell - goldBeforeSell === sellPrice,
  `◈${goldBeforeSell} → ◈${goldAfterSell}`);

// ------------------------------------------------------------------
console.log('\n=== the pack and purse belong to the active hero ===');

// Hero 1 has traded (bought a sword, sold it back) and so is short of the ◈120
// the untouched heroes still hold — which is the whole point: gold is per hero.
const heroOneGold = await shopGold();
const heroTabs = page.locator('.aq-shop-hero');
const tabCount = await heroTabs.count();
check('the shop offers the party as trading heroes', tabCount >= 2, `${tabCount} heroes`);

if (tabCount >= 2) {
  await heroTabs.nth(1).click();
  await settle(500);
  const heroTwoGold = await shopGold();
  const packHeader = (await page.textContent('.aq-shop-col:nth-child(2) .aq-shop-col-head').catch(() => '')) ?? '';
  check('switching heroes shows a different purse', heroTwoGold !== heroOneGold,
    `hero 1 ◈${heroOneGold}, hero 2 ◈${heroTwoGold}`);
  check('the pack is labelled for the trading hero', packHeader.includes('Pack'), packHeader.trim());
  // Back to hero 1: their spend is still theirs, not shared across the party.
  await heroTabs.nth(0).click();
  await settle(400);
  check('the first hero\'s spending stuck to them', (await shopGold()) === heroOneGold,
    `◈${await shopGold()}`);
}

// ------------------------------------------------------------------
console.log('\n=== filtering the shelves ===');

const allRows = await rowsIn(1).count();
// Armor is one of the smith's categories; picking it should narrow the shelf.
const armorTab = page.locator('.aq-shop-filter', { hasText: 'Armor' });
if (await armorTab.count() > 0) {
  await armorTab.first().click();
  await settle(300);
  const armorRows = await rowsIn(1).count();
  check('a category filter narrows the shelf', armorRows > 0 && armorRows < allRows,
    `${allRows} → ${armorRows} with Armor`);
  await page.locator('.aq-shop-filter', { hasText: 'All' }).first().click();
  await settle(200);
  check('clearing the filter shows everything again', (await rowsIn(1).count()) === allRows);
} else {
  check('a category filter narrows the shelf', false, 'no Armor tab found');
}

// ------------------------------------------------------------------
console.log('\n=== a merchant refuses what they do not deal in ===');

// Put a blade in the second hero's pack to offer the alchemist. Buy it here at
// the smith, as that hero (still flush with coin), and carry it over.
await heroTabs.nth(1).click(); await settle(400);        // the cleric, ◈120
await rowsIn(1).first().click(); await settle(200);       // the iron sword
await page.locator('.aq-btn.buy').click(); await settle(400);

await page.locator('.aq-shop-close').click();
await settle(400);
check('closing the shop dismisses it', (await page.locator('.aq-shop').count()) === 0);

await openShop('alchemist');
// Trade as the hero who is carrying the blade.
await page.locator('.aq-shop-hero').nth(1).click(); await settle(300);
await page.locator('.aq-shop-choice', { hasText: 'Sell' }).first().click();
await settle(400);
const blade = page.locator('.aq-shop-col:nth-child(2) .aq-shop-row', { hasText: 'Iron Sword' }).first();
check('the blade is in the trading hero\'s pack to offer', (await blade.count()) > 0);
if (await blade.count() > 0) {
  await blade.click();
  await settle(300);
  const sellDisabled = await page.locator('.aq-btn.sell').isDisabled().catch(() => false);
  check('the alchemist will not buy a weapon', sellDisabled, 'sell button disabled for a blade');
}

// ------------------------------------------------------------------
console.log('\n=== closing releases the hero ===');

await page.locator('.aq-shop-close').click();
await settle(500);
check('the second shop closed too', (await page.locator('.aq-shop').count()) === 0);
const after = await hud();
check('the freed hero can be prompted again',
  after?.promptVerb === 'Trade with' || after?.promptName?.length > 0,
  `"${after?.promptVerb} ${after?.promptName}"`);

// ------------------------------------------------------------------
console.log('\n=== console ===');
const noisy = errors.filter(e => !/favicon|status of 401/.test(e));
check('the game logged no errors', noisy.length === 0, noisy.slice(0, 2).join(' | '));

await browser.close();
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed\n`);
process.exit(failed === 0 ? 0 : 1);
