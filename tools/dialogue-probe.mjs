// The question-menu conversation, played rather than inspected.
//
//     node tools/dialogue-probe.mjs
//
// What this exists to catch is a menu that opens on nobody, a question that
// answers with someone else's words, or a conversation you cannot get out of.
// It walks to Mirka's Father, opens his menu, asks a question, turns the page,
// comes back to the list, and takes its leave — checking the hero is held while
// the window is open and released after.

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
const hud = () => page.evaluate(async () => (await import('/js/game.js')).hudSnapshot());
const warp = (x, y) => page.evaluate(async a => (await import('/js/game.js')).debugWarp(a.x, a.y), { x, y });

const clearLevelUp = async () => {
  for (let i = 0; i < 4 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 3000 }).catch(() => { });
    await settle(450);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(400); }
  }
  if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(350); }
};

const player = { email: `talk.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };

console.log('\n=== into the world ===');
await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Talk Tester');
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
// Wait for the world to BE there, not for a guess at how long that takes. Nine
// .tmx files are fetched at startup now; a fixed sleep that was long enough for
// four reads the world mid-build and finds the generator's villagers standing
// where the map's ought to be.
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

/// Put the party at a tile and CONFIRM they got there.
///
/// debugWarp does nothing while the world is paused, and the world is paused
/// behind a level-up card — which arrives unbidden the moment a discovery pays
/// out. Warping blind then meant standing next to whoever happened to be near
/// the spawn and testing them instead, which is how this probe came to report
/// that Mirka's father was a shopkeeper called Mother Sena.
const standAt = async (x, y) => {
  for (let i = 0; i < 4; i++) {
    await clearLevelUp();
    await warp(x, y);
    await settle(350);
    const h = await hud();
    if (h && Math.abs(h.heroTx - x) <= 1 && Math.abs(h.heroTy - y) <= 1) {
      await clearLevelUp();       // one may have popped on arrival
      await settle(250);
      return h;
    }
  }
  return await hud();
};

console.log('\n=== the question menu ===');
// Mirka's father is in the Whispering Wood now, at the door of the Cleric's
// house in the chapel vale — the region the ring puts that house in. Stand the
// world up there and walk to him.
await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r2_whispering_wood'));
await settle(1200);
await clearLevelUp();
await standAt(24, 35);
const before = await hud();
check('Mirka\'s Father can be spoken to', before?.promptVerb === 'Talk to', `"${before?.promptVerb} ${before?.promptName}"`);

await page.keyboard.press('e');
await settle(700);
const panel = page.locator('.aq-talk');
check('the conversation opens as a window, not a balloon', await panel.count() > 0);

const name = (await page.locator('.aq-talk-name').textContent().catch(() => ''))?.trim();
check('it is headed with who is speaking', name === "Mirka's Father", name);

const greeting = (await page.locator('.aq-talk-greeting').textContent().catch(() => ''))?.trim() ?? '';
check('he greets you before the questions', greeting.length > 20, `"${greeting.slice(0, 46)}…"`);

const topics = page.locator('.aq-talk-topic');
// Wait for the questions to actually be in the window before reading or clicking
// any of them. The window opens a frame before its list is filled, and on a
// busy machine that gap is wide enough to click into an empty menu.
await topics.first().waitFor({ state: 'visible', timeout: 10000 });
const nTopics = await topics.count();
check('every question from the concept is offered', nTopics === 11, `${nTopics} questions`);

const qs = (await topics.allTextContents()).map(s => s.replace(/[❧✓]/g, '').trim());
check('the questions are the ones the design asks for',
  qs.some(q => /Who are you/i.test(q)) && qs.some(q => /Wasting/i.test(q)) &&
  qs.some(q => /Cave Beyond Time/i.test(q)) && qs.some(q => /Cerno/i.test(q)) &&
  qs.some(q => /Panacea/i.test(q)) && qs.some(q => /armour/i.test(q)),
  qs.slice(0, 3).join(' / '));

// the hero is held while the window is open
const held = await hud();
check('the hero is held while talking — no prompt behind the window', !held?.promptVerb, `verb "${held?.promptVerb ?? ''}"`);

console.log('\n=== asking a question ===');
// Ask the one with a two-page answer we can turn.
const cerno = topics.filter({ hasText: 'Who is Cerno' });
await cerno.click();
await settle(400);
const asked = (await page.locator('.aq-talk-asked').textContent().catch(() => ''))?.trim() ?? '';
check('the question is echoed above the answer', /Cerno/i.test(asked), asked);

let answer = (await page.locator('.aq-talk-answer').textContent().catch(() => ''))?.trim() ?? '';
check('he answers in the book\'s own terms', /Kaladash/i.test(answer), `"${answer.slice(0, 54)}…"`);

const pages = (await page.locator('.aq-talk-pages').textContent().catch(() => ''))?.trim() ?? '';
check('a long answer turns a leaf at a time', pages.startsWith('1 /'), pages);

await page.click('.aq-talk-go');            // go on…
await settle(350);
const answer2 = (await page.locator('.aq-talk-answer').textContent().catch(() => ''))?.trim() ?? '';
check('turning the page gives the rest of it', answer2 !== answer && answer2.length > 10, `"${answer2.slice(0, 54)}…"`);

await page.screenshot({ path: 'tools/shots/dialogue-answer.png' });

await page.click('.aq-talk-go');            // ask something else
await settle(350);
check('the answer ends back at the list of questions', await page.locator('.aq-talk-topic').count() === 11);
check('what you already asked is marked', await page.locator('.aq-talk-topic.heard').count() === 1);
await page.screenshot({ path: 'tools/shots/dialogue-menu.png' });

console.log('\n=== taking your leave ===');
await page.click('.aq-talk-leave');
await settle(600);
check('the conversation closes', await page.locator('.aq-talk').count() === 0);
const after = await hud();
check('and the hero is released', after?.promptVerb === 'Talk to', `"${after?.promptVerb} ${after?.promptName}"`);

console.log('\n=== a villager with no menu still just talks ===');
// The forester at the ford has no question topics — she should still use the
// canvas balloon, not the window. Chosen over the collier, who stands among his
// kiln stores where [E] would open a barrel before it greeted him.
await standAt(33, 22);
await page.keyboard.press('e');
await settle(500);
const plain = await hud();
check('an NPC without questions opens no window', await page.locator('.aq-talk').count() === 0);
check('and speaks their line in the balloon as before', (plain?.talkLine ?? '').length > 10, `"${(plain?.talkLine ?? '').slice(0, 42)}…"`);

console.log('\n=== console ===');
check('the game logged no errors', errors.length === 0, errors.slice(0, 3).join(' | '));

console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed`);
await browser.close();
process.exit(failed === 0 ? 0 : 1);
