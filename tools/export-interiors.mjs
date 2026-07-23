// Capture the built Cleric's-house interiors and write them to disk as JSON.
//
//     node tools/export-interiors.mjs
//
// Same idea as export-stage01: the interiors are built by C# rather than authored,
// so the only faithful way to move them into Tiled is to run their builders once
// and write down what they produced. tools/make-house-tmx.py turns this into the
// .tmx files.

import { chromium } from 'playwright-core';
import { writeFileSync, mkdirSync } from 'node:fs';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5223';
mkdirSync('tools/refs', { recursive: true });

const FLOORS = ['cleric_house', 'cleric_house_upper'];

const browser = await chromium.launch({ channel: 'chrome', args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
const settle = ms => page.waitForTimeout(ms);

const player = { email: `interior.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };
await page.goto(`${CLIENT}/signup`); await settle(2200);
await page.fill('#signup-name', 'Interior Exporter');
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]'); await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(1500);

const out = {};
for (const id of FLOORS) {
  await page.evaluate(async (i) => (await import('/js/game.js')).debugLoadInterior(i), id);
  await settle(700);
  const w = await page.evaluate(async () => (await import('/js/game.js')).debugExportMap());
  if (!w || !w.tiles) { console.error(`export failed for ${id}`); continue; }
  out[id] = w;
  const n = o => (o ?? []).length;
  console.log(`${id}: ${w.cols}x${w.rows}  props ${n(w.props)}  npcs ${n(w.npcs)}  ` +
              `portals ${n(w.portals)}  examinables ${n(w.examinables)}`);
}

writeFileSync('tools/refs/cleric-house.json', JSON.stringify(out));
console.log('wrote tools/refs/cleric-house.json');
if (errors.length) console.log('console errors:', errors.slice(0, 3));
await browser.close();
