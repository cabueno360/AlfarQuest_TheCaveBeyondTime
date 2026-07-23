import { chromium } from 'playwright-core';
const CLIENT = 'http://localhost:5223';
const browser = await chromium.launch({ channel: 'chrome', args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1280, height: 800 } });
const errs = [];
page.on('console', m => { if (m.type() === 'error') errs.push(m.text()); });
const settle = ms => page.waitForTimeout(ms);
const warp = (x, y) => page.evaluate(async a => (await import('/js/game.js')).debugWarp(a.x, a.y), { x, y });
const p = { email: `house.${Date.now()}@example.com`, password: 'a-long-enough-passphrase' };
await page.goto(`${CLIENT}/signup`); await settle(2200);
await page.fill('#signup-name', 'House'); await page.fill('#signup-email', p.email);
await page.fill('#signup-password', p.password); await page.fill('#signup-confirm', p.password);
await page.click('button[type=submit]'); await settle(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async () => (await import('/js/game.js')).hudSnapshot() !== null, null, { timeout: 25000 });
await settle(9000);   // deixa os .tmx chegarem
const clearLevel = async () => { for (let i=0;i<4;i++){ await page.click('.aq-levelup-go',{timeout:1200}).catch(()=>{}); await settle(250);
  if (await page.locator('.aq-cw.shown').count()){ await page.keyboard.press('c'); await settle(250);} } };
await clearLevel();

// which floor: arg 'upper' takes the stairs first
const floor = process.argv[2] || 'ground';
// walk to the Cleric's door (20,25) and enter
await warp(20, 25); await settle(500); await clearLevel();
await page.keyboard.press('e'); await settle(1000);   // now inside, ground floor

const shots = floor === 'upper'
  ? [ ['stairs',21,13] ]   // go upstairs first below
  : [ ['g-dining',21,13],['g-prayer',38,12],['g-living',7,13],['g-entrance',21,21],
      ['g-library',7,22],['g-advice',33,21],['g-kitchen',21,5],['g-storage',6,5] ];

if (floor === 'upper') {
  await warp(38, 4); await settle(500); await page.keyboard.press('e'); await settle(1000);  // take the stairs up (38,4)
  const up = [ ['u-mirka',9,7],['u-master',33,7],['u-study',7,16],['u-storage',20,19],['u-balcony',36,16] ];
  for (const [n,x,y] of up) { await warp(x,y); await settle(650); await page.screenshot({ path:`tools/shots/house-${n}.png` }); }
} else {
  for (const [n,x,y] of shots) { await warp(x,y); await settle(600); await page.screenshot({ path:`tools/shots/house-${n}.png` }); }
}
console.log('errors:', errs.filter(e=>/house|obj|atlas|404|Failed/i.test(e)).slice(0,6));
console.log('done', floor);
await browser.close();
