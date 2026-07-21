// Does the progression system actually reward the player?
//
//     node tools/progression-probe.mjs
//
// The failure this exists to catch is the one the system already had: every
// piece present — a curve, a GainXp, an experience bar — and nothing calling
// any of it. So the checks below insist on the *loop*: the world pays, the sheet
// receives, the window opens, the points buy something the engine uses, and it
// all survives leaving and coming back.

import { chromium } from 'playwright-core';
import { mkdirSync } from 'node:fs';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5223';
const SHOTS = 'tools/shots';
mkdirSync(SHOTS, { recursive: true });

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

const settle = (ms = 800) => page.waitForTimeout(ms);
const shot = n => page.screenshot({ path: `${SHOTS}/${n}.png` });
const text = async sel => { try { return (await page.textContent(sel, { timeout: 2500 })) ?? ''; } catch { return ''; } };

// The HUD the canvas drew last frame. Pixels cannot be asserted on; this is the
// same object the renderer was handed.
const hud = () => page.evaluate(async () => {
  const m = await import('/js/game.js');
  return m.hudSnapshot();
});

/// Dismisses a level-up window if one is open.
///
/// The party earns XP throughout these checks, and a level-up pauses the world
/// and takes the keyboard. Without this, a cast pressed while the popup was up
/// went to the dialog and the mana checks failed intermittently for a reason
/// that had nothing to do with mana.
const clearLevelUp = async () => {
  for (let i = 0; i < 4 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 4000 }).catch(() => { });
    await page.waitForTimeout(900);
    if ((await page.locator('.aq-cw.shown').count()) > 0) {
      await page.keyboard.press('c');           // the hand-off opened the sheet
      await page.waitForTimeout(700);
    }
  }
};

/// Holds a key down for a while, letting the game tick. Movement is the only way
/// to reach most rewards, so the probe has to actually play.
const walk = async (key, ms) => {
  await page.keyboard.down(key);
  await page.waitForTimeout(ms);
  await page.keyboard.up(key);
  await page.waitForTimeout(120);
};

const player = {
  name: 'Wren Deepstep',
  email: `wren.${Date.now()}@example.com`,
  password: 'the-long-way-down-again',
};

console.log('\n=== into the world ===');

await page.goto(`${CLIENT}/signup`);
await settle(2500);
await page.fill('#signup-name', player.name);
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]');
await settle(2500);

await page.goto(`${CLIENT}/play`);
await settle(7000);
check('the game is running', (await page.locator('#gameCanvas').count()) === 1);

console.log('\n=== the world pays for arriving ===');

const atSpawn = await hud();
check('the HUD reports a level', atSpawn?.heroLevel >= 1, `level ${atSpawn?.heroLevel}`);
check('the HUD reports what the next level costs',
  atSpawn?.xpNext === 100, `xpNext ${atSpawn?.xpNext} (spec: 100 for 1→2)`);
check('standing in the spawn camp paid experience',
  atSpawn?.xp > 0, `${atSpawn?.xp} XP`);
check('the bar fraction agrees with the numbers',
  Math.abs((atSpawn?.xpFrac ?? 0) - atSpawn.xp / atSpawn.xpNext) < 0.001,
  `frac ${atSpawn?.xpFrac?.toFixed(3)}`);
await shot('p1-spawn');

console.log('\n=== the sheet sees the same experience ===');

// Re-read rather than reusing the reading from the spawn: the resource checks
// above take half a minute of real play, and the party earns XP throughout.
// Comparing against the older number made a correct sheet look wrong.
const nowHud = await hud();

await page.keyboard.press('c');
await settle(1200);
const sheet = await text('.aq-cw-xp');
check('the character window opened', sheet.length > 0);

// The sheet used to pin these full because nothing spent them. Now it must show
// what the simulation actually holds.
// The label and the numbers render with no space between them.
const vitals = (await text('.aq-cw-vitals')).replace(/\s+/g, ' ');
const manaOnSheet = vitals.match(/Mana ?(\d+) ?\/ ?(\d+)/);
check('the sheet shows mana', manaOnSheet !== null, vitals.slice(0, 90));
check('the sheet mirrors the engine\'s mana',
  manaOnSheet !== null && Math.abs(Number(manaOnSheet[1]) - (nowHud.party?.[0]?.mana ?? -1)) <= 2,
  manaOnSheet ? `sheet ${manaOnSheet[1]}, engine ${nowHud.party?.[0]?.mana?.toFixed(0)}` : '');
