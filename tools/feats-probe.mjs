// Feats of the body: the ford boulder (Strength) and the climbers' scar
// (Dexterity) — each consults the fates, and each answers both outcomes.
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
const heroAt = () => page.evaluate(async () => {
  const es = (await import('/js/game.js')).lastEnts() ?? [];
  const h = es.find(e => e.t === 'hero' && e.active);
  return h ? { x: h.x, y: h.y } : null;
});
const clearLevelUp = async () => {
  for (let i = 0; i < 6 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 3000 }).catch(() => { });
    await settle(500);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(400); }
  }
};
const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });

await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Feat Tester');
await page.fill('#signup-email', `feat.${Date.now()}@example.com`);
await page.fill('#signup-password', 'a-long-enough-passphrase');
await page.fill('#signup-confirm', 'a-long-enough-passphrase');
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(5000);
await clearLevelUp();

console.log('=== the ford boulder (Strength) ===');
await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r2_whispering_wood'));
await settle(2500); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugWarp(41, 20));
await settle(700);
const b1 = await hud();
check('the boulder offers a shove', b1?.promptVerb === 'Shove', `"${b1?.promptVerb} ${b1?.promptName}"`);

const before = (await dice()).length;
await page.keyboard.press('e'); await settle(900); await clearLevelUp();
const shove = (await dice()).slice(before).find(d => d.kind === 'shove');
check('the shove consults the fates', !!shove, JSON.stringify(shove ?? null));

if (shove?.outcome === 'good') {
  await settle(600);
  const after = await hud();
  check('  moved: what it sat on is revealed', /hollow/i.test(after?.promptName ?? ''),
    `"${after?.promptVerb} ${after?.promptName}"`);
  // The flag keeps it moved: reload the region and the boulder is not placed.
  await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r2_whispering_wood'));
  await settle(2000);
  await page.evaluate(async () => (await import('/js/game.js')).debugWarp(41, 20));
  await settle(700);
  const back = await hud();
  check('  and it stays moved across a reload', back?.promptVerb !== 'Shove', `"${back?.promptVerb ?? ''}"`);
} else {
  const locked = (await dice()).length;
  await page.keyboard.press('e'); await settle(700);
  check('  refused: the shoulders burn, no second roll', (await dice()).length === locked, 'no new roll');
}

console.log('=== the climbers\' scar (Dexterity) ===');
await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r3_deepdelve'));
await settle(2500); await clearLevelUp();
await page.evaluate(async () => (await import('/js/game.js')).debugWarp(46, 25));
await settle(700);
const c1 = await hud();
check('the scar offers a climb', c1?.promptVerb === 'Climb', `"${c1?.promptVerb} ${c1?.promptName}"`);

const at0 = await heroAt();
const before2 = (await dice()).length;
await page.keyboard.press('e'); await settle(900); await clearLevelUp();
const climb = (await dice()).slice(before2).find(d => d.kind === 'climb');
check('the climb consults the fates', !!climb, JSON.stringify(climb ?? null));

await settle(2600);   // the party goes up with the die
const at1 = await heroAt();
if (climb?.outcome === 'good') {
  check('  up: the party stands at the brink', at1 && at0 && (at0.y - at1.y) > 200,
    `y ${Math.round(at0?.y ?? 0)} → ${Math.round(at1?.y ?? 0)}`);
  // The scar works both ways — the brink offers the way back down.
  const c2 = await hud();
  check('  and the brink offers the way down', c2?.promptVerb === 'Climb', `"${c2?.promptVerb} ${c2?.promptName}"`);
} else {
  check('  refused: nobody moved', at1 && at0 && Math.abs(at0.y - at1.y) < 40,
    `y ${Math.round(at0?.y ?? 0)} → ${Math.round(at1?.y ?? 0)}`);
  const locked2 = (await dice()).length;
  await page.keyboard.press('e'); await settle(700);
  check('  and the holds refuse a second try', (await dice()).length === locked2, 'no new roll');
}

console.log('=== console ===');
check('the game logged no errors', errors.length === 0, errors.slice(0, 3).join(' | '));

console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed`);
await browser.close();
process.exit(failed === 0 ? 0 : 1);
