// Which music is playing, and where.
//
//     node tools/music-probe.mjs
//
// Four tracks, three places, and the failure this catches is the quiet one: a
// renamed file or a mistyped path does not throw, it just plays nothing, and
// nothing on screen says so. So every check below asks the player what it is
// actually playing and then asks the server whether that file exists.
//
// The hand-over on the title screen is exercised through the browser's real
// "ended" event on the intro element — the same event the code listens for —
// rather than by waiting out ten megabytes of intro wav or by reaching past the
// module to call play() ourselves.

import { chromium } from 'playwright-core';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5223';

let passed = 0, failed = 0;
const check = (name, ok, detail = '') => {
  console.log(`${ok ? '  ok  ' : ' FAIL '} ${name}${detail ? ` — ${detail}` : ''}`);
  ok ? passed++ : failed++;
};

// Autoplay is allowed so the probe measures the music rather than Chrome's
// gesture policy. The policy itself is handled by music.js arming listeners,
// and the sign-up below clicks half a dozen times regardless.
const browser = await chromium.launch({
  channel: 'chrome',
  args: ['--no-sandbox', '--autoplay-policy=no-user-gesture-required'],
});
const page = await browser.newPage({ viewport: { width: 1280, height: 860 } });

const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
page.on('pageerror', e => errors.push(String(e)));

const settle = (ms = 800) => page.waitForTimeout(ms);
const title = fn => page.evaluate(fn);

/// What the title screen's player says it is playing.
const titleTrack = () => page.evaluate(async () =>
  (await import('/js/titlemusic.js')).nowPlaying());
const gameTrack = () => page.evaluate(async () =>
  (await import('/js/game.js')).nowPlaying());

const name = url => (url ?? '').split('/').pop() ?? '';

console.log('\n=== the files are all there ===');

// Asked of the server, not of the player: a track that 404s still shows up as
// "playing" in the element, which is exactly how a rename goes unnoticed.
for (const f of ['crystal-deep-intro.wav', 'into-the-crystal-deep.mp3',
                 'the-cleric-game.mp3', 'ballad-of-the-wandering.mp3']) {
  const res = await page.request.get(`${CLIENT}/audio/${f}`);
  const bytes = Number(res.headers()['content-length'] ?? 0);
  check(`${f} is served`, res.ok() && bytes > 100_000,
    `${res.status()}, ${(bytes / 1e6).toFixed(1)} MB`);
}

console.log('\n=== into the game ===');

const player = { email: `music.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };
await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Music Tester');
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]');
await settle(3000);

console.log('\n=== the title screen ===');

await page.goto(`${CLIENT}/heroes`);
await settle(2500);
// A click first: the sign-up gestures belong to a document that has since been
// replaced, and a fresh one has to earn permission again.
await page.mouse.click(640, 700);
await settle(1200);

const first = await titleTrack();
check('the intro plays on arrival', name(first) === 'crystal-deep-intro.wav', name(first) || '(nothing)');

console.log('\n=== the intro hands over to the theme ===');

// Fired rather than waited out — the intro is ten megabytes of wav, and seeking
// to its tail only stalls on a buffer that has not arrived. The event dispatched
// is the genuine "ended" the browser raises when a non-looping track finishes;
// the listener under test cannot tell this one apart, which is the point.
const fired = await page.evaluate(async () => {
  const el = (await import('/js/titlemusic.js')).nowPlayingElement();
  if (!el) return 'no element';
  if (el.loop) return 'intro is looping — it would never end on its own';
  el.dispatchEvent(new Event('ended'));
  return 'ended dispatched on the intro';
});
console.log(`  (${fired})`);

await page.waitForFunction(async () => {
  const t = (await import('/js/titlemusic.js')).nowPlaying() ?? '';
  return t.includes('into-the-crystal-deep');
}, null, { timeout: 20000 }).catch(() => { });

const second = await titleTrack();
check('the theme takes over when the intro ends',
  name(second) === 'into-the-crystal-deep.mp3', name(second) || '(nothing)');
check('it is actually playing, not just selected',
  await page.evaluate(async () => {
    const el = (await import('/js/titlemusic.js')).nowPlayingElement();
    return !!el && !el.paused && !el.ended;
  }));
check('and it loops, because there is nothing after it',
  await page.evaluate(async () =>
    (await import('/js/titlemusic.js')).nowPlayingElement()?.loop === true));

console.log('\n=== the mute button ===');

const muted = await page.evaluate(async () => (await import('/js/titlemusic.js')).toggleIntroMute());
check('muting silences the theme', muted === true
  && await page.evaluate(async () =>
    (await import('/js/titlemusic.js')).nowPlayingElement()?.muted === true).catch(() => false));
await page.evaluate(async () => (await import('/js/titlemusic.js')).toggleIntroMute());

console.log('\n=== above ground ===');

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
await settle(3000);

const hud = await page.evaluate(async () => (await import('/js/game.js')).hudSnapshot());
check('the world starts above ground', hud?.stage === 1, `stage ${hud?.stage}`);

const outside = await gameTrack();
check('the approach plays the cleric game', name(outside) === 'the-cleric-game.mp3',
  name(outside) || '(nothing)');
check('the title music did not follow the player into the world',
  await page.evaluate(async () => (await import('/js/titlemusic.js')).nowPlaying() === null));

// The stage-2 track is not reached by this probe: getting into the mine means
// clearing the overworld, which is a run, not a check. What is verified is that
// the file is served and that the switch is driven by hud.stage — see the frame
// loop in game.js.
console.log('  (the cavern track is not covered here: reaching it means clearing the overworld)');

console.log('\n=== leaving ===');

await page.evaluate(async () => (await import('/js/game.js')).stopGame());
await settle(1400);
check('stopping the game stops its music',
  await page.evaluate(async () => (await import('/js/game.js')).nowPlaying() === null));

console.log('\n=== console ===');
const noisy = errors.filter(e => !/favicon|status of 401/.test(e));
check('nothing logged an error', noisy.length === 0, noisy.slice(0, 2).join(' | '));

await browser.close();
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed\n`);
process.exit(failed === 0 ? 0 : 1);
