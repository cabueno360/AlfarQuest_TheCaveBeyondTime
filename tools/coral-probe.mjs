// The other shore: the Diver refuses without the key, crosses with it, rolls
// the crossing die, and lands on the Great Coral Tree — renamed rooms, its own
// bestiary, its keeper, and the Tree itself to behold.
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
const clearLevelUp = async () => {
  for (let i = 0; i < 12 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 3000 }).catch(() => { });
    await settle(500);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(400); }
  }
};
const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });

await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Coral Tester');
await page.fill('#signup-email', `coral.${Date.now()}@example.com`);
await page.fill('#signup-password', 'a-long-enough-passphrase');
await page.fill('#signup-confirm', 'a-long-enough-passphrase');
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(5000);
await clearLevelUp();

console.log('=== the sealed door refuses without the key ===');
await page.evaluate(async () => (await import('/js/game.js')).debugEnterCave());
await settle(1800);
await page.click('.aq-cutscene-skip', { timeout: 3000 }).catch(() => { });
await settle(1200); await clearLevelUp();
const h1 = await hud();
check('the delve begins in the Cistern', h1?.region === 'The Crystal Cistern', `"${h1?.region}"`);
await page.evaluate(async () => (await import('/js/game.js')).debugDive());
await settle(900);
const still1 = await hud();
check('without the key the Diver stays shut', still1?.level === 1, `level ${still1?.level}`);

console.log('=== the crossing ===');
await page.evaluate(async () => (await import('/js/game.js')).debugClaim('kazzat_key'));
// The deeper gates ask for levels; feed the sheets so the crossing is offered.
await page.evaluate(async () => (await import('/js/game.js')).debugGrantXp(60000));
await settle(800); await clearLevelUp();
const before = (await dice()).length;
await page.evaluate(async () => (await import('/js/game.js')).debugDive());
await settle(2000);
const mid = await hud();
check('the boarding scene plays before the crossing', mid?.level === 1, `still level ${mid?.level} while the scene runs`);
await settle(8000); await clearLevelUp();

const h2 = await hud();
check('the Diver lands on the Great Coral Tree', h2?.region === 'The Great Coral Tree' && h2?.level === 2,
  `"${h2?.region}" at level ${h2?.level}`);
const crossing = (await dice()).slice(before).find(d => d.kind === 'crossing');
check('the crossing consults the fates — Kazzat\'s horrors', !!crossing, JSON.stringify(crossing ?? null));
check('  a d20, sea-green, judged good or bad', crossing?.sides === 20 && ['good', 'bad'].includes(crossing?.outcome),
  `d${crossing?.sides} → ${crossing?.outcome}`);

console.log('=== the other shore ===');
const ents = await page.evaluate(async () => (await import('/js/game.js')).lastEnts());
const labels = (ents ?? []).filter(e => e.t === 'label').map(l => l.text ?? l.name ?? '?');
check('the shore keeps its alcove and its way up', labels.some(l => /alcove/i.test(l)) && labels.some(l => /leave/i.test(l)),
  labels.join(' · '));

const kinds = (h2?.enemyKinds ?? '').split(',').filter(Boolean);
const coralKinds = ['drowned', 'reef_skitterer', 'brine_swarm'];
check('the sea\'s own prowl the shore, not the husks', kinds.length > 0 && kinds.every(k => coralKinds.includes(k) || k === 'coral_warden'),
  kinds.join(','));
check('  and the mix is a mix', kinds.filter(k => coralKinds.includes(k)).length >= 2, `${kinds.length} kinds`);
check('the Warden of the Coral Tree holds the Heartwood',
  kinds.includes('coral_warden'), h2?.boss?.name ?? '(boss bar arms on approach)');

console.log('=== beholding the Tree ===');
// The Tree stands at the roots-room (sanctuary, 72,27). Warp beside it.
await page.evaluate(async () => (await import('/js/game.js')).debugWarp(72, 26));
await settle(700); await clearLevelUp();
const h3 = await hud();
check('the Tree offers itself', h3?.promptVerb === 'Behold', `"${h3?.promptVerb} ${h3?.promptName}"`);
if (h3?.promptVerb === 'Behold') {
  await page.keyboard.press('e'); await settle(2600);   // the fated reveal takes its 2.1s
  check('  and the reading opens', (await page.locator('.aq-read').count()) > 0);
  await page.keyboard.press('Escape'); await settle(400);
}

console.log('=== deeper floors keep the plain way down ===');
await page.evaluate(async () => (await import('/js/game.js')).debugDive());
await settle(1500); await clearLevelUp();
const h4 = await hud();
check('the descent from the shore is plain — the Weeping Gallery',
  h4?.region === 'The Weeping Gallery' && h4?.level === 3, `"${h4?.region}" at level ${h4?.level}`);

console.log('=== console ===');
check('the game logged no errors', errors.length === 0, errors.slice(0, 3).join(' | '));

console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed`);
await browser.close();
process.exit(failed === 0 ? 0 : 1);
