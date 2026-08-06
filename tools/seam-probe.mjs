// Walk across a region seam and prove the player cannot tell.
//
//     node tools/seam-probe.mjs
//
// Stands up Ashwold, walks the party north up the mine road until the map
// changes, and checks the three things that make a seam invisible: the map
// really changed, the party arrived at the far edge rather than the middle, and
// the crossing did NOT fade — a door earns a fade, a stride does not.
import { chromium } from 'playwright-core';

const C = process.env.CLIENT ?? 'http://localhost:5223';
const b = await chromium.launch({ channel: 'chrome', args: ['--no-sandbox'] });
const p = await b.newPage({ viewport: { width: 1280, height: 800 } });
const errs = []; p.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
const s = ms => p.waitForTimeout(ms);
let pass = 0, fail = 0;
const ok = (c, m) => { c ? pass++ : fail++; console.log(`  ${c ? 'ok  ' : 'FAIL'}   ${m}`); };
const hud = () => p.evaluate(async () => (await import('/js/game.js')).hudSnapshot());
const warp = (x, y) => p.evaluate(async a => (await import('/js/game.js')).debugWarp(a.x, a.y), { x, y });

const u = { email: `seam.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };
await p.goto(`${C}/signup`); await s(2200);
await p.fill('#signup-name', 'Seam'); await p.fill('#signup-email', u.email);
await p.fill('#signup-password', u.password); await p.fill('#signup-confirm', u.password);
await p.click('button[type=submit]'); await s(2500);
await p.goto(`${C}/play`);
await p.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await s(14000);
const clear = async () => { for (let i = 0; i < 4; i++) {
  await p.click('.aq-levelup-go', { timeout: 800 }).catch(() => {});
  await s(200);
  if (await p.locator('.aq-cw.shown').count()) { await p.keyboard.press('c'); await s(200); }
} };
await clear();

console.log('=== crossing north out of Ashwold ===');
ok(await p.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r1_ashwold')), 'Ashwold stands up');
await s(1200); await clear();
const before = await hud();
ok(before.region === 'Ashwold', `starts in Ashwold (got "${before.region}")`);

// On the mine road at the very top of the village map, then walk north.
await warp(36, 2); await s(500); await clear();
const at2 = await hud();
ok(at2.heroTx >= 34 && at2.heroTx <= 37, `on the mine road at the north edge (x=${at2.heroTx})`);

await p.keyboard.down('w');
let crossed = null;
for (let i = 0; i < 40 && !crossed; i++) {
  await s(150);
  const h = await hud();
  if (h.region !== 'Ashwold') crossed = h;
}
await p.keyboard.up('w');

ok(!!crossed, 'walking north changed the map');
if (crossed) {
  ok(crossed.region === 'The Whispering Wood', `arrived in the Whispering Wood (got "${crossed.region}")`);
  ok(crossed.heroTy >= 50, `set down at the FAR edge, not the middle (y=${crossed.heroTy})`);
  ok(Math.abs(crossed.heroTx - at2.heroTx) <= 2,
     `kept its column across the seam (${at2.heroTx} -> ${crossed.heroTx})`);
}
await s(600);
await p.screenshot({ path: 'tools/shots/seam-after-crossing.png' });

// And back again: a seam has to work both ways or it is a trapdoor.
await p.keyboard.down('s');
let back = null;
for (let i = 0; i < 40 && !back; i++) {
  await s(150);
  const h = await hud();
  if (h.region === 'Ashwold') back = h;
}
await p.keyboard.up('s');
ok(!!back, 'walking south came back to Ashwold');
if (back) ok(back.heroTy <= 5, `set down at Ashwold's north edge (y=${back.heroTy})`);

// ------------------------------------------------------------------
console.log('\n=== east out of the wood, into Deepdelve ===');
ok(await p.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r2_whispering_wood')),
   'the wood stands up');
await s(1200); await clear();
// On the mountain road at the wood's east edge, then walk east.
await warp(70, 13); await s(500); await clear();
const at3 = await hud();
ok(at3.region === 'The Whispering Wood', `starts in the wood (got "${at3.region}")`);
await p.keyboard.down('d');
let east = null;
for (let i = 0; i < 40 && !east; i++) {
  await s(150);
  const h = await hud();
  if (h.region !== 'The Whispering Wood') east = h;
}
await p.keyboard.up('d');
ok(!!east, 'walking east changed the map');
if (east) {
  ok(east.region === 'Deepdelve', `arrived in Deepdelve (got "${east.region}")`);
  ok(east.heroTx <= 5, `set down at the far edge (x=${east.heroTx})`);
  ok(Math.abs(east.heroTy - at3.heroTy) <= 2,
     `kept its row across the seam (${at3.heroTy} -> ${east.heroTy})`);
}

// ------------------------------------------------------------------
console.log('\n=== and the world can reach the cave again ===');
// The whole reason Deepdelve had to exist: until it did, nothing in the new
// Stage 1 had a way down, which is why the old map was still the one being played.
await p.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r3_deepdelve'));
await s(1200); await clear();
// The mouth is a doorway now — [E] at the threshold, and the summon die
// answers first: a good light descends, a bad one holds the door. Either is
// the game working; only silence is a failure.
await warp(54, 16); await s(600); await clear();
const shelf = await hud();
ok(shelf.stage === 1, `on the shelf, above ground (stage ${shelf.stage})`);
await warp(54, 14); await s(600); await clear();
const beforeMouth = ((await p.evaluate(async () => (await import('/js/game.js')).diceSeen())) ?? []).length;
await p.keyboard.press('e');
let down = null;
for (let i = 0; i < 30 && !down; i++) {
  await s(200);
  await p.click('.aq-cutscene-skip', { timeout: 200 }).catch(() => { });
  const h = await hud();
  if (h.stage === 2) down = h;
}
const mouthRoll = ((await p.evaluate(async () => (await import('/js/game.js')).diceSeen())) ?? [])
  .slice(beforeMouth).find(d => d.kind === 'summon');
ok(!!mouthRoll, 'the light is consulted at the threshold');
if (down) ok(down.level === 1, `and the descent granted is to the first depth (level ${down.level})`);
else ok(mouthRoll?.outcome === 'bad', `the dark held the door (outcome ${mouthRoll?.outcome})`);
await p.screenshot({ path: 'tools/shots/seam-into-the-cave.png' });
// A granted descent leaves the party IN the cave — climb back out before the
// ring walk, or every region load after this reads "The Crystal Cistern".
if (down) {
  await p.click('.aq-cutscene-skip', { timeout: 1500 }).catch(() => { });
  await p.evaluate(async () => (await import('/js/game.js')).debugLeaveCave());
  await s(1200); await clear();
}

// ------------------------------------------------------------------
console.log('\n=== the ring closes: south out of Deepdelve, west out of the road ===');
// R3 -> R4: the miners' track down at the top of the road country.
ok(await p.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r3_deepdelve')),
   'Deepdelve stands up');
await s(1200); await clear();
await warp(31, 54); await s(500); await clear();     // on the south track
await p.keyboard.down('s');
let r4 = null;
for (let i = 0; i < 40 && !r4; i++) { await s(150); const h = await hud(); if (h.region !== 'Deepdelve') r4 = h; }
await p.keyboard.up('s');
ok(!!r4, 'walking south changed the map');
if (r4) ok(r4.region === 'The Kae Ychel Road', `arrived on the Kae Ychel Road (got "${r4.region}")`);

// R4 -> R1: west off the road country brings you back to the village. The ring.
await warp(2, 32); await s(500); await clear();
const onroad = await hud();
ok(onroad.region === 'The Kae Ychel Road', `back on the road (got "${onroad.region}")`);
await p.keyboard.down('a');
let home = null;
for (let i = 0; i < 40 && !home; i++) { await s(150); const h = await hud(); if (h.region !== 'The Kae Ychel Road') home = h; }
await p.keyboard.up('a');
ok(!!home, 'walking west off the road changed the map');
if (home) ok(home.region === 'Ashwold',
   `the ring closes — the optional wing comes home to Ashwold (got "${home.region}")`);

console.log('=== console ===');
ok(errs.length === 0, `the game logged no errors${errs.length ? ': ' + errs[0] : ''}`);
console.log(`\n${fail ? 'FAILURES' : 'ALL PASS'}: ${pass} passed, ${fail} failed`);
await b.close();
process.exit(fail ? 1 : 0);
