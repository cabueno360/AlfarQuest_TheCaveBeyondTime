// The quest CORE, played rather than inspected.
//
//     node tools/quest-probe.mjs
//
// Everything the campaign hangs on happens once, at the mouth of the Cave Beyond
// Time: the party is Mage and Thief until then, the main objective points you at
// the vale, and the first descent is a story beat — the Entrance-of-Cave cutscene
// plays, the Cleric joins at your side, and the objective moves on. This proves
// that whole beat fires, in the real engine, through the same EnterCave the mouth
// triggers, and that it is a once-ever moment (flag `cave_entered`), not once a
// visit. The places themselves are worldmap-probe's job; this is the story seam.

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

const settle = (ms = 500) => page.waitForTimeout(ms);
const hudRaw = () => page.evaluate(async () => (await import('/js/game.js')).hudSnapshot());
const hud = async () => {
  for (let i = 0; i < 10; i++) {
    const h = await hudRaw();
    if (h && h.stage !== undefined) return h;
    await settle(120);
  }
  return await hudRaw();
};
const enterCave = () => page.evaluate(async () => (await import('/js/game.js')).debugEnterCave());
const claim = flag => page.evaluate(async f => (await import('/js/game.js')).debugClaim(f), flag);
const region = id => page.evaluate(async r => (await import('/js/game.js')).debugLoadRegion(r), id);
const talkTo = id => page.evaluate(async i => (await import('/js/game.js')).debugTalkTo(i), id);
const warp = (x, y) => page.evaluate(async a => (await import('/js/game.js')).debugWarp(a.x, a.y), { x, y });
const snap = () => page.evaluate(() => { try { return JSON.parse(DotNet.invokeMethod('AlfarQuest.Client', 'Snapshot')); } catch { return null; } });
const cutsceneSrc = () => page.evaluate(() => {
  const v = document.querySelector('.aq-cutscene video');
  return v ? (v.getAttribute('src') || '') : null;
});
const keysOf = h => (h?.party ?? []).map(p => p.key);

const clearLevelUp = async () => {
  // Up to a dozen: the quest reward's 800 XP can level a fresh hero several times.
  for (let i = 0; i < 12 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 3000 }).catch(() => { });
    await settle(400);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(350); }
  }
  if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(300); }
};

