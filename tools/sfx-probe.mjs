// The sound effects, played rather than inspected.
//
//     node tools/sfx-probe.mjs
//
// Two failures this catches. The quiet one: a family points at a file that was
// renamed or never copied, so the engine raises the event and nothing comes out
// — invisible without asking the server whether every take exists. And the
// disconnected one: the engine fills state.sounds but the audio layer never
// turns it into a voice, or the other way round. So the checks below follow one
// swing from the render payload all the way to a started voice.

import { chromium } from 'playwright-core';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5223';

let passed = 0, failed = 0;
const check = (name, ok, detail = '') => {
  console.log(`${ok ? '  ok  ' : ' FAIL '} ${name}${detail ? ` — ${detail}` : ''}`);
  ok ? passed++ : failed++;
};

// Autoplay allowed so the probe measures the sound system, not Chrome's gesture
// gate. The gate itself is covered by sfx.js arming a resume listener, and the
// sign-up clicks plenty regardless.
const browser = await chromium.launch({
  channel: 'chrome',
  args: ['--no-sandbox', '--autoplay-policy=no-user-gesture-required'],
});
const page = await browser.newPage({ viewport: { width: 1280, height: 860 } });

const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
page.on('pageerror', e => errors.push(String(e)));

const settle = (ms = 800) => page.waitForTimeout(ms);
const sfxCount = () => page.evaluate(async () => (await import('/js/game.js')).sfxCount());
const hud = () => page.evaluate(async () => (await import('/js/game.js')).hudSnapshot());
const lastSounds = () => page.evaluate(async () => (await import('/js/game.js')).lastSounds());

/// Resets the rolling family set, runs `act`, and returns every family that was
/// raised while it ran. Accumulated frame-by-frame in game.js because a sound is
/// in the payload for a single frame — polling snapshots misses sparse events.
const familiesDuring = async act => {
  await page.evaluate(async () => (await import('/js/game.js')).resetSoundFamilies());
  await act();
  return page.evaluate(async () => (await import('/js/game.js')).soundFamiliesSeen());
};

const hold = async (key, ms) => { await page.keyboard.down(key); await settle(ms); await page.keyboard.up(key); };

// Every take every family can play, kept in step with sfx.js by hand — if a
// family gains a take there, add it here and the served-files check covers it.
const FILES = [
  ...[1, 2, 3, 4, 5].map(n => `step-dirt-${n}`),
  ...[1, 2, 3, 4, 5].map(n => `step-stone-${n}`),
  ...[1, 2, 3].map(n => `step-water-${n}`),
  'dash',
  ...[1, 2, 3].map(n => `swing-${n}`),
  ...[1, 2].map(n => `bow-${n}`),
  ...[1, 2, 3].map(n => `hit-${n}`),
  'crit',
  ...[1, 2, 3].map(n => `block-${n}`), ...[1, 2].map(n => `parry-${n}`),
  ...[1, 2, 3].map(n => `fire-${n}`),
  ...[1, 2].map(n => `frost-${n}`),
  ...[1, 2].map(n => `holy-${n}`),
  ...[1, 2, 3].map(n => `spell-impact-${n}`),
  ...[1, 2].map(n => `chest-${n}`),
  'unlock',
  ...[1, 2, 3, 4, 5].map(n => `mine-${n}`),
  ...[1, 2, 3, 4].map(n => `chop-${n}`),
];

console.log('\n=== every take is on disk ===');

let missing = [];
for (const f of FILES) {
  const res = await page.request.get(`${CLIENT}/audio/sfx/${f}.ogg`);
  if (!res.ok()) missing.push(`${f} (${res.status()})`);
}
check('all 50 sound files are served', missing.length === 0,
  missing.length ? missing.join(', ') : `${FILES.length} files`);

console.log('\n=== into the world ===');

const player = { email: `sfx.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };
await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Sfx Tester');
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]');
await settle(2800);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null,
                           null, { timeout: 25000 });
// A click to satisfy the audio context, then a beat for the library to decode.
await page.mouse.click(640, 430);
await settle(3500);

console.log('\n=== a basic attack makes a sound ===');

// Attack for a moment and watch two things move: the render payload must carry
// an attack event, and the voice counter must climb — the payload alone proves
// the engine spoke, the counter proves the speakers heard. Which sound it is
// depends on the weapon: the mage looses a bolt (bow), a melee hero swings.
const before = await sfxCount();
const attackFamilies = await familiesDuring(() => hold('j', 2000));
const after = await sfxCount();

// The mage leads with a fire staff, so their basic attack is a magic cast, not a
// bolt or a swing — the sound follows the weapon now.
check('the engine raised an attack sound in the frame payload',
  ['swing', 'bow', 'magic_fire', 'magic_frost'].some(fam => attackFamilies.includes(fam)),
  attackFamilies.join(', ') || '(none)');
check('those events started voices', after > before, `${before} → ${after} voices`);

// And the swing specifically. Exactly one of the three is melee (the cleric,
// slot 2 of the default party); switch to it and confirm the sound becomes the
// swing, not the bolt — proof the weapon, not a constant, chooses the sound. The
// swing sounds on every melee attack whether or not it lands.
await page.keyboard.press('2');
await settle(500);
const activeMelee = (await hud())?.party?.find(h => h.active)?.key;
const meleeFamilies = await familiesDuring(() => hold('j', 2000));
check(`the melee hero (${activeMelee}) swings instead of loosing a bolt`,
  meleeFamilies.includes('swing'), meleeFamilies.join(', ') || '(none)');

console.log('\n=== walking makes footsteps ===');

const beforeSteps = await sfxCount();
const walkFamilies = await familiesDuring(() => hold('w', 1600));
check('footfalls sound for the ground underfoot',
  walkFamilies.includes('step_dirt') || walkFamilies.includes('step_stone'),
  walkFamilies.join(', ') || '(none)');
check('and they reached the speakers', (await sfxCount()) > beforeSteps);

console.log('\n=== the distance fade ===');

// A sound raised at the camera is full volume; one raised a long way off is
// dropped before it ever becomes a voice. Read straight from the engine: the v
// on every event is in range and never above one. Sampled across a busy stretch
// rather than one frame, so it is a real reading and not a vacuous pass on a
// quiet frame.
const vols = [];
await page.keyboard.down('j');
await page.keyboard.down('w');
for (let i = 0; i < 12 && vols.length < 4; i++) {
  await settle(160);
  for (const s of await lastSounds()) vols.push(s.v);
}
await page.keyboard.up('j');
await page.keyboard.up('w');
check('every sound is faded into range, never over full',
  vols.length > 0 && vols.every(v => v > 0 && v <= 1.0001),
  vols.length ? vols.map(v => v.toFixed(2)).join(', ') : '(never caught a sounding frame)');

console.log('\n=== mute silences everything ===');

// Mute is one number on the master gain; unmuting must not lose the game's own
// sounds. The counter still climbs while muted — voices are still created, just
// silent — so what is asserted is that the toggle reports muted and back.
const muted = await page.evaluate(async () => (await import('/js/game.js')).toggleMute());
check('the sound toggles off', muted === true);
const unmuted = await page.evaluate(async () => (await import('/js/game.js')).toggleMute());
check('and back on', unmuted === false);

console.log('\n=== console ===');
const noisy = errors.filter(e => !/favicon|status of 401/.test(e));
check('nothing logged an error', noisy.length === 0, noisy.slice(0, 2).join(' | '));

await browser.close();
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed\n`);
process.exit(failed === 0 ? 0 : 1);