check('the sheet shows the engine\'s XP, not a separate copy',
  sheet.includes(`${nowHud.xp} / ${nowHud.xpNext}`), sheet.replace(/\s+/g, ' ').trim().slice(0, 80));
await shot('p2-sheet-xp');

console.log('\n=== the new attributes exist and derive something ===');

const attrs = await text('.aq-attrs');
for (const a of ['Strength', 'Dexterity', 'Agility', 'Vitality', 'Intelligence', 'Wisdom', 'Defense', 'Luck']) {
  check(`${a} is on the sheet`, attrs.includes(a));
}

const statsBefore = await page.locator('.aq-cw-panel:has-text("Combat") .aq-stat').allTextContents()
  .catch(() => []);
const derived = (await text('.aq-cw-right')).replace(/\s+/g, ' ');
for (const s of ['Damage Reduction', 'Magic Resistance', 'Block Chance', 'Find Rarity']) {
  check(`"${s}" is derived and shown`, derived.includes(s));
}
await shot('p3-attributes');

await page.keyboard.press('c');
await settle(900);

console.log('\n=== walking earns more ===');

// North and east, toward the wood and the ford. Long enough to cross at least
// one more zone boundary.
const before = (await hud()).xp;
for (let i = 0; i < 6; i++) { await walk('w', 1400); await walk('d', 900); }
const after = await hud();
check('exploring earned more experience', after.xp !== before || after.heroLevel > 1,
  `${before} → ${after.xp} XP at level ${after.heroLevel}`);
await shot('p4-explored');

console.log('\n=== levelling up ===');

// Keep going until a level lands or the budget runs out. The level-up pauses
// the game, so once the window is up walking stops mattering.
// Tracked rather than hard-coded: the checks below spend points, and an
// assertion written as a literal broke the moment a second attribute was tested.
let pointsBeforeSpending = 0;
let pointsSpent = 0;

let levelled = (await page.locator('.aq-levelup').count()) > 0;
for (let i = 0; i < 14 && !levelled; i++) {
  await walk(i % 2 ? 'w' : 'd', 1500);
  levelled = (await page.locator('.aq-levelup').count()) > 0;
}

check('a level-up window appeared', levelled, levelled ? '' : `still level ${(await hud())?.heroLevel}`);

if (levelled) {
  const popup = (await text('.aq-levelup-frame')).replace(/\s+/g, ' ');
  check('it announces the level reached', /Level \d+ reached/i.test(popup), popup.slice(0, 90));
  check('it awards the configured attribute points', /\+5 attribute points/i.test(popup), popup.slice(0, 140));
  check('it shows the hero', popup.includes('The '), popup.slice(0, 60));
  check('the world is frozen behind it',
    await page.evaluate(async () => (await import('/js/game.js')).hudSnapshot() !== null));
  await shot('p5-levelup');

  const hudAtLevel = await hud();
  check('the HUD level went up', hudAtLevel.heroLevel >= 2, `level ${hudAtLevel.heroLevel}`);
  check('the next level costs more than the first',
    hudAtLevel.xpNext === 180, `xpNext ${hudAtLevel.xpNext} (spec: 180 for 2→3)`);

  console.log('\n=== continue hands off to the attributes ===');

  // Drain the queue: a single award can cross more than one threshold, and the
  // hand-off to the sheet only happens once the last one is dismissed.
  for (let i = 0; i < 6 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 4000 }).catch(() => { });
    await settle(1000);
  }
  check('the level-up window closed', (await page.locator('.aq-levelup').count()) === 0);
  check('the character window opened by itself', (await page.locator('.aq-cw.shown').count()) === 1);

  const points = await text('.aq-cw-points');
  check('there are points waiting to be spent', /\d+ attribute points/.test(points), points.trim());
  pointsBeforeSpending = Number((points.match(/(\d+) attribute/) ?? [0, 0])[1]);
  await shot('p6-handoff');

  console.log('\n=== spending a point changes a real number ===');

  const readStat = async label => {
    const rows = await page.locator('.aq-stat').allTextContents();
    return rows.find(r => r.includes(label)) ?? '';
  };

  const defenseBefore = await readStat('Damage Reduction');
  // Selected by its row, not by index. An index selector here silently clicked
  // Agility, because the small + button is shared with skills and crafting and
  // there are twenty of them on the page.
  await page.locator('.aq-attr:has-text("Defense") .aq-mini-btn').first().click();
  pointsSpent++;
  await settle(900);
  const defenseAfter = await readStat('Damage Reduction');

  check('spending a point on Defense moved damage reduction',
    defenseBefore !== defenseAfter && defenseAfter !== '',
    `"${defenseBefore.trim()}" → "${defenseAfter.trim()}"`);
  // Wisdom drives magic resistance and mana regeneration. Both are read by the
  // engine, so this is the same test as the Defense one: does the attribute
  // reach a number the simulation uses, or only a label?
  const wisBefore = await readStat('Magic Resistance');
  const regenBefore = await readStat('Mana Regen');
  await page.locator('.aq-attr:has-text("Wisdom") .aq-mini-btn').first().click();
  pointsSpent++;
  await settle(900);
  check('spending a point on Wisdom moved magic resistance',
    (await readStat('Magic Resistance')) !== wisBefore,
    `"${wisBefore.trim()}" → "${(await readStat('Magic Resistance')).trim()}"`);
  check('and moved mana regeneration',
    (await readStat('Mana Regen')) !== regenBefore,
    `"${regenBefore.trim()}" → "${(await readStat('Mana Regen')).trim()}"`);

  await shot('p7-spent');
}

