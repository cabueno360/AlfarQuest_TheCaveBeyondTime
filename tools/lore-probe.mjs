// Persuasions and lore dice, the batch: the Cook and Harven stand up at the
// pit head, Orlo and Kazzat can be pressed, and three Ychellen texts ask the
// mind's die before they speak.
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
const dice = () => page.evaluate(async () => (await import('/js/game.js')).diceSeen());
const persuades = () => page.evaluate(async () => (await import('/js/dice.js')).rolledLog().filter(d => d.kind === 'persuade'));
const text = async sel => { try { return (await page.textContent(sel, { timeout: 2000 })) ?? ''; } catch { return ''; } };
const clearLevelUp = async () => {
  for (let i = 0; i < 6 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 3000 }).catch(() => { });
    await settle(500);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(400); }
  }
};
const closeTalk = async () => {
  for (let i = 0; i < 3 && (await page.locator('.aq-talk').count()) > 0; i++) {
    if (await page.locator('.aq-talk-close').count()) await page.locator('.aq-talk-close').click().catch(() => { });
    else await page.keyboard.press('Escape');
    await settle(350);
  }
};
const pressTopic = async (who, pattern, label) => {
  await page.evaluate(async id => (await import('/js/game.js')).debugTalkTo(id), who);
  await settle(900);
  const name = await text('.aq-talk-name');
  const topics = await page.locator('.aq-talk-topic').allTextContents().catch(() => []);
  const topic = topics.find(t => pattern.test(t));
  check(`${label} answers the door`, name.length > 0, `"${name}"`);
  check(`  and can be pressed`, !!topic, `${topics.length} topics`);
  if (topic) {
    const before = (await persuades()).length;
    // A long menu can push the pressing below the fold — scroll to it first.
    const btn = page.locator('.aq-talk-topic', { hasText: pattern }).first();
    await btn.scrollIntoViewIfNeeded({ timeout: 2000 }).catch(() => { });
    await btn.click({ timeout: 3000 }).catch(async () => {
      await btn.evaluate(el => el.click()).catch(() => { });
    });
    await settle(1200);
    const rolls = (await persuades()).slice(before);
    check(`  the pressing consults the fates`, rolls.length === 1, JSON.stringify(rolls[0] ?? null));
  }
  await closeTalk();
};
const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });

await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Lore Tester');
await page.fill('#signup-email', `lore.${Date.now()}@example.com`);
await page.fill('#signup-password', 'a-long-enough-passphrase');
await page.fill('#signup-confirm', 'a-long-enough-passphrase');
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(5000); await clearLevelUp();

console.log('=== the pit head finds its voices ===');
await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r3_deepdelve'));
await settle(2200); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugClaim('sq_tips_started'));
await settle(300);
await pressTopic('harven', /how many did the slide take/i, 'Harven, foreman of Deepdelve,');
await pressTopic('camp_cook', /not coming back/i, 'the Cook');
await pressTopic('pit_captain', /right column/i, 'Captain Orlo');

console.log('=== Kazzat, pressed at his own kettle ===');
await page.evaluate(async () => (await import('/js/game.js')).debugEnterCave());
await settle(1800);
await page.click('.aq-cutscene-skip', { timeout: 3000 }).catch(() => { });
await settle(1200); await clearLevelUp();
await pressTopic('kazzat', /say what you see/i, 'Kazzat');
await page.evaluate(async () => (await import('/js/game.js')).debugLeaveCave());
await settle(1500); await clearLevelUp();

console.log('=== the Ychellen texts ask the mind\'s die ===');
await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r1_ashwold'));
await settle(2200); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugWarp(60, 47));
await settle(700);
const s1 = await hud();
check('the boundary stone offers its letters', /boundary/i.test(s1?.promptName ?? ''), `"${s1?.promptVerb} ${s1?.promptName}"`);
const beforeStone = (await dice()).length;
await page.keyboard.press('e'); await settle(900);
const stoneRoll = (await dice()).slice(beforeStone).find(d => d.kind === 'lore');
check('  and the reading consults the fates', !!stoneRoll, JSON.stringify(stoneRoll ?? null));
await settle(2400);
for (let i = 0; i < 3 && (await page.locator('.aq-read').count()) > 0; i++) { await page.keyboard.press('Escape'); await settle(350); }

await page.evaluate(async () => (await import('/js/game.js')).debugLoadInterior('mage_school'));
await settle(2000); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugWarp(39, 19));
await settle(700);
const s2 = await hud();
check('the Academy\'s warded door offers itself', /warded door/i.test(s2?.promptName ?? ''), `"${s2?.promptVerb} ${s2?.promptName}"`);
const beforeDoor = (await dice()).length;
await page.keyboard.press('e'); await settle(900);
const doorRoll = (await dice()).slice(beforeDoor).find(d => d.kind === 'lore');
check('  and the seals ask a harder die', !!doorRoll, JSON.stringify(doorRoll ?? null));
await settle(2400);
for (let i = 0; i < 3 && (await page.locator('.aq-read').count()) > 0; i++) { await page.keyboard.press('Escape'); await settle(350); }

check('the game logged no errors', errors.length === 0, errors.slice(0, 3).join(' | '));
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed`);
await browser.close();
process.exit(failed === 0 ? 0 : 1);
