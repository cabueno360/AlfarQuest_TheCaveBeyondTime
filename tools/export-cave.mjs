// Capture the built cave (Stage 2) and write it to disk as JSON.
//
//     node tools/export-cave.mjs
//
// The cave's layout is hand-authored in C# and identical at every depth — only
// its population changes — so one capture describes every level.
import { chromium } from 'playwright-core';
import { writeFileSync, mkdirSync } from 'node:fs';
const CLIENT = process.env.CLIENT ?? 'http://localhost:5223';
mkdirSync('tools/refs', { recursive: true });
const b = await chromium.launch({ channel: 'chrome', args: ['--no-sandbox'] });
const page = await b.newPage({ viewport: { width: 1280, height: 800 } });
const s = ms => page.waitForTimeout(ms);
const p = { email: `cave.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };
await page.goto(`${CLIENT}/signup`); await s(2200);
await page.fill('#signup-name', 'Cave Exporter'); await page.fill('#signup-email', p.email);
await page.fill('#signup-password', p.password); await page.fill('#signup-confirm', p.password);
await page.click('button[type=submit]'); await s(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await s(1500);
await page.evaluate(async () => (await import('/js/game.js')).debugEnterCave());
await s(1200);
const w = await page.evaluate(async () => (await import('/js/game.js')).debugExportMap());
if (!w || !w.tiles) { console.error('export failed'); await b.close(); process.exit(1); }
writeFileSync('tools/refs/cave.json', JSON.stringify(w));
const n = o => (o ?? []).length;
console.log(`cave: ${w.cols}x${w.rows}  props ${n(w.props)}  arches ${n(w.arches)}  containers ${n(w.containers)}  discoveries ${n(w.discoveries)}`);
console.log('wrote tools/refs/cave.json');
await b.close();
