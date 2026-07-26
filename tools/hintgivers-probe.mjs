// Brother Enoch & Goodwife Marrow, now hint-givers — played, not inspected.
//   node tools/hintgivers-probe.mjs
// Proves the two vale NPCs open a real question menu, carry the Cave hint that
// advances the main quest, and reveal a returned-delver line only after the cave.

import { chromium } from 'playwright-core';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5223';
let passed = 0, failed = 0;
const check = (name, ok, detail = '') => {
  console.log(`${ok ? '  ok  ' : ' FAIL '} ${name}${detail ? ` — ${detail}` : ''}`);
  ok ? passed++ : failed++;
};

const browser = await chromium.launch({ channel: 'chrome', args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
page.on('pageerror', e => console.log('  pageerror:', String(e).slice(0, 120)));

const settle = (ms = 500) => page.waitForTimeout(ms);
const hud = async () => page.evaluate(async () => (await import('/js/game.js')).hudSnapshot());
const region = id => page.evaluate(async r => (await import('/js/game.js')).debugLoadRegion(r), id);
const talkTo = id => page.evaluate(async i => (await import('/js/game.js')).debugTalkTo(i), id);
const claim = flag => page.evaluate(async f => (await import('/js/game.js')).debugClaim(f), flag);
const topics = () => page.locator('.aq-talk-topic').allTextContents();
const close = async () => { for (let i = 0; i < 3; i++) { await page.keyboard.press('Escape'); await settle(200); } };

const player = { email: `hint.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };
console.log('\n=== into the world ===');
await page.goto(`${CLIENT}/signup`); await settle(2200);
await page.fill('#signup-name', 'Hint Tester');
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]'); await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(4000);

check('the Whispering Wood loads', await region('r2_whispering_wood'), 'r2_whispering_wood');
await settle(700);
const objBefore = (await hud())?.objective ?? '';

console.log('\n=== Brother Enoch ===');
await talkTo('chapel_keeper'); await settle(800);
check('opens a question menu', (await page.locator('.aq-talk').count()) > 0);
const enochName = (await page.locator('.aq-talk-name').textContent().catch(() => '')) || '';
check('  it is Brother Enoch', /enoch/i.test(enochName), enochName);
let et = await topics();
check('  he offers several topics', et.length >= 4, `${et.length} topics`);
check('  including a Cave-Beyond-Time hint', et.some(t => /cave beyond time/i.test(t)));
check('  and the returned-delver line is hidden before the descent',
  !et.some(t => /been into the cave/i.test(t)));
// Ask the Cave topic — it sets quest_cave_learned, advancing the main line.
await page.locator('.aq-talk-topic', { hasText: /cave beyond time/i }).first().click(); await settle(500);
await close();
const objAfter = (await hud())?.objective ?? '';
check('asking his Cave hint advances the main objective', objAfter !== objBefore, `"${objBefore}" -> "${objAfter}"`);

console.log('\n=== Goodwife Marrow ===');
await talkTo('vale_widow'); await settle(800);
const marrowName = (await page.locator('.aq-talk-name').textContent().catch(() => '')) || '';
check('opens as Goodwife Marrow', /marrow/i.test(marrowName), marrowName);
const mt = await topics();
check('  she offers several topics', mt.length >= 4, `${mt.length} topics`);
check('  including the bell and the cave', mt.some(t => /bell/i.test(t)) && mt.some(t => /cave/i.test(t)));
check('  returned-delver line hidden before the descent', !mt.some(t => /came back from the cave/i.test(t)));
await close();

console.log('\n=== after the descent, they receive you differently ===');
await claim('cave_entered'); await settle(500);
await talkTo('chapel_keeper'); await settle(800);
et = await topics();
check('Brother Enoch now shows the returned-delver line', et.some(t => /been into the cave/i.test(t)));
await close();
await talkTo('vale_widow'); await settle(800);
const mt2 = await topics();
check('Goodwife Marrow now offers to ring the bell for you', mt2.some(t => /came back from the cave/i.test(t)));
await close();

console.log(`\n${failed === 0 ? 'ALL PASSED' : 'SOME FAILED'}  (${passed} ok, ${failed} failed)`);
await browser.close();
process.exit(failed === 0 ? 0 : 1);
