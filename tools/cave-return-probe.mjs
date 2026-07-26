// The way BACK from the cave, played rather than inspected.
//
//     node tools/cave-return-probe.mjs
//
// The delve used to be one-way: the only way out was Abandon Delve, which quit to
// the roster. Now a "Leave the cave" step sits at the mouth on every level, and
// stepping it runs LeaveCave — stands the region back up and drops the party in
// front of the mouth. This proves that whole loop in the real engine: descend,
// the exit prompt is there at the mouth, pressing [E] on it returns you to the
// overworld you came from, and doing it the deterministic way (DebugLeaveCave)
// lands the same. Also that the R3 tile cleanup didn't break the cave build.

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
const snap = () => page.evaluate(() => { try { return JSON.parse(DotNet.invokeMethod('AlfarQuest.Client', 'Snapshot')); } catch { return null; } });
const enterCave = () => page.evaluate(async () => (await import('/js/game.js')).debugEnterCave());
const leaveCave = () => page.evaluate(async () => (await import('/js/game.js')).debugLeaveCave());
const warp = (x, y) => page.evaluate(async a => (await import('/js/game.js')).debugWarp(a.x, a.y), { x, y });
const TILE = 32;

const clearLevelUp = async () => {
  for (let i = 0; i < 12 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 3000 }).catch(() => { });
    await settle(400);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(350); }
  }
  if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(300); }
};

const player = { email: `caveret.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };

console.log('\n=== into the world ===');
await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Cave Return Tester');
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(4500);
await clearLevelUp();

const start = await hud();
check('the world opens on the overworld', start?.stage === 1, `stage ${start?.stage}`);
const region0 = start?.regionName ?? start?.region ?? '';

// ------------------------------------------------------------------
console.log('\n=== descend: the cave still builds (R3 cleanup intact) ===');

await enterCave();
await settle(1200);
// The first descent plays the Entrance-of-Cave cutscene — skip past it.
await page.click('.aq-cutscene-skip', { timeout: 3000 }).catch(() => { });
await settle(700);
await clearLevelUp();

const inCave = await hud();
check('the party is underground (stage 2)', inCave?.stage === 2, `stage ${inCave?.stage}`);
check('the chamber built with its boss', /guardian/i.test(inCave?.boss?.name || ''), inCave?.boss?.name || '(no boss)');

// ------------------------------------------------------------------
console.log('\n=== the exit prompt is there at the mouth ===');

// The renderer floats a name-plate over the labelled portal, so the way out is a
// mark you can see in the dark rather than one you stumble onto.
const ents = await page.evaluate(async () => (await import('/js/game.js')).lastEnts());
const label = (ents ?? []).find(e => e.t === 'label' && /leave the cave/i.test(e.name || ''));
check('a "Leave the cave" mark is shown in the cave', !!label, label ? `at ${Math.round(label.x)},${Math.round(label.y)}` : '(none)');

// The party arrives two tiles below the exit (Spawn = entrance + 2). Step onto it.
const before = await hud();
await warp(before.heroTx, before.heroTy - 2);
await settle(500);
const atMouth = await hud();
check('standing on it, the prompt offers the way out', atMouth?.promptName === 'Leave the cave', `"${atMouth?.promptVerb} ${atMouth?.promptName}"`);
check('  and the verb is "Step"', atMouth?.promptVerb === 'Step', `"${atMouth?.promptVerb}"`);
await page.screenshot({ path: 'tools/shots/cave-return-prompt.png' }).catch(() => { });

// ------------------------------------------------------------------
console.log('\n=== pressing [E] returns to the overworld ===');
await page.keyboard.press('e');
await settle(1200);
await clearLevelUp();
const out = await hud();
check('pressing [E] leaves the delve — back on the overworld', out?.stage === 1, `stage ${out?.stage}`);
check('  and back in the region we came from', (out?.regionName ?? out?.region ?? '') === region0, `"${region0}" → "${out?.regionName ?? out?.region ?? ''}"`);
await page.screenshot({ path: 'tools/shots/cave-return-overworld.png' }).catch(() => { });

// ------------------------------------------------------------------
console.log('\n=== the deterministic path lands the same ===');
await enterCave();
await settle(900);
await page.click('.aq-cutscene-skip', { timeout: 2000 }).catch(() => { });
await settle(500);
await clearLevelUp();
check('back underground for the second test', (await hud())?.stage === 2);
await leaveCave();
await settle(1000);
await clearLevelUp();
const out2 = await hud();
check('DebugLeaveCave returns to the overworld too', out2?.stage === 1, `stage ${out2?.stage}`);
check('  the delve state is cleared (no boss carried out)', !out2?.boss || !/guardian/i.test(out2?.boss?.name || ''), out2?.boss?.name || '(none)');

// ------------------------------------------------------------------
const fatal = errors.filter(e => !/favicon|404|net::ERR|Failed to load resource/i.test(e));
check('no console errors during the loop', fatal.length === 0, fatal.slice(0, 2).join(' | '));

console.log(`\n${failed === 0 ? 'ALL PASS' : 'SOME FAILED'} — ${passed} ok, ${failed} failed\n`);
await browser.close();
process.exit(failed === 0 ? 0 : 1);