console.log('\n=== mana and stamina are real ===');

// The sheet is open from the level-up hand-off; these checks drive the game.
if ((await page.locator('.aq-cw.shown').count()) > 0) {
  await page.keyboard.press('c');
  await settle(900);
}

const lead = async () => (await hud())?.party?.[0];

await clearLevelUp();
const beforeCast = await lead();
check('the lead hero has a mana pool', beforeCast?.mmana > 0, `${beforeCast?.mana}/${beforeCast?.mmana}`);
check('it starts full', Math.abs(beforeCast.mana - beforeCast.mmana) < 1, `${beforeCast.mana}/${beforeCast.mmana}`);
check('the ultimate reads as ready', beforeCast?.abilityReady === true);

await page.keyboard.press('k');
await settle(400);
const afterCast = await lead();
check('casting spent mana', afterCast.mana < beforeCast.mana - 20,
  `${beforeCast.mana.toFixed(0)} → ${afterCast.mana.toFixed(0)}`);
check('the ultimate is no longer ready', afterCast.abilityReady === false);

// Cast until the pool runs dry. The gap has to clear the 6s cooldown, or the
// presses land on cooldown and the pool quietly refills between them — which is
// what an earlier version of this check measured, and why it reported that mana
// was never spent.
let drained = await lead();
for (let i = 0; i < 6 && drained.mana >= 40; i++) {
  await clearLevelUp();
  await page.keyboard.press('k');
  await settle(6300);
  drained = await lead();
}
await clearLevelUp();
check('sustained casting empties the pool', drained.mana < 40,
  `${drained.mana.toFixed(0)}/${drained.mmana.toFixed(0)} after casting`);
check('an unaffordable ultimate is reported, not silently ignored',
  drained.abilityAffordable === false,
  `mana ${drained.mana.toFixed(0)}, affordable ${drained.abilityAffordable}`);

await settle(4000);
const regenerated = await lead();
check('mana refills on its own', regenerated.mana > drained.mana,
  `${drained.mana.toFixed(0)} → ${regenerated.mana.toFixed(0)}`);
check('but not instantly — it is still short of full',
  regenerated.mana < regenerated.mmana,
  `${regenerated.mana.toFixed(0)}/${regenerated.mmana.toFixed(0)}`);

await clearLevelUp();

// The sheet used to pin these full because nothing spent them. With the pool
// genuinely low, it has to say so.
await page.keyboard.press('c');
await settle(1200);
const drainedSheet = (await text('.aq-cw-vitals')).replace(/\s+/g, ' ');
const drainedMana = drainedSheet.match(/Mana ?(\d+) ?\/ ?(\d+)/);
check('the sheet shows a drained pool, not a full one',
  drainedMana !== null && Number(drainedMana[1]) < Number(drainedMana[2]),
  drainedMana ? `${drainedMana[1]}/${drainedMana[2]}` : drainedSheet.slice(0, 80));
