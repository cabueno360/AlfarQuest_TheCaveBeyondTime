// Look at the three places that were just migrated to Tiled.
//
//     node tools/shot-places.mjs
//
// Loads each interior through the debug seam and takes a picture at a few
// spots, so the map that Tiled writes and the map the game draws can be
// compared without walking there.
import { chromium } from 'playwright-core';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5223';
const browser = await chromium.launch({ channel: 'chrome', args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
const errs = [];
page.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
const settle = ms => page.waitForTimeout(ms);
const warp = (x, y) => page.evaluate(async a => (await import('/js/game.js')).debugWarp(a.x, a.y), { x, y });
const load = id => page.evaluate(async i => (await import('/js/game.js')).debugLoadInterior(i), id);

const p = { email: `places.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };
await page.goto(`${CLIENT}/signup`); await settle(2200);
await page.fill('#signup-name', 'Places'); await page.fill('#signup-email', p.email);
await page.fill('#signup-password', p.password); await page.fill('#signup-confirm', p.password);
await page.click('button[type=submit]'); await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(12000);            // let every .tmx land

for (let i = 0; i < 4; i++) {
  await page.click('.aq-levelup-go', { timeout: 1200 }).catch(() => {});
  await settle(250);
  if (await page.locator('.aq-cw.shown').count()) { await page.keyboard.press('c'); await settle(250); }
}

const PLACES = [
  ['mage_school', [['hall', 22, 12], ['library', 7, 8], ['circle', 36, 12]]],
  ['seoshe', [['gate', 27, 35], ['square', 28, 21], ['hill', 28, 8], ['docks', 45, 22]]],
  ['thieves_warehouse', [['floor', 19, 20], ['office', 8, 6], ['pit', 30, 20]]],
];

for (const [id, spots] of PLACES) {
  await load(id); await settle(1200);
  for (const [name, x, y] of spots) {
    await warp(x, y); await settle(700);
    await page.screenshot({ path: `tools/shots/place-${id}-${name}.png` });
  }
  console.log(id, 'ok');
}

console.log('console errors:', errs.slice(0, 6));
await browser.close();
