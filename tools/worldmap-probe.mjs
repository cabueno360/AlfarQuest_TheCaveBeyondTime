// The expanded Stage 1 world & the map-transition system, played rather than
// inspected.
//
//     node tools/worldmap-probe.mjs
//
// What this exists to catch is a doorway that goes nowhere, or a house you cannot
// get back out of. It stands the hero at the Cleric's door, steps inside (a whole
// separate map), and steps back out onto the same doorstep — and checks the
// canonical NPCs from the book are where the lore puts them.

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
/// The HUD, waited for rather than sampled. hudSnapshot() answers null while the
/// world is paused — which it is behind a level-up card and behind the character
/// sheet the card hands off to — so a bare read taken a beat too early reports
/// "stage undefined" and takes three checks down with it, for no reason but the
/// clock. Every read retries for a second before giving up.
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
const markersRaw = () => page.evaluate(async () => (await import('/js/game.js')).npcMarkers());
const markers = async () => {
  for (let i = 0; i < 10; i++) {
    const m = await markersRaw();
    if (m && m.length) return m;
    await settle(120);
  }
  return (await markersRaw()) ?? [];
};
const press = k => page.keyboard.press(k);

const clearLevelUp = async () => {
  // Dismiss the popup, then close the character sheet it hands off to — both pause
  // the world, and a sheet left open freezes every prompt after it.
  for (let i = 0; i < 4 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 3000 }).catch(() => { });
    await settle(450);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(400); }
  }
  if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(350); }
};