await shot('p9-pools');
await page.keyboard.press('c');
await settle(900);

const beforeDash = (await lead()).stam;
for (let i = 0; i < 4; i++) {
  await page.keyboard.down('w');
  await page.keyboard.press('Shift');
  await settle(320);
  await page.keyboard.up('w');
}
const afterDash = (await lead()).stam;
check('dashing spent stamina', afterDash < beforeDash,
  `${beforeDash.toFixed(0)} → ${afterDash.toFixed(0)}`);

console.log('\n=== belongings ===');

// The sheet is already open from the hand-off. Equipping moves an item out of
// the pack and onto the hero, which changes both halves of what has to survive.
const packCount = async () => (await text('.aq-cw-badge.pack')).trim();
const gold = async () => (await page.locator('.aq-cw-coin-amt').first().textContent() ?? '').trim();
const wornIcons = async () => (await page.locator('.aq-slots.worn .aq-slot.filled .aq-slot-icon').allTextContents()).join('');

if ((await page.locator('.aq-cw.shown').count()) === 0) {
  await page.keyboard.press('c');
  await settle(1200);
}

const packBefore = await packCount();
const goldBefore = await gold();
check('the pack has something in it', /^[1-9]/.test(packBefore), packBefore);
check('the purse has coin', goldBefore !== '0', goldBefore);

// Scoped to the pack grid: the equipment grid renders the same .aq-slot
// class, and an unscoped selector would have unequipped instead.
await page.locator('.aq-slots.pack .aq-slot.filled').first().click();
await settle(900);
const packAfterEquip = await packCount();
const wornAfterEquip = await wornIcons();
check('equipping took the item out of the pack', packAfterEquip !== packBefore,
  `${packBefore} → ${packAfterEquip}`);

console.log('\n=== progression survives leaving ===');

// Captured before quitting: how many one-shot rewards are still untaken. This
// is the number that must not go back up.
const rewardsBeforeQuit = (await hud())?.rewardsLeft;

await page.keyboard.press('Escape');
await settle(600);
await page.click('button:has-text("Abandon Delve")');
await settle(3000);
check('abandoning returns to hero select', page.url().includes('/heroes'), page.url());

// Read what was actually written, rather than what the HUD said a moment before
// quitting — the world keeps running between the two, and comparing against a
// stale reading made a passing save look broken.
const saved = await page.evaluate(async () => {
  const token = localStorage.getItem('alfarquest.session') || sessionStorage.getItem('alfarquest.session');
  const res = await fetch('http://localhost:5080/api/saves/mine', { headers: { Authorization: `Bearer ${token}` } });
  const saves = await res.json();
  return saves[0] ?? null;
});

check('a save was written', saved !== null);
const savedMage = saved?.party?.find(h => h.heroKey === 'mage');
check('it recorded the level', savedMage?.level >= 2, `level ${savedMage?.level}`);
check('it recorded the spent point', savedMage?.spent?.defense === 1, `DEF spent ${savedMage?.spent?.defense}`);
const expectedPoints = pointsBeforeSpending - pointsSpent;
check('it recorded unspent points', savedMage?.attributePoints === expectedPoints,
  `${savedMage?.attributePoints} left, expected ${expectedPoints}`);
check('it recorded the points that were spent',
  (savedMage?.spent?.defense ?? 0) + (savedMage?.spent?.wisdom ?? 0) === pointsSpent,
  `DEF ${savedMage?.spent?.defense} WIS ${savedMage?.spent?.wisdom}`);
check('it recorded the claimed rewards',
  (saved?.claimedRewards ?? []).includes('Ashwold Camp'), JSON.stringify(saved?.claimedRewards));

const savedGold = saved?.belongings?.find(t => t.kind === 'currency' && t.key === 'gold')?.count;
const savedPack = (saved?.belongings ?? []).filter(t => t.kind === 'pack')
  .reduce((n, t) => n + t.count, 0);
check('it recorded the purse', savedGold > 0, `${savedGold} gold`);
check('it recorded the pack', savedPack === 5, `${savedPack} items`);
check('it recorded the worn gear', (savedMage?.equipped ?? []).length === 3,
  JSON.stringify(savedMage?.equipped));

