// Stage 1 — the RING of four hand-authored regions — played rather than
// inspected.
//
//     node tools/worldmap-probe.mjs
//
// Stage 1 is no longer one big generated map: it is Ashwold, the Whispering
// Wood, Deepdelve and the Kae Ychel road, four maps joined in a ring so that the
// optional wing comes home instead of doubling back. This walks each region,
// checks it is a real place (its name, its people, a canonical villager), opens
// the interiors that hang off it (the Cleric's house, the Academy, Seoshe and
// the warehouse within it), and confirms the cave mouth in Deepdelve still takes
// the party down. The seam crossings themselves are seam-probe's job; this is
// about the places.

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
const warp = (x, y) => page.evaluate(async a => (await import('/js/game.js')).debugWarp(a.x, a.y), { x, y });
const region = id => page.evaluate(async r => (await import('/js/game.js')).debugLoadRegion(r), id);
const markers = () => page.evaluate(async () => (await import('/js/game.js')).npcMarkers());
const press = k => page.keyboard.press(k);
const text = async sel => { try { return (await page.textContent(sel, { timeout: 2000 })) ?? ''; } catch { return ''; } };

const clearLevelUp = async () => {
  for (let i = 0; i < 4 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 3000 }).catch(() => { });
    await settle(400);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(350); }
  }
  if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(300); }
};

/// Warp to a tile and CONFIRM the party got there before reading anything.
/// debugWarp does nothing while the world is paused, and the world pauses behind
/// a level-up card — so a blind warp reads whoever was standing at the spawn.
const standAt = async (x, y) => {
  for (let i = 0; i < 4; i++) {
    await clearLevelUp();
    await warp(x, y); await settle(300);
    const h = await hud();
    if (h && Math.abs(h.heroTx - x) <= 1 && Math.abs(h.heroTy - y) <= 1) {
      await clearLevelUp(); await settle(200);
      return hud();
    }
  }
  return hud();
};

