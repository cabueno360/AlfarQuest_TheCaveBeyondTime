// The action hotbar and the active-skill system, played.
//
//     node tools/skills-probe.mjs
//
// What this proves: every hero carries their own four skills on the bar, cast by
// 1–4; a skill spends mana and runs its own cooldown; a skill not yet learned
// sits locked; the potion is its own button; and switching heroes swaps the whole
// bar. Read from the render payload the hotbar is drawn from, so what the test
// sees is what the player sees.

import { chromium } from 'playwright-core';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5223';

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
const active = async () => (await hud())?.party?.find(p => p.active);
const clearLevelUp = async () => {
  for (let i = 0; i < 4 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 4000 }).catch(() => { });
    await settle(700);
  }
};

const player = { email: `skills.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };

console.log('\n=== into the world ===');
await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Skill Tester');
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]');
await settle(2600);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null,
                           null, { timeout: 25000 });
await settle(2500);
await clearLevelUp();

console.log('\n=== the hotbar has the active hero\'s four skills ===');

let h = await hud();
const bar = h?.hotbar ?? [];
check('there is an action hotbar', bar.length === 4, `${bar.length} slots`);
check('every slot names a skill and its shortcut',
  bar.every(s => s.name && s.icon && s.shortcut === String(s.slot)),
  bar.map(s => `${s.shortcut}:${s.name}`).join(', '));
check('the potion is its own button, off cooldown at the start',
  h.potionReady === true && h.potionCdFrac === 0);

console.log('\n=== skills are learned as the hero grows ===');

// At level 1 the first skills are ready and the later ones sit locked behind the
// level they unlock at — which is the hotbar "filling in as you learn skills".
const s1 = bar.find(s => s.slot === 1);
const locked = bar.filter(s => !s.unlocked);
check('the first skill is unlocked and castable at level 1', s1?.unlocked && s1?.ready,
  `${s1?.name} ready=${s1?.ready}`);
check('the later skills are locked behind a level',
  locked.length >= 1 && locked.every(s => s.unlockLevel > 1),
  locked.map(s => `${s.name}@L${s.unlockLevel}`).join(', ') || '(none locked)');

console.log('\n=== casting spends mana and starts a cooldown ===');

const manaBefore = (await active())?.mana ?? 0;
await page.mouse.move(880, 430);           // aim right, so an aimed bolt has somewhere to go
await page.keyboard.press('1');            // cast skill 1
await settle(350);
h = await hud();
const s1After = h.hotbar.find(s => s.slot === 1);
const manaAfter = (await active())?.mana ?? 0;
check('the skill went on cooldown', s1After?.cdFrac > 0, `cdFrac ${s1After?.cdFrac?.toFixed(2)}`);
check('the skill spent mana', manaAfter < manaBefore, `${manaBefore.toFixed(0)} → ${manaAfter.toFixed(0)}`);
check('a skill on cooldown is not castable', s1After?.ready === false);

console.log('\n=== a locked skill does nothing ===');

const lockedSlot = locked[0]?.slot;
const manaPreLocked = (await active())?.mana ?? 0;
if (lockedSlot) {
  await page.keyboard.press(String(lockedSlot));
  await settle(300);
  const lockedAfter = (await hud()).hotbar.find(s => s.slot === lockedSlot);
  // Mana only ever regenerates upward while we wait, so "spent nothing" is "did
  // not drop", not "unchanged".
  const manaPostLocked = (await active())?.mana ?? 0;
  check('pressing a locked skill spends nothing and starts no cooldown',
    lockedAfter?.cdFrac === 0 && manaPostLocked >= manaPreLocked - 0.5,
    `cdFrac ${lockedAfter?.cdFrac}, mana ${manaPreLocked.toFixed(0)} → ${manaPostLocked.toFixed(0)}`);
} else {
  check('pressing a locked skill spends nothing and starts no cooldown', true, '(no locked slot this build)');
}

console.log('\n=== the potion is a button of its own ===');

await page.keyboard.press('q');
await settle(300);
h = await hud();
check('drinking a potion puts it on cooldown', h.potionCdFrac > 0 && h.potionReady === false,
  `cdFrac ${h.potionCdFrac?.toFixed(2)}`);

console.log('\n=== switching heroes swaps the whole bar ===');

const mageBar = (await hud()).hotbar.map(s => s.name).join(',');
await page.keyboard.press('F2');           // to the cleric
await settle(400);
const clericHero = await active();
const clericBar = (await hud()).hotbar.map(s => s.name).join(',');
check('the steered hero changed', clericHero?.key === 'cleric', clericHero?.key);
check('the cleric brings a different set of skills', clericBar !== mageBar,
  `mage [${mageBar}] vs cleric [${clericBar}]`);
check('the cleric\'s bar includes a heal', (await hud()).hotbar.some(s => /mend|heal/i.test(s.name)),
  clericBar);

console.log('\n=== a cast actually lands ===');

// Stand the cleric next to an immortal dummy and cast Smite at it — the hit
// register climbs, so the skill dealt real damage, not just an animation.
await page.evaluate(async () => (await import('/js/game.js')).debugTrainingDummy());
await settle(400);
const hitsBefore = (await hud())?.recentHits?.length ?? 0;
for (let i = 0; i < 8; i++) {
  await page.mouse.move(880, 430);
  await page.keyboard.press('1');
  await settle(320);
  if (((await hud())?.recentHits?.length ?? 0) > hitsBefore) break;
  await clearLevelUp();
}
check('a cast skill deals damage', ((await hud())?.recentHits?.length ?? 0) > hitsBefore,
  `hits ${hitsBefore} → ${(await hud())?.recentHits?.length}`);

console.log('\n=== console ===');
const noisy = errors.filter(e => !/favicon|status of 401|AudioContext/.test(e));
check('the game logged no errors', noisy.length === 0, noisy.slice(0, 2).join(' | '));

await browser.close();
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed\n`);
process.exit(failed === 0 ? 0 : 1);