await page.goto(`${CLIENT}/play`);
// Wait for the first frame, then only long enough for a discovery to have fired
// if it were going to. Short on purpose: the nearest creature is ~400px away and
// cannot reach the party in this window, so any XP gained here would have to be
// a re-awarded discovery — which is the bug being tested for.
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 20000 });
await settle(3000);
const restored = await hud();

check('the level came back', restored?.heroLevel >= savedMage?.level,
  `${savedMage?.level} → ${restored?.heroLevel}`);

// Progression is asserted as "never went backwards" rather than "is identical".
// The party auto-fights, so a few seconds of being loaded legitimately earns XP
// — an equality check here failed for that reason and looked like a save bug.
check('progression did not go backwards',
  restored?.heroLevel > savedMage?.level || restored?.xp >= savedMage?.xp,
  `level ${savedMage?.level} xp ${savedMage?.xp} → level ${restored?.heroLevel} xp ${restored?.xp}`);

// The precise test for the refill exploit: the claims survived, and the number
// of untaken rewards did not go back up.
check('the claims survived the reload', restored?.claimsHeld >= 1, `${restored?.claimsHeld} claims`);
check('the world did NOT refill its one-shot rewards',
  restored?.rewardsLeft === rewardsBeforeQuit,
  `${rewardsBeforeQuit} untaken before, ${restored?.rewardsLeft} after`);

await page.keyboard.press('c');
await settle(1200);
const pointsAfterReload = await text('.aq-cw-points');
check('unspent points came back too',
  new RegExp(`${expectedPoints} attribute points`).test(pointsAfterReload), pointsAfterReload.trim());
const attrsAfter = (await text('.aq-attrs')).replace(/\s+/g, ' ');
check('the point spent on Defense came back', /DEF Defense 4 ?\+1/.test(attrsAfter),
  (attrsAfter.match(/DEF[^A-Z]*/) ?? [''])[0].trim());

check('the pack came back the size it was left',
  (await packCount()) === packAfterEquip, `${packAfterEquip} → ${await packCount()}`);
// Not equality: creatures drop coin, and three seconds of being loaded can
// legitimately add some. What must not happen is the purse resetting, or the
// 120-gold starting grant landing a second time.
const goldNow = Number((await gold()).replace(/,/g, ''));
check('the purse came back', goldNow >= savedGold, `saved ${savedGold} → ${goldNow}`);
check('the gear equipped before leaving is still worn',
  (await wornIcons()) === wornAfterEquip, `"${wornAfterEquip}" → "${await wornIcons()}"`);
check('the starting purse was not granted a second time',
  goldNow < savedGold + 120, `saved ${savedGold} → ${goldNow} (a re-grant would add 120)`);
await shot('p8-restored');

console.log('\n=== saving twice does not accumulate ===');

// The upsert replaces a save's children by deleting and re-adding them. If the
// delete missed anything, a second exit would double the rows rather than
// replace them — and nothing on screen would show it.
await page.keyboard.press('Escape');
await settle(500);
await page.click('button:has-text("Abandon Delve")');
await settle(3000);

const secondSave = await page.evaluate(async () => {
  const token = localStorage.getItem('alfarquest.session') || sessionStorage.getItem('alfarquest.session');
  const res = await fetch('http://localhost:5080/api/saves/mine', { headers: { Authorization: `Bearer ${token}` } });
  return (await res.json())[0] ?? null;
});

check('there is still exactly one save', secondSave?.id === saved?.id, `${saved?.id} → ${secondSave?.id}`);
check('belongings were replaced, not appended',
  secondSave?.belongings?.length === saved?.belongings?.length,
  `${saved?.belongings?.length} → ${secondSave?.belongings?.length}`);
check('worn gear was replaced, not appended',
  secondSave?.party?.find(h => h.heroKey === 'mage')?.equipped?.length === 3,
  JSON.stringify(secondSave?.party?.find(h => h.heroKey === 'mage')?.equipped));
check('claims were replaced, not appended',
  secondSave?.claimedRewards?.length === saved?.claimedRewards?.length,
  `${saved?.claimedRewards?.length} → ${secondSave?.claimedRewards?.length}`);

console.log('\n=== console ===');
const noisy = errors.filter(e => !/favicon|status of 401/.test(e));
check('the app logged no errors', noisy.length === 0, noisy.slice(0, 2).join(' | '));

await browser.close();
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed\n`);
process.exit(failed === 0 ? 0 : 1);
