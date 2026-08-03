// One question: does coming DOWN the Cleric's stair land at the stair's foot
// (tile ~38,5) rather than the front door (tile ~21,23)?
import { chromium } from 'playwright-core';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5173';
const browser = await chromium.launch({ channel: 'chrome', args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
const settle = (ms = 500) => page.waitForTimeout(ms);
const hud = () => page.evaluate(async () => (await import('/js/game.js')).hudSnapshot());
const warp = (x, y) => page.evaluate(async a => (await import('/js/game.js')).debugWarp(a.x, a.y), { x, y });
const clearLevelUp = async () => {
  for (let i = 0; i < 4 && (await page.locator('.aq-levelup').count()) > 0; i++) {
    await page.click('.aq-levelup-go', { timeout: 3000 }).catch(() => { });
    await settle(500);
    if ((await page.locator('.aq-cw.shown').count()) > 0) { await page.keyboard.press('c'); await settle(400); }
  }
};

await page.goto(`${CLIENT}/signup`);
await settle(2200);
await page.fill('#signup-name', 'Stair Tester');
await page.fill('#signup-email', `stair.${Date.now()}@example.com`);
await page.fill('#signup-password', 'a-long-enough-passphrase');
await page.fill('#signup-confirm', 'a-long-enough-passphrase');
await page.click('button[type=submit]');
await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(5000);
await clearLevelUp();

await page.evaluate(async () => (await import('/js/game.js')).debugLoadRegion('r2_whispering_wood'));
await settle(1500); await clearLevelUp();

const step = async (label) => {
  const h = await hud();
  console.log(`${label}: tile (${h?.heroTx}, ${h?.heroTy}) prompt "${h?.promptVerb} ${h?.promptName}"`);
  return h;
};

await warp(20, 33); await settle(500); await clearLevelUp();
await step('at the house door');
await page.keyboard.press('e'); await settle(1500); await clearLevelUp();
const inside = await step('inside (ground)');

await warp(38, 5); await settle(500); await clearLevelUp();
await step('at the stair');
await page.keyboard.press('e'); await settle(1500); await clearLevelUp();
const upper = await step('upper floor');

await page.keyboard.press('e'); await settle(1500); await clearLevelUp();
const down = await step('back on the ground floor');

const nearStair = Math.abs((down?.heroTx ?? 0) - 38.5) <= 3 && Math.abs((down?.heroTy ?? 0) - 5) <= 3;
const upperNearStair = Math.abs((upper?.heroTx ?? 0) - 21.5) <= 3 && Math.abs((upper?.heroTy ?? 0) - 14) <= 3;
console.log(upperNearStair ? 'ok    going UP lands at the upper stair' : ` FAIL  going up landed at (${upper?.heroTx}, ${upper?.heroTy}), expected ~ (21.5, 14)`);
console.log(nearStair ? 'ok    coming DOWN lands at the stair foot' : ` FAIL  came down at (${down?.heroTx}, ${down?.heroTy}), expected ~ (38.5, 5) — front door is (21.5, 23.5)`);
await browser.close();
process.exit(nearStair && upperNearStair ? 0 : 1);
