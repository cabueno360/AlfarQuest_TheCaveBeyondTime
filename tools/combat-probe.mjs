// The advanced combat system, as data and as played.
//
//     node tools/combat-probe.mjs
//
// Two halves. First the model, read straight off the engine: the weapon types
// differ, the damage schools exist, the status architecture names them, and the
// resistances land on the right types. Then the fight, on the deterministic
// staged encounter: a weapon rolls a range rather than a fixed number, blows can
// miss, and a hammer stuns.

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
const facts = () => page.evaluate(async () => (await import('/js/game.js')).combatFacts());
const clearLevelUp = async () => {
  for (let i = 0; i < 4 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 4000 }).catch(() => { });
    await settle(700);
  }
};

const player = { email: `combat.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };

console.log('\n=== into the world ===');
await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Combat Tester');
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

console.log('\n=== weapons are not all the same weapon ===');

const f = await facts();
const weapons = f?.weapons ?? [];
check('there is a weapon type catalogue', weapons.length >= 6, `${weapons.length} types`);
const speeds = weapons.map(w => w.speed);
const hammer = weapons.find(w => w.type === 'Hammer');
const dagger = weapons.find(w => w.type === 'Dagger');
check('they swing at different speeds', new Set(speeds).size >= 5,
  weapons.map(w => `${w.name} ${w.speed}`).join(', '));
check('the hammer is slow and the dagger fast', hammer?.speed > 1.3 && dagger?.speed < 0.8,
  `hammer ${hammer?.speed}, dagger ${dagger?.speed}`);
check('only the hammer stuns', hammer?.stun > 0 && dagger?.stun === 0,
  `hammer stun ${hammer?.stun}`);
check('the dagger trades damage for critical chance', dagger?.crit > hammer?.crit,
  `dagger crit ${dagger?.crit}`);
check('the spear reaches further than the dagger',
  weapons.find(w => w.type === 'Spear')?.reach > dagger?.reach);

console.log('\n=== damage has types, and creatures resist them ===');

const dtypes = f?.damageTypes ?? [];
check('damage comes in physical and magical schools',
  dtypes.some(d => d.category === 'Physical') && dtypes.some(d => d.category === 'Magic'),
  `${dtypes.length} types`);
check('the magic schools the brief names are all here',
  ['Fire', 'Ice', 'Lightning', 'Nature', 'Holy', 'Dark'].every(n => dtypes.some(d => d.name === n)));
const r = f?.resistances ?? {};
check('crystal turns a point aside but shatters under a hammer',
  r.slimePiercing > 0 && r.slimeBlunt < 0, `piercing ${r.slimePiercing}, blunt ${r.slimeBlunt}`);
check('stone is the opposite — hard to cut, crushed all the same',
  r.wormSlashing > 0 && r.wormBlunt < 0, `slashing ${r.wormSlashing}, blunt ${r.wormBlunt}`);

console.log('\n=== the status-effect architecture names them all ===');

const effects = (f?.statusEffects ?? []).map(e => e.name);
check('burning, poison, freeze, stun and the rest are catalogued',
  ['Burning', 'Poison', 'Freeze', 'Stun'].every(n => effects.includes(n)), effects.join(', '));
check('some deal damage over time, some hold in place',
  (f.statusEffects).some(e => e.behaviour === 'DamageOverTime')
  && (f.statusEffects).some(e => e.behaviour === 'Immobilise'));

console.log('\n=== every hero carries a different weapon ===');

const party = (await hud())?.party ?? [];
const mage = party.find(p => p.key === 'mage');
const cleric = party.find(p => p.key === 'cleric');
const thief = party.find(p => p.key === 'thief');
check('the mage wields a fire staff', mage?.dmgType === 'Fire',
  `${mage?.dmgType} ${mage?.dmgMin}-${mage?.dmgMax}`);
check('the cleric wields a blunt, slow, stunning weapon',
  cleric?.dmgType === 'Blunt' && cleric?.weaponSpeed > 1.3 && cleric?.stunChance > 0,
  `${cleric?.dmgType} speed ${cleric?.weaponSpeed} stun ${cleric?.stunChance}`);
check('the thief looses piercing bolts', thief?.dmgType === 'Piercing');
check('each weapon is a range, not a fixed number',
  party.every(p => p.dmgMax > p.dmgMin), party.map(p => `${p.key} ${p.dmgMin}-${p.dmgMax}`).join(', '));

console.log('\n=== the fight: a range, a stun, a miss ===');

const start = await hud();

// Whale on an immortal, evasive training dummy — a live creature dies in a hit
// or two, far too few rolls for a 20%-chance stun or an evasive miss to show.
// The dummy sits to the right, so hold right-and-attack to keep swinging at it.
// Whales on the dummy for up to `beats`, stopping early once `until(hud)` is
// satisfied — so a rare event (a 20%-chance stun) gets as many swings as it needs
// without padding every run to the worst case.
const beatOn = async (beats, until = null) => {
  await page.evaluate(async () => (await import('/js/game.js')).debugTrainingDummy());
  await settle(400);
  // The dummy sits just right of centre. A hero faces the mouse, not the movement
  // keys, so aim the cursor right — otherwise the swing cone points the wrong way
  // and never touches it. Holding still (no move keys) keeps it in reach.
  await page.mouse.move(880, 430);
  await page.keyboard.down('j');
  for (let i = 0; i < beats; i++) {
    await page.mouse.move(880, 430);
    await settle(260);
    await clearLevelUp();
    if (until && until(await hud())) break;
  }
  await page.keyboard.up('j');
  await settle(200);
};

// The cleric's hammer: slow, heavy, and the only thing that stuns. Beat until one
// lands — it is a one-in-five chance per hit, so a fixed count would flake.
await page.keyboard.press('F2');
await settle(300);
const stunBase = start?.stuns ?? 0;
await beatOn(80, h => h && h.stuns > stunBase && new Set(h.recentHits).size >= 5);
const afterCleric = await hud();
check('the hammer stunned the dummy', afterCleric.stuns > (start?.stuns ?? 0),
  `${start?.stuns ?? 0} → ${afterCleric.stuns}`);
check('the hammer rolled a range of damage, not one number',
  new Set(afterCleric.recentHits).size >= 4, `hits: ${afterCleric.recentHits.slice(0, 10).join(', ')}`);
check('its rolls sat inside the weapon range',
  afterCleric.recentHits.length > 0 && Math.min(...afterCleric.recentHits) >= 1
  && Math.max(...afterCleric.recentHits) <= cleric.dmgMax * 2.2,   // headroom for a crit
  `${Math.min(...afterCleric.recentHits)}–${Math.max(...afterCleric.recentHits)}, weapon ${cleric.dmgMin}-${cleric.dmgMax}`);

// The thief's bow: fast and light, so it looses far more shots and gets far more
// rolls against the evasive dummy — which is where a miss finally shows.
await page.keyboard.press('F3');
await settle(300);
await beatOn(28);
const end = await hud();
check('some shots missed the evasive dummy', end.misses > (afterCleric?.misses ?? 0),
  `${afterCleric?.misses ?? 0} → ${end.misses} misses`);

console.log('\n=== console ===');
const noisy = errors.filter(e => !/favicon|status of 401|AudioContext/.test(e));
check('the game logged no errors', noisy.length === 0, noisy.slice(0, 2).join(' | '));

await browser.close();
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed\n`);
process.exit(failed === 0 ? 0 : 1);
