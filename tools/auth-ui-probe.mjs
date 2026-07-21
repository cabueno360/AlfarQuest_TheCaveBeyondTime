// Drives the real UI in headless Chrome: sign up, sign out, sign in, the guard,
// the player menu, the profile page, an avatar upload.
//
//     node tools/auth-ui-probe.mjs
//
// The point of doing this in a browser rather than against the API is that the
// interesting failures live in the wiring — a route that renders for an
// anonymous visitor, a token that never reaches the request, a portrait that
// does not refresh. None of those are visible from curl.

import { chromium } from 'playwright-core';
import { writeFileSync, mkdirSync } from 'node:fs';

const CLIENT = process.env.CLIENT ?? 'http://localhost:5223';
const SHOTS = 'tools/shots';
mkdirSync(SHOTS, { recursive: true });

let passed = 0, failed = 0;
const check = (name, ok, detail = '') => {
  console.log(`${ok ? '  ok  ' : ' FAIL '} ${name}${detail ? ` — ${detail}` : ''}`);
  ok ? passed++ : failed++;
};

const browser = await chromium.launch({
  channel: 'chrome',
  args: ['--no-sandbox'],
});
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });

// Anything the app logs to the console is worth seeing: a Blazor exception shows
// up there long before it shows up on screen.
const consoleErrors = [];
page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
page.on('pageerror', e => consoleErrors.push(String(e)));

const settle = async (ms = 900) => page.waitForTimeout(ms);
const shot = async name => page.screenshot({ path: `${SHOTS}/${name}.png`, fullPage: false });

// One missing element should fail its own check, not abort the run and hide
// every check after it.
const attr = async (selector, name) => {
  try { return await page.getAttribute(selector, name, { timeout: 2500 }); }
  catch { return null; }
};
const text = async selector => {
  try { return (await page.textContent(selector, { timeout: 2500 })) ?? ''; }
  catch { return ''; }
};

const stamp = Date.now();
const player = {
  name: 'Bryn Ashwalker',
  email: `bryn.${stamp}@example.com`,
  password: 'the-lantern-burns-low',
};

console.log('\n=== the gate ===');

await page.goto(CLIENT, { waitUntil: 'networkidle' });
await settle(2500);                        // WebAssembly boot

const landing = await page.textContent('body');
check('the landing page renders for an anonymous visitor', /Create an Account/.test(landing));
check('it offers a way in', /Sign In/.test(landing));
check('it does not start the game', (await page.locator('#gameCanvas').count()) === 0);
await shot('01-landing');

console.log('\n=== the guard ===');

for (const route of ['/play', '/heroes', '/profile', '/settings']) {
  await page.goto(CLIENT + route);
  await settle(1200);
  const url = page.url();
  check(`${route} redirects an anonymous visitor to sign-in`, url.includes('/signin'), url);
  check(`${route} is remembered as the destination`,
    decodeURIComponent(url).includes(`returnUrl=${route}`), url);
}

await page.goto(CLIENT + '/play');
await settle(1200);
check('the game canvas is never created for an anonymous visitor',
  (await page.locator('#gameCanvas').count()) === 0);
check('no game assets were requested',
  !(await page.evaluate(() => performance.getEntriesByType('resource')
    .some(r => /assets\/(Outside|Caves)|js\/game\.js/.test(r.name)))));
await shot('02-guard-redirect');

console.log('\n=== sign up ===');

await page.goto(CLIENT + '/signup');
await settle(1200);
await shot('03-signup-empty');

// Submit empty, to see the validation rather than a silent no-op.
await page.click('button[type=submit]');
await settle(600);
const emptyErrors = await page.locator('.aq-field-error, .validation-message').allTextContents();
check('an empty form reports what is missing',
  emptyErrors.filter(t => t.trim()).length >= 3, `${emptyErrors.filter(t => t.trim()).length} messages`);

await page.fill('#signup-name', player.name);
await page.fill('#signup-email', 'not-an-email');
await page.fill('#signup-password', 'short');
await page.fill('#signup-confirm', 'different');
await page.click('button[type=submit]');
await settle(600);
const badText = await text('.aq-form');
check('a malformed email is caught', /does not look like an email/.test(badText));
check('a short password is caught', /at least 10 characters/i.test(badText));
check('a mismatched confirmation is caught', /do not match/.test(badText));
await shot('04-signup-invalid');

// The strength meter should move as the password gets better.
await page.fill('#signup-password', 'abcdefghij');
await settle(300);
const weakLabel = await text('.aq-strength-label');
await page.fill('#signup-password', player.password + '-A1!');
await settle(300);
const strongLabel = await text('.aq-strength-label');
check('the strength meter responds to the password',
  weakLabel.trim() !== strongLabel.trim(), `"${weakLabel.trim()}" → "${strongLabel.trim()}"`);

await page.fill('#signup-email', player.email);
await page.fill('#signup-password', player.password);
await page.fill('#signup-confirm', player.password);
await page.fill('#signup-country', 'Iceland');
await shot('05-signup-filled');
await page.click('button[type=submit]');
await settle(2000);

