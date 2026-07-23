// Walk a hand-authored region and photograph it.
//
//     node tools/shot-region.mjs r1_ashwold
//
// The map preview shows only the tile layers. The village's props, its people
// and its light come from the engine, so the only honest look at a region is
// the game drawing it.
import { chromium } from 'playwright-core';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5223';
const REGION = process.argv[2] ?? 'r1_ashwold';

const browser = await chromium.launch({ channel: 'chrome', args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
const errs = [];
page.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
const settle = ms => page.waitForTimeout(ms);
const warp = (x, y) => page.evaluate(async a => (await import('/js/game.js')).debugWarp(a.x, a.y), { x, y });

const p = { email: `region.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };
await page.goto(`${CLIENT}/signup`); await settle(2200);
await page.fill('#signup-name', 'Region'); await page.fill('#signup-email', p.email);
await page.fill('#signup-password', p.password); await page.fill('#signup-confirm', p.password);
await page.click('button[type=submit]'); await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(13000);            // every .tmx has to land before the region is stood up

// Walking a region earns XP from its discoveries, so the level-up card comes up
// mid-tour and photographs itself instead of the place. Cleared at every stop,
// not just at the start.
const clearLevel = async () => {
  for (let i = 0; i < 4; i++) {
    await page.click('.aq-levelup-go', { timeout: 900 }).catch(() => {});
    await settle(220);
    if (await page.locator('.aq-cw.shown').count()) { await page.keyboard.press('c'); await settle(220); }
  }
};
await clearLevel();

const ok = await page.evaluate(async r => (await import('/js/game.js')).debugLoadRegion(r), REGION);
console.log('region stood up:', ok);
await settle(1500);

// The player's own line through the place, in order, plus the corners of it.
const TOURS = {
  r1_ashwold: [
    ['01-arrival', 11, 30], ['02-bridge', 19, 30], ['03-mill', 27, 24],
    ['04-smithy', 26, 29], ['05-street', 32, 30], ['06-square', 38, 31],
    ['07-inn', 40, 27], ['08-backlane', 36, 38], ['09-fields', 52, 46],
    ['10-minroad', 37, 22], ['11-ford', 37, 17], ['12-shrine', 33, 11],
    ['13-graves', 30, 9], ['14-woodyard', 29, 14], ['15-eastroad', 56, 33],
    ['16-westwood', 8, 22], ['17-landing', 17, 35], ['18-gate', 7, 30],
  ],
  r2_whispering_wood: [
    ['01-fromvillage', 36, 52], ['02-roadclimb', 36, 44], ['03-valeturn', 34, 34],
    ['04-footbridge', 29, 33], ['05-vale', 20, 33], ['06-clerichouse', 19, 36],
    ['07-chapel', 16, 34], ['08-valegraves', 14, 32], ['09-ford', 36, 18],
    ['10-cross', 37, 23], ['11-mountainroad', 52, 13], ['12-delvercamp', 52, 20],
    ['13-burn', 48, 42], ['14-kilns', 46, 41], ['15-deepwood', 12, 16],
    ['16-hollow', 11, 47], ['17-highseat', 20, 15], ['18-eastedge', 68, 13],
  ],
  r3_deepdelve: [
    ['01-pass', 4, 13], ['02-cleft', 9, 13], ['03-passout', 15, 16],
    ['04-bowl', 26, 24], ['05-pitfloor', 34, 27], ['06-pithead', 40, 24],
    ['07-office', 34, 30], ['08-bunkhouse', 29, 24], ['09-tarn', 24, 33],
    ['10-pump', 26, 37], ['11-tips', 33, 39], ['12-southtrack', 31, 48],
    ['13-switchback', 45, 24], ['14-turn', 45, 18], ['15-shelf', 50, 14],
    ['16-mouth', 54, 14], ['17-cerno', 52, 14], ['18-lastwatch', 56, 14],
  ],
  r4_kae_ychel_road: [
    ['01-roadin', 4, 32], ['02-brook', 10, 12], ['03-oasis', 22, 24],
    ['04-caravan', 29, 29], ['05-wayhouse', 30, 30], ['06-well', 22, 25],
    ['07-trackjoin', 33, 26], ['08-trackhead', 32, 10], ['09-academyspur', 52, 27],
    ['10-outpost', 54, 22], ['11-courtyard', 52, 21], ['12-hall', 49, 20],
    ['13-drynorth', 42, 12], ['14-ruins', 12, 50], ['15-colonnade', 15, 49],
    ['16-drysoutheast', 52, 44], ['17-stoppedwagon', 44, 40], ['18-eastwaymark', 66, 30],
  ],
};
const SPOTS = TOURS[REGION] ?? TOURS.r1_ashwold;
for (const [name, x, y] of SPOTS) {
  await warp(x, y); await settle(500);
  await clearLevel();
  await settle(350);
  await page.screenshot({ path: `tools/shots/region-${REGION}-${name}.png` });
}
const hud = await page.evaluate(async () => (await import('/js/game.js')).hudSnapshot());
console.log('region name in hud:', hud?.region, ' safe:', hud?.inSafeZone);
console.log('console errors:', errs.slice(0, 5));
await browser.close();