/// Warp beside a tile, dismiss any level-up that popped (they pause the world and
/// freeze the prompt), let a frame recompute what's in reach, and read the HUD.
const standAt = async (x, y) => {
  await warp(x, y); await settle(250);
  await clearLevelUp();
  await warp(x, y); await settle(300);   // re-place in case the hand-off moved focus
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
// Wait for the world to actually BE there rather than for a guess at how long
// that takes. Eight .tmx files are fetched at startup now, and a fixed sleep
// that was long enough for four reads the world mid-build: stage undefined, no
// villagers, and three checks failing for no reason but the clock.
await page.waitForFunction(async () => {
  const g = await import('/js/game.js');
  const h = g.hudSnapshot();
  return h && h.stage === 1 && (h.party?.length ?? 0) > 0;
}, null, { timeout: 30000 });
await settle(1200);
await clearLevelUp();

const start = await hud();
check('the world loads on the overworld', start?.stage === 1, `stage ${start?.stage}`);

// ------------------------------------------------------------------
console.log('\n=== the world is four times larger ===');

const marks = await markers();
const maxX = Math.max(...marks.map(n => n.x || 0));
const maxY = Math.max(...marks.map(n => n.y || 0));
// Tile 116 east = ~3712px; tile 107 south = ~3424px. The old map ended near 2560.
check('the map stretches far to the east (toward Kae Ychel)', maxX > 3400, `furthest NPC at x=${Math.round(maxX)}`);
check('and far to the south (toward Seoshe)', maxY > 3300, `furthest NPC at y=${Math.round(maxY)}`);

// Reaching those places at all proves the ground between is walkable, not void.
const caravan = await standAt(116, 56);   // the caravan on the east road
check('a merchant keeps the caravan on the Kae Ychel road', /Sella|Trade/i.test(`${caravan?.promptVerb} ${caravan?.promptName}`),
  `"${caravan?.promptVerb} ${caravan?.promptName}"`);
await page.screenshot({ path: 'tools/shots/world-4-east.png' });

const steps = await standAt(48, 108);     // the fishing steps on the coast road
check('a fisher works the coast road toward Seoshe', /Old Nets|Fisher|Talk/i.test(`${steps?.promptVerb} ${steps?.promptName}`),
  `"${steps?.promptVerb} ${steps?.promptName}"`);
await press('e'); await settle(400);
const netsTalk = await hud();
check('the fisher speaks of Seoshe and the Neruum flotilla', /Seoshe|Neruum|coast|crescent/i.test(netsTalk?.talkLine || ''),
  `"${(netsTalk?.talkLine || '').slice(0, 46)}…"`);
await press('e'); await settle(300);
await page.screenshot({ path: 'tools/shots/world-5-south.png' });

// Back to the spawn camp so the rest of the probe runs from the old core.
await warp(13, 70); await settle(400);

// ------------------------------------------------------------------
console.log('\n=== the canonical NPCs are where the lore puts them ===');

const cerno = await standAt(43, 13);   // the cave forecourt
check('walking up to the cave guide offers to talk', cerno?.promptName === 'Cerno',
  `"${cerno?.promptVerb} ${cerno?.promptName}"`);
await press('e'); await settle(400);
const cernoTalk = await hud();
check('the guide is Cerno of Kaladash, from the book', cernoTalk?.talkName === 'Cerno' && /Kaladash/.test(cernoTalk?.talkRole || ''),
  `${cernoTalk?.talkName}, ${cernoTalk?.talkRole}`);
check('Cerno speaks the story\'s warning', /panacea|madness|cave/i.test(cernoTalk?.talkLine || ''),
  `"${(cernoTalk?.talkLine || '').slice(0, 48)}…"`);
await press('e'); await settle(300);   // close the talk

const father = await standAt(23, 26);  // the path below the Cleric's house
check('Mirka\'s father waits below the house', father?.promptName === "Mirka's Father",
  `"${father?.promptVerb} ${father?.promptName}"`);
await press('e'); await settle(500);
// He answers a menu of questions now rather than a one-line balloon — the
// concept art's "ideas of conversation". See tools/dialogue-probe.mjs.
const greeting = (await page.locator('.aq-talk-greeting').textContent().catch(() => '')) ?? '';
check('he opens a conversation, greeting you first', /delvers|asleep|ask/i.test(greeting),
  `"${greeting.trim().slice(0, 48)}…"`);
check('every question from the concept is on offer',
  await page.locator('.aq-talk-topic').count() === 11);
await page.click('.aq-talk-leave').catch(() => { });
await settle(400);

// ------------------------------------------------------------------
console.log('\n=== the door opens onto a separate map ===');

const atDoor = await standAt(20, 25);  // the Cleric's doorstep
check('the doorway prompts to enter', atDoor?.promptVerb === 'Enter' && /Cleric/i.test(atDoor?.promptName || ''),
  `"${atDoor?.promptVerb} ${atDoor?.promptName}"`);
await page.screenshot({ path: 'tools/shots/world-1-door.png' });

await press('e'); await settle(800);
const inside = await hud();
check('stepping through loads the interior (a new map)', inside?.stage === 3, `stage ${inside?.stage}`);
// The bug this replaced froze the loop the instant you entered; prove it still ticks.
await settle(500);
const stillTicking = await hud();
check('the interior keeps running (no camera-clamp freeze)', stillTicking?.stage === 3);
await page.screenshot({ path: 'tools/shots/world-2-ground-floor.png' });

// -- examining an object: his empty armour stand, by the door --
const atStand = await standAt(17, 20);
check('an object can be examined', atStand?.promptVerb === 'Examine' && /armour stand/i.test(atStand?.promptName || ''),
  `"${atStand?.promptVerb} ${atStand?.promptName}"`);
await press('e'); await settle(500);
const readOpen = await page.locator('.aq-read').count();
check('examining opens the reading panel', readOpen > 0);
check('and reads the story of what he took with him',
  /empty|plate|cave/i.test(await page.textContent('.aq-read-body').catch(() => '')));
await page.keyboard.press('Escape'); await settle(400);
check('closing the panel returns to the house', (await page.locator('.aq-read').count()) === 0);

// -- the prayer corner: the shrine's breviaries, one of the design's named objects
const atBooks = await standAt(40, 11.5);
check('the prayer books lie at the shrine rail', /prayer books/i.test(atBooks?.promptName || ''),
  `"${atBooks?.promptVerb} ${atBooks?.promptName}"`);
await press('e'); await settle(500);
check('and they read as his, in the journal\'s hand',
  /breviar|office for the sick|listening/i.test(await page.textContent('.aq-read-body').catch(() => '')));
await page.keyboard.press('Escape'); await settle(400);

// ------------------------------------------------------------------
console.log('\n=== up the stairs to Mirka, and back down ===');

const atStair = await standAt(38, 5);  // the stairway, ground floor
check('the stairway prompts to go up', atStair?.promptVerb === 'Go' && /up/i.test(atStair?.promptName || ''),
  `"${atStair?.promptVerb} ${atStair?.promptName}"`);
await press('e'); await settle(800);
const upstairs = await hud();
check('the upper floor loads', upstairs?.stage === 3);

// -- the Cleric's journal, on Mirka's nightstand: the chief lore source --
const atJournal = await standAt(8, 5);
check('the journal can be read', atJournal?.promptVerb === 'Read' && /journal/i.test(atJournal?.promptName || ''),
  `"${atJournal?.promptVerb} ${atJournal?.promptName}"`);
await press('e'); await settle(500);
check('the journal opens as a book of pages', (await page.locator('.aq-read.journal').count()) > 0);
check('it speaks in the Cleric\'s own hand, from the book',
  /Cerno|panacea|Mirka|wasting|damnation/i.test(await page.textContent('.aq-read-body').catch(() => '')));
const turns = await page.locator('.aq-read-turn').count();
check('and it has pages to turn', turns >= 2, `${turns} page controls`);
await page.locator('.aq-read-turn', { hasText: 'Next' }).click().catch(() => { });
await settle(300);
await page.screenshot({ path: 'tools/shots/world-6-journal.png' });
await page.keyboard.press('Escape'); await settle(400);
check('the journal closes', (await page.locator('.aq-read').count()) === 0);

const atMirka = await standAt(6, 5);   // her sickbed
check('Mirka is in her room', atMirka?.promptName === 'Mirka', `"${atMirka?.promptVerb} ${atMirka?.promptName}"`);
await press('e'); await settle(400);
const mirkaTalk = await hud();
check('watching over Mirka reads from the book', /wasting|panacea|sleeps|journal/i.test(mirkaTalk?.talkLine || ''),
  `"${(mirkaTalk?.talkLine || '').slice(0, 46)}…"`);
await press('e'); await settle(300);
await page.screenshot({ path: 'tools/shots/world-3-mirka.png' });

const atDown = await standAt(21, 14); // the stairs down
check('the stairs down prompt to descend', atDown?.promptVerb === 'Go' && /down/i.test(atDown?.promptName || ''),
  `"${atDown?.promptVerb} ${atDown?.promptName}"`);
await press('e'); await settle(800);
check('back on the ground floor', (await hud())?.stage === 3);

// ------------------------------------------------------------------
console.log('\n=== and back out the front door onto the same doorstep ===');

const atExit = await standAt(21, 24);  // just inside the front door
check('the front door prompts to leave', atExit?.promptVerb === 'Step',
  `"${atExit?.promptVerb} ${atExit?.promptName}"`);
await press('e'); await settle(800);
const backOut = await hud();
check('leaving returns to the overworld', backOut?.stage === 1, `stage ${backOut?.stage}`);

const again = await standAt(20, 25);
check('the doorway is still there, and still enterable', again?.promptVerb === 'Enter' && /Cleric/i.test(again?.promptName || ''),
  `"${again?.promptVerb} ${again?.promptName}"`);

// ------------------------------------------------------------------
console.log('\n=== the Academy outpost, on the road to Kae Ychel ===');

const atStudent = await standAt(92, 49);   // an apprentice in the courtyard
check('apprentices study in the courtyard', atStudent?.promptName === 'An Apprentice',
  `"${atStudent?.promptVerb} ${atStudent?.promptName}"`);
await press('e'); await settle(400);
check('an apprentice speaks of the Academy, from the book',
  /ward|array|Evoker|Thami|Gersimo|scrying/i.test((await hud())?.talkLine || ''));
await press('e'); await settle(300);

const atGate = await standAt(96, 43);      // the grand doorway
check('the Academy has an entrance', atGate?.promptVerb === 'Enter' && /Academy/i.test(atGate?.promptName || ''),
  `"${atGate?.promptVerb} ${atGate?.promptName}"`);
await page.screenshot({ path: 'tools/shots/world-7-academy.png' });
await press('e'); await settle(800);
const inSchool = await hud();
check('the college hall loads as its own map', inSchool?.stage === 3 && /Academy/i.test(inSchool?.region || ''),
  `stage ${inSchool?.stage}, ${inSchool?.region}`);

const atMaster = await standAt(19, 7);     // the grandmaster at the lectern
check('a grandmaster teaches here', atMaster?.promptName === 'The Grandmaster',
  `"${atMaster?.promptVerb} ${atMaster?.promptName}"`);
await press('e'); await settle(400);
check('the grandmaster speaks the Mage\'s own history, from the book',
  /Kae Ychel|boon|demon|banish|tests/i.test((await hud())?.talkLine || ''));
await press('e'); await settle(300);

const atMemorial = await standAt(35, 17);   // the memorial to the lost apprentices
check('the memorial can be read', atMemorial?.promptVerb === 'Read' && /memorial/i.test(atMemorial?.promptName || ''),
  `"${atMemorial?.promptVerb} ${atMemorial?.promptName}"`);
await press('e'); await settle(500);
check('it names the apprentices lost in the Mage\'s ritual',
  /Bruelos|Gersimo|Thami|demon|banish/i.test(await page.textContent('.aq-read-body').catch(() => '')));
await page.keyboard.press('Escape'); await settle(400);

await standAt(21, 20);                       // back to the entrance
const atOut = await hud();
check('the way out is the courtyard', atOut?.promptVerb === 'Step' && /courtyard/i.test(atOut?.promptName || ''),
  `"${atOut?.promptVerb} ${atOut?.promptName}"`);
await press('e'); await settle(700);
check('leaving the Academy returns to the overworld', (await hud())?.stage === 1);

// ------------------------------------------------------------------
console.log('\n=== Seoshe, the coastal city ===');

const atWatch = await standAt(28, 78);     // a guard at the gate
check('the gate is guarded', atWatch?.promptName === 'A Harbour Watchman',
  `"${atWatch?.promptVerb} ${atWatch?.promptName}"`);

const atCityGate = await standAt(31, 76);  // the gate itself
check('the city gate can be entered', atCityGate?.promptVerb === 'Enter' && /Seoshe/i.test(atCityGate?.promptName || ''),
  `"${atCityGate?.promptVerb} ${atCityGate?.promptName}"`);
await press('e'); await settle(900);
const inCity = await hud();
check('Seoshe loads as its own city map', inCity?.stage === 3 && /Seoshe/i.test(inCity?.region || ''),
  `stage ${inCity?.stage}, ${inCity?.region}`);
check('and it is daylight, not a dungeon', inCity?.outdoor === true, `outdoor=${inCity?.outdoor}`);
await standAt(28, 30);   // stand in the square for the shot
await page.screenshot({ path: 'tools/shots/world-8-seoshe.png' });

const atMarket = await standAt(34, 26);     // the Neruum trader in the square
check('a merchant keeps the market', /Trade with|Neruum/i.test(`${atMarket?.promptVerb} ${atMarket?.promptName}`),
  `"${atMarket?.promptVerb} ${atMarket?.promptName}"`);
await press('e'); await settle(600);
check('the market merchant opens a shop', (await page.locator('.aq-shop').count()) > 0);
await page.keyboard.press('Escape'); await settle(400);

// ------------------------------------------------------------------
console.log('\n=== the Thieves\' hideout — the burnt warehouse ===');

const atWarehouse = await standAt(44, 33);  // the burnt warehouse door, Low Docks
check('the burnt warehouse can be entered from the docks',
  atWarehouse?.promptVerb === 'Enter' && /warehouse/i.test(atWarehouse?.promptName || ''),
  `"${atWarehouse?.promptVerb} ${atWarehouse?.promptName}"`);
await press('e'); await settle(900);
const inHideout = await hud();
check('the hideout loads as its own map (an interior within the city)',
  inHideout?.stage === 3 && /Warehouse/i.test(inHideout?.region || ''),
  `stage ${inHideout?.stage}, ${inHideout?.region}`);
await page.screenshot({ path: 'tools/shots/world-9-hideout.png' });

const atSite = await standAt(23, 14);       // the fused-glass spot where it happened
check('the crystal\'s work can be examined', atSite?.promptVerb === 'Examine' && /where it happened/i.test(atSite?.promptName || ''),
  `"${atSite?.promptVerb} ${atSite?.promptName}"`);
await press('e'); await settle(500);
check('it tells of the crew the crystal took, from the book',
  /Kas|Essil|Yash|silver-blue|cave/i.test(await page.textContent('.aq-read-body').catch(() => '')));
await page.keyboard.press('Escape'); await settle(400);

const atTunnel = await standAt(4, 8);       // the twins' tunnel, back room corner
check('the twins\' tunnel is in the corner', atTunnel?.promptVerb === 'Examine' && /tunnel/i.test(atTunnel?.promptName || ''),
  `"${atTunnel?.promptVerb} ${atTunnel?.promptName}"`);

const atHideoutDoor = await standAt(19, 24); // the way back out
check('the way out returns to the docks', atHideoutDoor?.promptVerb === 'Enter' && /docks/i.test(atHideoutDoor?.promptName || ''),
  `"${atHideoutDoor?.promptVerb} ${atHideoutDoor?.promptName}"`);
await press('e'); await settle(900);
const backInCity = await hud();
check('leaving the warehouse returns to Seoshe, not straight outside',
  backInCity?.stage === 3 && /Seoshe/i.test(backInCity?.region || ''),
  `stage ${backInCity?.stage}, ${backInCity?.region}`);

await standAt(27, 36);                        // back to the city gate
const cityOut = await hud();
check('the way out of the city is the gate', cityOut?.promptVerb === 'Step' && /gate/i.test(cityOut?.promptName || ''),
  `"${cityOut?.promptVerb} ${cityOut?.promptName}"`);
await press('e'); await settle(700);
check('leaving Seoshe returns to the overworld', (await hud())?.stage === 1);

// ------------------------------------------------------------------
console.log('\n=== console ===');
const noisy = errors.filter(e => !/favicon|status of 401/.test(e));
check('the game logged no errors', noisy.length === 0, noisy.slice(0, 2).join(' | '));

await browser.close();
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed\n`);
process.exit(failed === 0 ? 0 : 1);
