// Capture the built Stage 1 world and write it to disk as JSON.
//
//     node tools/export-stage01.mjs
//
// Stage 1 is generated from a fixed seed (Random(42)) rather than authored, so the
// only faithful way to "use the current layout exactly" is to run the generator
// once and write down what it produced. tools/make-tmx.py then turns this into
// Maps/Outside/Stage01_Outside.tmx.
//
// Re-run this only when you intend to re-baseline the map from the generator —
// once Stage 1 is authored in Tiled, the .tmx is the source of truth, not this.

import { chromium } from 'playwright-core';
import { writeFileSync, mkdirSync } from 'node:fs';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5223';
const OUT = 'tools/refs/stage01-world.json';
mkdirSync('tools/refs', { recursive: true });

const browser = await chromium.launch({ channel: 'chrome', args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
const errors = [];
page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
const settle = ms => page.waitForTimeout(ms);

const player = { email: `export.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };
await page.goto(`${CLIENT}/signup`); await settle(2200);
await page.fill('#signup-name', 'Exporter');
await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.click('button[type=submit]'); await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(1500);

const world = await page.evaluate(async () => (await import('/js/game.js')).debugExportMap());
if (!world || !world.tiles) {
  console.error('export failed — no world returned', errors.slice(0, 3));
  await browser.close();
  process.exit(1);
}

writeFileSync(OUT, JSON.stringify(world));
const n = o => (o ?? []).length;
console.log(`wrote ${OUT}`);
console.log(`  ${world.cols}x${world.rows} tiles @${world.tile}px  "${world.name}"`);
console.log(`  props ${n(world.props)}  npcs ${n(world.npcs)}  portals ${n(world.portals)}`);
console.log(`  examinables ${n(world.examinables)}  containers ${n(world.containers)}  discoveries ${n(world.discoveries)}`);
console.log(`  creatures ${n(world.creatures)}  arches ${n(world.arches)}  safeZones ${n(world.safeZones)}`);
await browser.close();