const player = { email: `quest.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };

console.log('\n=== into the world ===');
await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Quest Tester');
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(4500);
await clearLevelUp();

// ------------------------------------------------------------------
console.log('\n=== before the cave: two heroes, one objective ===');

const start = await hud();
check('the world opens on the overworld', start?.stage === 1, `stage ${start?.stage}`);
const keys0 = keysOf(start);
check('the party starts as two — no Cleric yet', keys0.length === 2 && !keys0.includes('cleric'), keys0.join(', '));
check('  and they are the Mage and the Thief', keys0.includes('mage') && keys0.includes('thief'), keys0.join(', '));
const obj0 = start?.objective ?? '';
check('the main quest sets an objective', obj0.length > 0, `"${obj0}"`);
check('  and it is the pre-cave step (the vale / the Cleric\'s kin)',
  /vale|cleric|kin|cave beyond/i.test(obj0), `"${obj0}"`);
check('no cutscene has played before the descent', (await cutsceneSrc()) === null);

// ------------------------------------------------------------------
console.log('\n=== the first descent: the story beat fires ===');

await enterCave();
await settle(1400);

const src = await cutsceneSrc();
check('the first descent plays the Entrance-of-Cave cutscene',
  /entrance-of-cave/i.test(src ?? ''), src ?? '(no cutscene element)');

// Skip it so the world runs again before we read the post-state.
await page.click('.aq-cutscene-skip', { timeout: 3000 }).catch(() => { });
await settle(800);
await clearLevelUp();

const afterEnter = await hud();
const keys1 = keysOf(afterEnter);
check('the Cleric joins the party at the mouth', keys1.includes('cleric'), keys1.join(', '));
check('  and the party is three now', keys1.length === 3, `${keys1.length} heroes`);
check('the party is underground (stage 2)', afterEnter?.stage === 2, `stage ${afterEnter?.stage}`);

const obj1 = afterEnter?.objective ?? '';
check('the objective guides the delve underground (not blank)', obj1.length > 0, `"${obj1}"`);
check('  and it advanced from the surface step', obj1 !== obj0, `"${obj0}" → "${obj1}"`);
check('  and it now points at the Crystal Heart', /crystal heart/i.test(obj1), `"${obj1}"`);

await page.screenshot({ path: 'tools/shots/quest-cave-objective.png' }).catch(() => { });

check('skipping the cutscene resumed the game (no video left)', (await cutsceneSrc()) === null);

// ------------------------------------------------------------------
console.log('\n=== the Crystal Heart has a Guardian ===');

// One keeper per named depth now: depth 1 belongs to the Warden of the
// Cistern, the teaching boss — the true Guardian waits at depth 5.
const boss0 = afterEnter?.boss;
check('a boss holds the chamber — the Warden of the Cistern',
  /warden/i.test(boss0?.name || ''), boss0?.name || '(no boss)');
check('  and it is a real health pool, not a husk', (boss0?.mhp ?? 0) >= 600, `${boss0?.mhp ?? 0} hp`);

// Stand the steered hero beside the Guardian and let it act. Only the boss casts
// in the cave (ordinary husks have no trick), so any cast that fires is the Guardian
// — and its summon pulls fresh husks in. Counts are cumulative, so they survive the
// hero taking hits meanwhile.
const castsBefore = (await hud())?.castsFired ?? 0;
const summonBefore = (await hud())?.husksSummoned ?? 0;
// Gather the party on the Guardian (healed, so the fight lasts) until it has worked
// well into its kit — cast several times and summoned at least once.
let h2 = await hud();
for (let i = 0; i < 16 && ((h2?.castsFired ?? 0) < castsBefore + 3 || (h2?.husksSummoned ?? 0) <= summonBefore); i++) {
  await page.evaluate(async () => (await import('/js/game.js')).debugWarpToBoss());
  await settle(600);
  h2 = await hud();
}
console.log(`    (diag: casts=${h2?.castsFired} summoned=${h2?.husksSummoned} bossHp=${h2?.boss?.hp?.toFixed?.(0)}/${h2?.boss?.mhp} nearKind=${h2?.nearKind})`);
check('the Guardian works through its kit (several powers fire)', (h2?.castsFired ?? 0) >= castsBefore + 2,
  `${castsBefore} → ${h2?.castsFired ?? 0} casts`);
check('  and its Summon Husks pulls fresh husks into the fight', (h2?.husksSummoned ?? 0) > summonBefore,
  `${summonBefore} → ${h2?.husksSummoned ?? 0} summoned`);
await page.screenshot({ path: 'tools/shots/quest-boss.png' }).catch(() => { });
await clearLevelUp();

// ------------------------------------------------------------------
console.log('\n=== the Pact runs on into the dark ===');

// Drive the flags the delve would set — reaching the Crystal Heart, then the Cave
// Beyond Time itself — without a real five-level descent. (In play, `cave_heart` is
// set by World.Rewards when the party walks into the boss chamber.)
await claim('cave_heart'); await settle(600); await clearLevelUp();
const objHeart = (await hud())?.objective ?? '';
check('reaching the Crystal Heart advances the objective to the deepest reach',
  /deepest|cave beyond time/i.test(objHeart), `"${objHeart}"`);

await claim('cave_deep'); await settle(700);

// The 800-XP payout levels the leader and pops a level-up card, which PAUSES the
// sim — so the snapshot freezes on the old step. Clear the cards first; then the
// world runs again and recomputes the (now empty) objective. Also unobscures the toast.
await clearLevelUp(); await settle(500);
// The main line hands the HUD to the epilogue now: the Pact's payout is the
// Panacea, and "The Way Back Up" starts by itself the moment cave_deep lands —
// so the objective is Mirka's bedside, not an empty string.
const objDeep = (await hud())?.objective ?? '';
check('reaching the Cave Beyond Time hands the objective to the epilogue',
  /panacea|mirka/i.test(objDeep), `"${objDeep}"`);

// ------------------------------------------------------------------
console.log('\n=== the reward: shown on the banner, and really in the pack ===');

const toast = ((await page.textContent('.aq-quest-toast', { timeout: 2500 }).catch(() => null)) || '')
  .replace(/\s+/g, ' ').trim();
check('completing the main quest raises a "Quest Complete" toast', /delver/i.test(toast), `"${toast}"`);
check('  and the toast names the rare story items',
  /panacea/i.test(toast) && /crystal heart shard/i.test(toast), `"${toast}"`);
check('  and shows the XP and gold payout',
  /800\s*XP/i.test(toast) && /300\s*gold/i.test(toast), `"${toast}"`);
await page.screenshot({ path: 'tools/shots/quest-toast.png' }).catch(() => { });

// The rare item really lands in the leader's pack, not just on the banner.
await page.keyboard.press('c'); await settle(1000);
if ((await page.locator('.aq-cw.shown').count()) > 0) {
  await page.click('#aq-tab-inventory').catch(() => { }); await settle(700);
  const labels = await page.locator('.aq-pack-item')
    .evaluateAll(els => els.map(e => e.getAttribute('aria-label') || e.textContent || '')).catch(() => []);
  const pack = labels.join(' | ');
  check('the Phial of Panacea actually lands in the pack', /panacea/i.test(pack),
    pack.slice(0, 120) || '(empty pack)');
  await page.keyboard.press('c'); await settle(400);
} else {
  check('the character window opens to verify the pack', false, '(sheet did not open)');
}

// ------------------------------------------------------------------
console.log('\n=== it is a once-ever beat, not once-a-visit ===');

// Leave and come back: the second descent must NOT replay the cutscene, and must
// NOT recruit a second Cleric.
await enterCave();
await settle(1200);
check('a second descent does NOT replay the cutscene', (await cutsceneSrc()) === null);
const again = await hud();
const clerics = keysOf(again).filter(k => k === 'cleric').length;
check('  and does NOT recruit a second Cleric', clerics === 1, `${clerics} cleric(s), party ${keysOf(again).join('/')}`);

// ------------------------------------------------------------------
console.log('\n=== a quest offered: its terms and reward, before you take it ===');

// Back up to the wood, and put the offer to Mirka's father through the same bridge
// play's [E] uses. (cave_entered is set by now, so the side quest reads as done the
// instant it is taken — a probe artifact; in play it is offered before the descent.)
check('the Whispering Wood loads', await region('r2_whispering_wood'), 'r2_whispering_wood');
await settle(1000);
await talkTo('mirkafather'); await settle(900);
check("Mirka's father's dialogue opens", (await page.locator('.aq-talk').count()) > 0);

const canHelp = page.locator('.aq-talk-topic', { hasText: 'Can I help' });
check('the "Can I help?" topic is offered', (await canHelp.count()) > 0);
if ((await canHelp.count()) > 0) {
  await canHelp.first().click(); await settle(500);
  // Read the pitch to its end; the last "Hear them out…" turns into the offer panel.
  for (let i = 0; i < 5 && (await page.locator('.aq-offer-title').count()) === 0; i++) {
    if ((await page.locator('.aq-talk-go').count()) === 0) break;
    await page.click('.aq-talk-go').catch(() => { }); await settle(400);
  }
  check('reading the pitch raises the quest-offer panel', (await page.locator('.aq-offer-title').count()) > 0);
  const title = (await page.textContent('.aq-offer-title').catch(() => '')) || '';
  check('  and it names the quest', /word for the cleric/i.test(title), `"${title.trim()}"`);
  const items = (await page.locator('.aq-offer-item').allTextContents().catch(() => [])).join(' | ');
  check('  and shows the reward UP FRONT', /mirka.?s locket/i.test(items), items || '(none)');
  const tallies = (await page.locator('.aq-offer-tally').allTextContents().catch(() => [])).join(' ');
  check('  with its XP and gold', /220\s*XP/i.test(tallies) && /90\s*gold/i.test(tallies), tallies || '(none)');
  await page.screenshot({ path: 'tools/shots/quest-offer.png' }).catch(() => { });

  await page.click('.aq-offer-accept'); await settle(700);
  check('accepting closes the offer panel', (await page.locator('.aq-offer-title').count()) === 0);

  // Close the conversation, then the journal shows the quest was really taken.
  for (let i = 0; i < 3 && (await page.locator('.aq-talk').count()) > 0; i++) { await page.keyboard.press('Escape'); await settle(300); }
  await clearLevelUp();
  await page.keyboard.press('l'); await settle(800);
  const journal = (await page.textContent('body').catch(() => '')) || '';
  check('the accepted quest is now in the journal', /word for the cleric/i.test(journal), '');
}

// ------------------------------------------------------------------
await page.screenshot({ path: 'tools/shots/quest.png' }).catch(() => { });
console.log(`\n${failed === 0 ? 'ALL PASSED' : failed + ' FAILED'}  (${passed} ok, ${failed} failed)`);
if (errors.length) console.log('console errors:', [...new Set(errors)].slice(0, 6));
await browser.close();
process.exit(failed === 0 ? 0 : 1);
