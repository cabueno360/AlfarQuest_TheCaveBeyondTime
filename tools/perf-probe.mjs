// Where the frame time goes. Answers "the game feels slow" with numbers.
//
//     node tools/perf-probe.mjs
import { chromium } from 'playwright-core';
const C = process.env.CLIENT ?? 'http://localhost:5223';
const b = await chromium.launch({ channel: 'chrome', args: ['--no-sandbox'] });
const p = await b.newPage({ viewport: { width: 1280, height: 800 } });
const s = ms => p.waitForTimeout(ms);
const u = { email: `perf.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };
await p.goto(`${C}/signup`); await s(2200);
await p.fill('#signup-name', 'Perf'); await p.fill('#signup-email', u.email);
await p.fill('#signup-password', u.password); await p.fill('#signup-confirm', u.password);
await p.click('button[type=submit]'); await s(2500);
await p.goto(`${C}/play`);
await p.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });

const read = async () => p.evaluate(async () => (await import('/js/game.js')).perfSnapshot());
const walk = async (ms) => {                    // moving, so culling and sorting are honest
  await p.keyboard.down('d'); await s(ms); await p.keyboard.up('d');
};
await s(4000);  console.log('before maps land :', await read());
await s(12000); console.log('after maps land  :', await read());
await walk(3000); console.log('while walking    :', await read());
const place = process.argv[2];
if (place) {
  await p.evaluate(async r => (await import('/js/game.js')).debugLoadRegion(r), place);
  await s(2500); await walk(3000);
  console.log(`in ${place}`.padEnd(17) + ':', await read());
}
await b.close();
