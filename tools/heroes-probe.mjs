// Individual hero progression, played and then round-tripped through the save.
//
//     node tools/heroes-probe.mjs
//
// The change this proves: inventory, gold and statistics belong to each hero, not
// to the account. So the checks below make the heroes DIVERGE — equip on one, loot
// with another — and then read the save back off the server to confirm each hero
// kept their own, and that the shared things (materials) stayed shared.

import { chromium } from 'playwright-core';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5223';
const API = process.env.API ?? 'http://localhost:5080';

let passed = 0, failed = 0;
const check = (name, ok, detail = '') => {
  console.log(`${ok ? '  ok  ' : ' FAIL '} ${name}${detail ? ` — ${detail}` : ''}`);
  ok ? passed++ : failed++;
};

const browser = await chromium.launch({ channel: 'chrome', args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 860 } });
const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
page.on('pageerror', e => errors.push(String(e)));

const settle = (ms = 700) => page.waitForTimeout(ms);
const hud = () => page.evaluate(async () => (await import('/js/game.js')).hudSnapshot());
const packBadge = async () => (await page.textContent('.aq-cw-badge.pack').catch(() => '')) ?? '';
const goldText = async () => (await page.textContent('.aq-money-amt, .aq-coin-amt, .aq-cw-coin-amt').catch(() => '')) ?? '';

const clearLevelUp = async () => {
  for (let i = 0; i < 4 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 4000 }).catch(() => { });
    await settle(700);
  }
};
const openSheet = async () => {
  await clearLevelUp();                        // a popup would swallow the 'c'
  for (let i = 0; i < 4 && (await page.locator('.aq-cw.shown').count()) === 0; i++) {
    await page.keyboard.press('c');
    await page.waitForSelector('.aq-cw.shown', { timeout: 3000 }).catch(() => { });
  }
  await settle(400);
};
const closeSheet = async () => {
  if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(700); }
};
const tab = async key => {
  await page.click(`#aq-tab-${key}`, { timeout: 4000 }).catch(() => { });
  await page.waitForSelector('.aq-cw-badge.pack, .aq-tab-grid', { timeout: 3000 }).catch(() => { });
  await settle(400);
};
const fightEncounter = async () => {
  await page.evaluate(async () => (await import('/js/game.js')).debugEncounter());
  await settle(400);
  await page.keyboard.down('j');
  for (let i = 0; i < 12; i++) {
    await settle(320);
    if (i % 2 === 0) { await page.keyboard.down('w'); await settle(110); await page.keyboard.up('w'); }
    if ((await hud())?.enemies === 0) break;
    await clearLevelUp();
  }
  await page.keyboard.up('j');
  await settle(300);
  await clearLevelUp();
};

const mySave = () => page.evaluate(async (api) => {
  const token = localStorage.getItem('alfarquest.session') || sessionStorage.getItem('alfarquest.session');
  const res = await fetch(`${api}/api/saves/mine`, { headers: { Authorization: `Bearer ${token}` } });
  return (await res.json())[0] ?? null;
}, API);

