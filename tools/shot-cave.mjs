import { chromium } from 'playwright-core';
const CLIENT='http://localhost:5223';
const b=await chromium.launch({channel:'chrome',args:['--no-sandbox']});
const page=await b.newPage({viewport:{width:1280,height:800}});
const errs=[]; page.on('console',m=>{if(m.type()==='error')errs.push(m.text());});
page.on('pageerror',e=>errs.push(String(e)));
const s=ms=>page.waitForTimeout(ms);
const hud=()=>page.evaluate(async()=>(await import('/js/game.js')).hudSnapshot());
const warp=(x,y)=>page.evaluate(async a=>(await import('/js/game.js')).debugWarp(a.x,a.y),{x,y});
const p={email:`cv.${Date.now()}@example.com`,password:'a-long-enough-passphrase'};
await page.goto(`${CLIENT}/signup`);await s(2200);
await page.fill('#signup-name','Cave Shot');await page.fill('#signup-email',p.email);
await page.fill('#signup-password',p.password);await page.fill('#signup-confirm',p.password);
await page.click('button[type=submit]');await s(2500);
await page.goto(`${CLIENT}/play`);
await page.waitForFunction(async()=>(await import('/js/game.js')).hudSnapshot()!==null,null,{timeout:25000});
await s(9000);
for(let i=0;i<4;i++){await page.click('.aq-levelup-go',{timeout:1200}).catch(()=>{});await s(300);
  if(await page.locator('.aq-cw.shown').count()){await page.keyboard.press('c');await s(300);}}
await page.evaluate(async()=>(await import('/js/game.js')).debugEnterCave());
await s(2500);
const h=await hud();
console.log('stage', h?.stage, '/', h?.region);
for (const [n,x,y] of [['entrance',44,49],['crystal',22,11],['lake',44,33],['mine',16,45]]) {
  await warp(x,y); await s(900); await page.screenshot({path:`tools/shots/cave-${n}.png`});
}
console.log('errors:', errs.slice(0,4));
await b.close();