const player = { email: `world.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };

console.log('\n=== into the world ===');
await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'World Tester');
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
// The world settles a few seconds after the page loads. Clear the startup
// transient, then confirm villagers are present, before touching anything.
await page.waitForTimeout(4500);
await page.waitForFunction(() => {
  try { return (JSON.parse(DotNet.invokeMethod('AlfarQuest.Client', 'Snapshot')).npcs ?? []).length > 0; }
  catch { return false; }
}, null, { timeout: 20000, polling: 1000 });
await clearLevelUp();

const start = await hud();
check('the world opens on the overworld', start?.stage === 1, `stage ${start?.stage}`);
check('and it opens in the village, Ashwold', start?.region === 'Ashwold', `"${start?.region}"`);

// ------------------------------------------------------------------
console.log('\n=== the ring: four regions, each a real place ===');

const REGIONS = [
  { id: 'r1_ashwold', name: 'Ashwold', who: 'Dagna', role: 'the smith' },
  { id: 'r2_whispering_wood', name: 'The Whispering Wood', who: 'Brother Enoch', role: 'the chapel keeper' },
  { id: 'r3_deepdelve', name: 'Deepdelve', who: 'Captain Orlo', role: 'the pit captain' },
  { id: 'r4_kae_ychel_road', name: 'The Kae Ychel Road', who: 'Ondu the Long-Hauler', role: 'the caravan master' },
];

for (const r of REGIONS) {
  check(`${r.name} stands up`, await region(r.id), r.id);
  await settle(900);
  const h = await hud();
  check(`  and it names itself`, h?.region === r.name, `"${h?.region}"`);
  const mk = await markers();
  check(`  and it is peopled`, (mk?.length ?? 0) >= 3, `${mk?.length ?? 0} villagers`);
  // The canonical villager is confirmed by PRESENCE, not by walking up: many of
  // them stand beside their own board or stock, and [E] there reads the notice
  // before it greets the person — a placement quirk, not a missing NPC.
  check(`  ${r.role} (${r.who}) is here`,
    (mk ?? []).some(n => n.name === r.who), (mk ?? []).map(n => n.name).slice(0, 4).join(', '));
}

// ------------------------------------------------------------------
console.log('\n=== the canonical placements from the book ===');

// Cerno of Kaladash, at the cave mouth in Deepdelve, as the book puts him.
await region('r3_deepdelve'); await settle(900);
const cerno = await standAt(51, 14);
check('the cave guide waits at the mouth', cerno?.promptName === 'Cerno',
  `"${cerno?.promptVerb} ${cerno?.promptName}"`);
if (cerno?.promptName === 'Cerno') {
  // Cerno speaks through the conversation window now, not the balloon — and
  // the window MUST be closed before moving on, or its Busy pins every
  // prompt after it to empty and the rest of the walk fails in cascade.
  await press('e'); await settle(700);
  check('  he is Cerno of Kaladash, from the book',
    (await page.locator('.aq-talk').count()) > 0 && /Cerno/.test(await text('.aq-talk-name')),
    `"${await text('.aq-talk-name')}"`);
  const cernoTalk = await text('.aq-talk');
  check('  and he speaks of the book\'s matters',
    /Kaladash|panacea|madness|cave|descend|myth/i.test(cernoTalk), `"${cernoTalk.slice(0, 44)}…"`);
  for (let i = 0; i < 3 && (await page.locator('.aq-talk').count()) > 0; i++) {
    if (await page.locator('.aq-talk-close').count()) await page.locator('.aq-talk-close').click().catch(() => { });
    else await press('Escape');
    await settle(300);
  }
}

// ------------------------------------------------------------------
console.log('\n=== the cave mouth in Deepdelve still takes the party down ===');

const shelf = await standAt(54, 16);
check('on the shelf below the mouth, above ground', shelf?.stage === 1, `stage ${shelf?.stage}`);
// The mouth is a doorway now — [E] at the threshold, and the light must
// answer first: the summon die decides whether the descent is granted this
// try or the dark holds the door for a while. Either answer is the game
// working; only silence is a failure.
const atMouth = await standAt(54, 14);
check('the mouth offers the descent', atMouth?.promptVerb === 'Descend',
  `"${atMouth?.promptVerb} ${atMouth?.promptName}"`);
const beforeMouth = ((await page.evaluate(async () => (await import('/js/game.js')).diceSeen())) ?? []).length;
await press('e');
let down = null;
for (let i = 0; i < 30 && !down; i++) {
  await settle(200);
  await page.click('.aq-cutscene-skip', { timeout: 200 }).catch(() => { });
  const h = await hudRaw();
  if (h?.stage === 2) down = h;
}
const mouthRoll = ((await page.evaluate(async () => (await import('/js/game.js')).diceSeen())) ?? [])
  .slice(beforeMouth).find(d => d.kind === 'summon');
check('the light is consulted at the threshold', !!mouthRoll, JSON.stringify(mouthRoll ?? null));
if (down) {
  check('  granted: the party descends to the first depth', down.level === 1, `level ${down.level}`);
  // Back above ground before the doors are walked, or every region load
  // after this reads "The Crystal Cistern" and fails in cascade.
  await page.click('.aq-cutscene-skip', { timeout: 1500 }).catch(() => { });
  await page.evaluate(async () => (await import('/js/game.js')).debugLeaveCave());
  await settle(1200); await clearLevelUp();
} else {
  check('  refused: the dark holds the door, party above ground',
    mouthRoll?.outcome === 'bad' && (await hud())?.stage === 1, `outcome ${mouthRoll?.outcome}`);
}

// ------------------------------------------------------------------
console.log('\n=== the Cleric\'s house, from the wood ===');

await region('r2_whispering_wood'); await settle(900);
const atDoor = await standAt(20, 34);
check('the doorway prompts to enter', atDoor?.promptVerb === 'Enter' && /Cleric/i.test(atDoor?.promptName || ''),
  `"${atDoor?.promptVerb} ${atDoor?.promptName}"`);
await press('e'); await settle(1000);
const inside = await hud();
check('stepping through loads the interior (a separate map)', inside?.stage === 3, `stage ${inside?.stage}`);
await settle(700);
check('the interior keeps running (no camera-clamp freeze)', (await hud())?.stage === 3);

// back out into the WOOD, not the old Stage 1 — the fix that made the ring safe
await region('r2_whispering_wood'); await settle(700);
const father = await standAt(24, 35);
check('Mirka\'s father waits by the house', father?.promptName === "Mirka's Father",
  `"${father?.promptVerb} ${father?.promptName}"`);
if (father?.promptName === "Mirka's Father") {
  await press('e'); await settle(600);
  check('  he opens a conversation window', (await page.locator('.aq-talk').count()) > 0);
  check('  headed with who is speaking', /Father/.test(await text('.aq-talk-name')));
  const topics = await page.locator('.aq-talk-topic').count();
  check('  offering a menu of questions', topics >= 5, `${topics} questions`);
  if (await page.locator('.aq-talk-close').count()) await page.locator('.aq-talk-close').click().catch(() => {});
  await settle(300);
}

// ------------------------------------------------------------------
console.log('\n=== the Academy Outpost, on the Kae Ychel road ===');

await region('r4_kae_ychel_road'); await settle(900);
const atGate = await standAt(53, 23);
check('the Academy has an entrance', atGate?.promptVerb === 'Enter' && /Academy/i.test(atGate?.promptName || ''),
  `"${atGate?.promptVerb} ${atGate?.promptName}"`);
await press('e'); await settle(1000);
const inSchool = await hud();
check('the college hall loads as its own map', inSchool?.stage === 3 && /Academy/i.test(inSchool?.region || ''),
  `stage ${inSchool?.stage}, "${inSchool?.region}"`);

// ------------------------------------------------------------------
console.log('\n=== Seoshe, from Ashwold, and the warehouse within it ===');

await region('r1_ashwold'); await settle(900);
const atCityGate = await standAt(7, 30);
check('the gate of Seoshe can be entered from the village',
  atCityGate?.promptVerb === 'Enter' && /Seoshe/i.test(atCityGate?.promptName || ''),
  `"${atCityGate?.promptVerb} ${atCityGate?.promptName}"`);
await press('e'); await settle(1000);
const inCity = await hud();
check('Seoshe loads as its own city map', inCity?.stage === 3 && /Seoshe/i.test(inCity?.region || ''),
  `stage ${inCity?.stage}, "${inCity?.region}"`);
check('and it is daylight, not a dungeon', inCity?.outdoor === true, `outdoor=${inCity?.outdoor}`);

const atWarehouse = await standAt(43, 33);
check('the burnt warehouse can be entered from the docks',
  atWarehouse?.promptVerb === 'Enter' && /warehouse/i.test(atWarehouse?.promptName || ''),
  `"${atWarehouse?.promptVerb} ${atWarehouse?.promptName}"`);
await press('e'); await settle(1000);
const inHideout = await hud();
check('the hideout loads as its own map (an interior within the city)',
  inHideout?.stage === 3, `stage ${inHideout?.stage}`);
const atHideoutDoor = await standAt(19, 25);
check('the way out returns to the docks',
  atHideoutDoor?.promptVerb === 'Enter' && /docks/i.test(atHideoutDoor?.promptName || ''),
  `"${atHideoutDoor?.promptVerb} ${atHideoutDoor?.promptName}"`);
await press('e'); await settle(1000);
const backInCity = await hud();
check('leaving the warehouse returns to Seoshe, not straight outside',
  backInCity?.stage === 3 && /Seoshe/i.test(backInCity?.region || ''),
  `stage ${backInCity?.stage}, "${backInCity?.region}"`);

// ------------------------------------------------------------------
console.log('\n=== console ===');
const noisy = errors.filter(e => !/favicon|status of 401/.test(e));
check('the game logged no errors', noisy.length === 0, noisy.slice(0, 2).join(' | '));

await browser.close();
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed\n`);
process.exit(failed === 0 ? 0 : 1);