const player = { email: `heroes.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };

console.log('\n=== into the world ===');
await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Hero Tester');
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]');
await settle(2600);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null,
                           null, { timeout: 25000 });

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
await settle(2500);
await clearLevelUp();

console.log('\n=== each hero has their own pack ===');

// Steer the mage (slot 1), open their sheet, read their pack, then equip one
// item — which moves it out of the MAGE's pack and onto the mage. A shared pack
// would then look emptier for everyone; an individual one only for the mage.
await page.keyboard.press('F1');
await settle(300);
await openSheet();
await tab('inventory');
const mageBefore = await packBadge();
const equippable = await page.locator('.aq-pack-item').count();
check('the mage sets out with their own pack', /^[1-9]/.test(mageBefore.trim()), mageBefore.trim());
if (equippable > 0) { await page.locator('.aq-pack-item').first().click(); await settle(700); }
const mageAfter = await packBadge();
check('equipping takes it out of the mage\'s pack', mageAfter !== mageBefore,
  `${mageBefore.trim()} → ${mageAfter.trim()}`);

// Now the thief (slot 2 — the Cleric only joins at the Cave, so the party
// sets out as two): their pack must be untouched by what the mage did.
await closeSheet();
await page.keyboard.press('F2');
await settle(300);
await openSheet();
await tab('inventory');
const thiefPack = await packBadge();
check('the thief\'s pack is their own, not the mage\'s',
  thiefPack !== mageAfter, `mage ${mageAfter.trim()} vs thief ${thiefPack.trim()}`);
await closeSheet();

console.log('\n=== loot goes to whoever is steering ===');

// Back to the mage, and cut down several staged encounters. Coins fall to the
// hero holding the reins — the mage — and the kills are tallied against them.
// Several rounds rather than one, so a run of unlucky no-coin drops does not
// leave the mage's purse untouched.
await page.keyboard.press('F1');
await settle(300);
for (let round = 0; round < 4; round++) await fightEncounter();

console.log('\n=== the save keeps each hero\'s own ===');

// Leave, which writes the save, then read it straight back off the server — the
// real round-trip, not the in-memory copy.
await closeSheet();
if ((await page.locator('.aq-loot').count()) > 0) { await page.keyboard.press('Escape'); await settle(500); }
// Esc opens the save-and-leave dialog now; confirming writes the save.
for (let i = 0; i < 4 && (await page.locator('.aq-quitbox').count()) === 0; i++) {
  await page.keyboard.press('Escape');
  await settle(500);
}
await page.click('.aq-quitrow button:has-text("Save and leave")').catch(() => { });
await settle(3000);

const save = await mySave();
const party = save?.party ?? [];
// Two, not three: the Cleric joins at the Cave, and this probe never descends.
check('the save stored every hero', party.length >= 2, `${party.length} heroes`);
check('each hero carries their own inventory, purse and record',
  party.every(h => Array.isArray(h.inventory) && Array.isArray(h.purse) && h.statistics),
  party.map(h => h.heroKey).join(', '));

const mage = party.find(h => h.heroKey === 'mage');
const others = party.filter(h => h.heroKey !== 'mage');
const gold = h => (h?.purse ?? []).find(t => t.key === 'gold')?.count ?? 0;
const packCount = h => (h?.inventory ?? []).reduce((n, t) => n + t.count, 0);

check('the mage\'s pack differs from the others — inventories are individual',
  others.some(o => packCount(o) !== packCount(mage)),
  `mage ${packCount(mage)} vs ${others.map(packCount).join('/')}`);
check('the mage earned gold the others did not — purses are individual',
  gold(mage) !== gold(others[0]) || gold(mage) !== gold(others[1]),
  `mage ${gold(mage)}g vs ${others.map(gold).join('/')}g`);
check('the mage has a record of the fight',
  (mage?.statistics?.damageDealt ?? 0) > 0 || (mage?.statistics?.enemiesDefeated ?? 0) > 0,
  `dealt ${mage?.statistics?.damageDealt}, kills ${mage?.statistics?.enemiesDefeated}`);
check('statistics are not shared — the heroes\' records differ',
  new Set(party.map(h => JSON.stringify(h.statistics))).size >= 2);

// Materials stay shared: the party-level belongings hold materials, never a
// per-hero purse or pack.
const belongings = save?.belongings ?? [];
check('materials stayed shared at the party level',
  belongings.every(t => t.kind === 'material'),
  `kinds: ${[...new Set(belongings.map(t => t.kind))].join(',') || '(none)'}`);

console.log('\n=== it all comes back ===');

await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null,
                           null, { timeout: 25000 });
await settle(2500);
await clearLevelUp();
const reMage = (await hud())?.party?.find(p => p.key === 'mage');
check('the mage came back at the level they left',
  reMage && reMage.level === mage.level, `L${reMage?.level} vs saved L${mage?.level}`);

await openSheet();
await tab('inventory');
const reloadedPack = await packBadge();
check('the mage\'s pack was restored', /^\d/.test(reloadedPack.trim()), reloadedPack.trim());
await closeSheet();

console.log('\n=== console ===');
// The AudioContext warning is a headless artifact — the runner has no audio
// device for Web Audio to render to. Not a game fault, and filtered like the 401s.
const noisy = errors.filter(e => !/favicon|status of 401|AudioContext/.test(e));
check('the game logged no errors', noisy.length === 0, noisy.slice(0, 2).join(' | '));

await browser.close();
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed\n`);
process.exit(failed === 0 ? 0 : 1);