check('a valid sign-up lands in the game', page.url().includes('/heroes'), page.url());
check('the player is greeted by name',
  (await page.textContent('body')).includes(player.name.split(' ')[0]), '');
await shot('06-heroes-after-signup');

console.log('\n=== the player menu ===');

check('the top bar shows the player', (await page.locator('.aq-playermenu-trigger').count()) === 1);
check('the default portrait is drawn when none was uploaded',
  (await attr('.aq-playermenu-trigger img', 'src') ?? '').includes('default-avatar'));

await page.click('.aq-playermenu-trigger');
await settle(400);
const menu = await text('.aq-dropdown');
check('the dropdown opens', menu.length > 0);
for (const item of ['Profile', 'Characters', 'Settings', 'Change password', 'Log out']) {
  check(`the menu offers "${item}"`, menu.includes(item));
}
check('the trigger reports its expanded state to assistive tech',
  ['True','true'].includes(await attr('.aq-playermenu-trigger', 'aria-expanded')));
await shot('07-player-menu');

await page.keyboard.press('Escape');
await settle(300);
check('Escape closes the dropdown', (await page.locator('.aq-dropdown').count()) === 0);

console.log('\n=== the game actually loads once signed in ===');

await page.goto(CLIENT + '/play');
// Long enough to pass PlayTimeTracker's 10-second floor, so leaving the page
// actually reports a session rather than discarding it as a mis-click.
await settle(14000);
check('the canvas exists for a signed-in player', (await page.locator('#gameCanvas').count()) === 1);
const drew = await page.evaluate(() => {
  const c = document.querySelector('#gameCanvas');
  return c ? c.width > 0 && c.height > 0 : false;
});
check('the canvas has been sized and is rendering', drew);
await shot('08-game-running');

// Leaving via the in-game button rather than a fresh page load: that is the
// path a player takes, and it is the one that disposes the page and reports the
// session.
await page.click('button:has-text("Abandon Delve")');
await settle(2500);
check('abandoning the delve returns to hero select', page.url().includes('/heroes'), page.url());

console.log('\n=== profile ===');

await page.goto(CLIENT + '/profile');
await settle(2500);
const profile = await page.textContent('body');
check('the profile shows the name', profile.includes(player.name));
check('the profile shows the email', profile.includes(player.email));
check('the profile shows the country', profile.includes('Iceland'));
check('the profile shows when the account was created', /Joined/.test(profile));
check('the profile shows the last sign-in', /Last seen/.test(profile));
for (const section of ['Avatar', 'Game Statistics', 'Personal Information', 'Account Settings', 'Recent Activity']) {
  check(`the "${section}" section is present`, profile.includes(section));
}
check('play time was reported by the game page', !/Time in the dark[\s\S]{0,80}0s/.test(profile));

// The statistic tiles must stack label-over-value. They once did not: another
// module owned the class name and turned them into flex rows, wrapping every
// value onto three lines. Nothing errored — only the layout was wrong.
const tile = await page.evaluate(() => {
  const el = document.querySelector('.aq-gamestat');
  if (!el) return null;
  const dt = el.querySelector('dt').getBoundingClientRect();
  const dd = el.querySelector('dd').getBoundingClientRect();
  return { stacked: dd.top >= dt.bottom - 1, lines: Math.round(dd.height / 22) };
});
check('each statistic stacks its label above its value', tile?.stacked === true, JSON.stringify(tile));
await shot('09-profile');

console.log('\n=== avatar upload ===');

// An 8x8 red PNG — small, real, and obviously not the default portrait. Every
// chunk checksums: an earlier fixture here did not, and the server refused it,
// correctly. That looked exactly like an upload bug for an hour.
const png = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAAEklEQVR4nGM4YRP1Hx9mGBkKAOorl0F2kxb0AAAAAElFTkSuQmCC',
  'base64');

await page.setInputFiles('#avatar-file', { name: 'portrait.png', mimeType: 'image/png', buffer: png });
await settle(900);
check('choosing a file shows a preview before uploading',
  (await page.locator('.aq-avatar-badge').count()) === 1);
await shot('10-avatar-preview');

await page.click('button:has-text("Upload image")');
await settle(2500);

const avatarSrc = await attr('.aq-profile-head img', 'src');
check('the portrait is now served from the API', (avatarSrc ?? '').includes('/api/avatars/'), avatarSrc);
check('the top bar portrait updated too, without a reload',
  ((await attr('.aq-playermenu-trigger img', 'src')) ?? '').includes('/api/avatars/'));
check('a success notification appeared', (await page.locator('.aq-toast.ok').count()) >= 1);
await shot('11-avatar-uploaded');

// And an upload that must be refused.
await page.setInputFiles('#avatar-file', {
  name: 'evil.png', mimeType: 'image/png', buffer: Buffer.from('<script>alert(1)</script>'),
});
await settle(700);
await page.click('button:has-text("Upload image")');
await settle(2000);
check('a file that is not really an image is refused',
  (await page.textContent('body')).includes('could not be read') ||
  (await page.locator('.aq-form-error').count()) >= 1);
await shot('12-avatar-refused');

console.log('\n=== editing the profile ===');

await page.fill('#profile-name', 'Bryn the Unlit');
await page.fill('#profile-country', 'Norway');
await page.click('button:has-text("Save changes")');
await settle(1800);
check('the edit is confirmed', (await page.locator('.aq-toast.ok').count()) >= 1);
check('the banner shows the new name', (await text('.aq-profile-name')).includes('Bryn the Unlit'));
check('the top bar shows the new name too',
  (await text('.aq-playermenu-name')).includes('Bryn the Unlit'));
check('email is not editable', await page.locator('#profile-email').isDisabled());
await shot('13-profile-edited');

console.log('\n=== the session persists ===');

await page.reload({ waitUntil: 'networkidle' });
await settle(3000);
check('a reload keeps the player signed in', !page.url().includes('/signin'), page.url());
check('and still on the profile', (await page.textContent('body')).includes('Bryn the Unlit'));

const stored = await page.evaluate(() => ({
  local: localStorage.getItem('alfarquest.session'),
  session: sessionStorage.getItem('alfarquest.session'),
}));
check('a token is stored', !!(stored.local || stored.session));
check('sign-up did not tick "remember me", so the token is tab-scoped',
  !stored.local && !!stored.session, `local=${!!stored.local} session=${!!stored.session}`);
check('the stored token is not the password', !(stored.session ?? '').includes(player.password));

console.log('\n=== sign out ===');

await page.click('.aq-playermenu-trigger');
await settle(400);
await page.click('button:has-text("Log out")');
await settle(1800);

check('signing out returns to the gate', page.url().replace(/\/$/, '') === CLIENT.replace(/\/$/, ''), page.url());
const afterOut = await page.evaluate(() => ({
  local: localStorage.getItem('alfarquest.session'),
  session: sessionStorage.getItem('alfarquest.session'),
}));
check('the stored token is gone', !afterOut.local && !afterOut.session);

await page.goto(CLIENT + '/profile');
await settle(1400);
check('the profile is protected again after signing out', page.url().includes('/signin'), page.url());
await shot('14-signed-out');

console.log('\n=== sign in, with remember me ===');

await page.goto(CLIENT + '/signin');
await settle(1000);
await page.fill('#signin-email', player.email);
await page.fill('#signin-password', 'the-wrong-password');
await page.click('button[type=submit]');
await settle(1800);
const refusal = (await text('.aq-form-error')).trim();
check('a wrong password is refused in the form', /do not match/.test(refusal), refusal);
check('the refusal does not say whether the account exists',
  !/no account|not found|unknown|no such/i.test(refusal), refusal);
await shot('15-signin-refused');

await page.fill('#signin-password', player.password);
await page.check('input[type=checkbox]');
await page.click('button[type=submit]');
await settle(2200);
check('the right password gets in', page.url().includes('/heroes'), page.url());

const remembered = await page.evaluate(() => ({
  local: localStorage.getItem('alfarquest.session'),
  session: sessionStorage.getItem('alfarquest.session'),
}));
check('"remember me" stores the token so it survives the browser closing',
  !!remembered.local && !remembered.session, `local=${!!remembered.local} session=${!!remembered.session}`);

console.log('\n=== redirect back to where they were going ===');

await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
await page.goto(CLIENT + '/play');
await settle(1600);
check('an anonymous visit to /play is intercepted', page.url().includes('/signin'));
await page.fill('#signin-email', player.email);
await page.fill('#signin-password', player.password);
await page.click('button[type=submit]');
await settle(3000);
check('signing in resumes the interrupted journey to /play', page.url().includes('/play'), page.url());
await shot('16-resumed-to-play');

console.log('\n=== an open redirect is not honoured ===');

await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
await page.goto(CLIENT + '/signin?returnUrl=' + encodeURIComponent('https://example.com/'));
await settle(1400);
await page.fill('#signin-email', player.email);
await page.fill('#signin-password', player.password);
await page.click('button[type=submit]');
await settle(2500);
check('an absolute returnUrl is ignored, not followed',
  page.url().startsWith(CLIENT), page.url());

console.log('\n=== keyboard and screen readers ===');

await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
await page.goto(CLIENT + '/signin');
await settle(1500);
const focused = await page.evaluate(() => document.activeElement?.id);
check('the caret lands in the first field', focused === 'signin-email', `focus on "${focused}"`);

const labelled = await page.evaluate(() =>
  [...document.querySelectorAll('.aq-form input:not([type=checkbox])')].every(
    i => i.id && document.querySelector(`label[for="${i.id}"]`)));
check('every input has a label pointing at it', labelled);

await page.goto(CLIENT + '/profile');
await settle(1200);

console.log('\n=== console ===');
const expected = /favicon|status of 401|status of 400 \(Bad Request\)/;
const noisy = consoleErrors.filter(e => !expected.test(e));
console.log(`  (${consoleErrors.length - noisy.length} expected 400/401s from the refusal checks were ignored)`);
check('the app logged no errors', noisy.length === 0, noisy.slice(0, 3).join(' | '));

await browser.close();
console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed\n`);
process.exit(failed === 0 ? 0 : 1);
