// Exercises the authentication and profile API the way an attacker and a player
// would, and asserts what should happen. Run against a live API + MySQL:
//
//     node tools/auth-probe.mjs
//
// Every check states the property it is defending, so a failure says what broke
// rather than which line number did.

// Run it like this — the limit has to be raised for the probe to get through its
// twenty-odd credential calls, but left low enough that the loop at the bottom
// still trips it:
//
//     Auth__SignInAttemptsPerWindow=40 dotnet run --project src/AlfarQuest.Api
//     LIMIT=40 node tools/auth-probe.mjs
//
// At the shipped default of 8 the probe throttles itself halfway through and
// reports failures in sign-out and password change that are nothing of the kind.

const API = process.env.API ?? 'http://localhost:5080';

let passed = 0, failed = 0;
const check = (name, ok, detail = '') => {
  console.log(`${ok ? '  ok  ' : ' FAIL '} ${name}${detail ? ` — ${detail}` : ''}`);
  ok ? passed++ : failed++;
};

const call = async (path, { method = 'GET', body, token, raw } = {}) => {
  const headers = {};
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body && !raw) headers['Content-Type'] = 'application/json';
  const res = await fetch(`${API}${path}`, {
    method,
    headers,
    body: raw ? body : body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  let json = null;
  try { json = JSON.parse(text); } catch { /* not json — fine */ }
  return { status: res.status, json, text };
};

const stamp = Date.now();
const alice = { email: `alice.${stamp}@example.com`, password: 'a-long-enough-passphrase' };
const mallory = { email: `mallory.${stamp}@example.com`, password: 'another-long-passphrase' };

// A 1x1 PNG, and the smallest thing that is definitely not one.
const onePixelPng = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==',
  'base64');

console.log(`\n=== registration ===`);

const signup = await call('/api/auth/signup', {
  method: 'POST',
  body: { fullName: 'Alice Delver', email: alice.email, password: alice.password,
          confirmPassword: alice.password, country: 'Iceland' },
});
check('a valid sign-up succeeds', signup.status === 200, `status ${signup.status}`);
check('sign-up returns a session token', !!signup.json?.token);
check('sign-up returns the profile', signup.json?.player?.email === alice.email);
check('the password never comes back', !signup.text.includes(alice.password));
check('no password hash is ever serialised', !/passwordHash/i.test(signup.text));

const aliceToken = signup.json?.token;

const dupe = await call('/api/auth/signup', {
  method: 'POST',
  body: { fullName: 'Someone Else', email: alice.email.toUpperCase(), password: 'yet-another-passphrase',
          confirmPassword: 'yet-another-passphrase' },
});
check('a duplicate email is refused, case-insensitively', dupe.status === 400, `status ${dupe.status}`);
check('the refusal names the email field', dupe.json?.field === 'Email', dupe.json?.message);

const weak = await call('/api/auth/signup', {
  method: 'POST',
  body: { fullName: 'Weak Willed', email: `weak.${stamp}@example.com`, password: 'short', confirmPassword: 'short' },
});
check('a too-short password is refused', weak.status === 400, `status ${weak.status}`);

console.log(`\n=== sign in ===`);

const wrong = await call('/api/auth/signin', {
  method: 'POST', body: { email: alice.email, password: 'not-the-right-password' },
});
check('a wrong password is refused', wrong.status === 401, `status ${wrong.status}`);

const unknown = await call('/api/auth/signin', {
  method: 'POST', body: { email: `nobody.${stamp}@example.com`, password: 'not-the-right-password' },
});
check('an unknown address is refused', unknown.status === 401, `status ${unknown.status}`);
check('both refusals are word-for-word identical (no account enumeration)',
  wrong.json?.message === unknown.json?.message, `"${wrong.json?.message}" vs "${unknown.json?.message}"`);

const signin = await call('/api/auth/signin', {
  method: 'POST', body: { email: alice.email, password: alice.password, rememberMe: true },
});
check('the right password is accepted', signin.status === 200, `status ${signin.status}`);
check('remember-me lasts about 30 days',
  signin.json && Math.abs((new Date(signin.json.expiresAt) - Date.now()) / 86400000 - 30) < 1,
  signin.json?.expiresAt);

const shortSession = await call('/api/auth/signin', {
  method: 'POST', body: { email: alice.email, password: alice.password, rememberMe: false },
});
check('without remember-me it lasts about 12 hours',
  shortSession.json && Math.abs((new Date(shortSession.json.expiresAt) - Date.now()) / 3600000 - 12) < 1,
  shortSession.json?.expiresAt);

console.log(`\n=== protected resources ===`);

check('/api/auth/me needs a token', (await call('/api/auth/me')).status === 401);
check('/api/auth/me accepts a good token', (await call('/api/auth/me', { token: aliceToken })).status === 200);
check('a forged token is refused',
  (await call('/api/auth/me', { token: 'not-a-real-token-at-all' })).status === 401);

check('the hero roster needs a token', (await call('/api/heroes')).status === 401);
check('the hero roster opens with one', (await call('/api/heroes', { token: aliceToken })).status === 200);
check('saves need a token', (await call('/api/saves/1')).status === 401);
check('the profile needs a token', (await call('/api/profile')).status === 401);

console.log(`\n=== one player cannot reach another's data ===`);

const malloryUp = await call('/api/auth/signup', {
  method: 'POST',
  body: { fullName: 'Mallory', email: mallory.email, password: mallory.password, confirmPassword: mallory.password },
});
const malloryToken = malloryUp.json?.token;

const aliceSave = await call('/api/saves', {
  method: 'POST', token: aliceToken,
  body: { playerName: 'Alice', activeHeroKey: 'mage', region: 'cave_beyond_time', playtimeSeconds: 60, party: [] },
});
check("Alice can create her own save", aliceSave.status === 200, `status ${aliceSave.status}`);

const saveId = aliceSave.json?.id;
const stolen = await call(`/api/saves/${saveId}`, { token: malloryToken });
check("Mallory cannot read Alice's save by id", stolen.status === 404, `status ${stolen.status} for id ${saveId}`);
check('Alice can read her own save', (await call(`/api/saves/${saveId}`, { token: aliceToken })).status === 200);

const overwrite = await call('/api/saves', {
  method: 'POST', token: malloryToken,
  body: { id: saveId, playerName: 'Mallory', activeHeroKey: 'thief', region: 'x', playtimeSeconds: 0, party: [] },
});
check("Mallory cannot overwrite Alice's save", overwrite.status === 404, `status ${overwrite.status}`);

const stillAlice = await call(`/api/saves/${saveId}`, { token: aliceToken });
check("Alice's save is untouched", stillAlice.json?.playerName === 'Alice', stillAlice.json?.playerName);

console.log(`\n=== avatar upload ===`);

const upload = async (token, bytes, filename, type) => {
  const form = new FormData();
  form.append('file', new Blob([bytes], { type }), filename);
  const res = await fetch(`${API}/api/avatars`, {
    method: 'POST', headers: { Authorization: `Bearer ${token}` }, body: form,
  });
  return { status: res.status, text: await res.text() };
};

const good = await upload(aliceToken, onePixelPng, 'me.png', 'image/png');
check('a real PNG is accepted', good.status === 200, good.text.slice(0, 120));
check('the profile now carries an avatar url', /avatarUrl":"api\/avatars\//.test(good.text));

const disguised = await upload(aliceToken, Buffer.from('<script>alert(1)</script>'), 'evil.png', 'image/png');
check('a script named .png is refused (bytes are sniffed, not the name)',
  disguised.status === 400, `status ${disguised.status}`);

const oversized = await upload(aliceToken, Buffer.alloc(6 * 1024 * 1024, 1), 'huge.png', 'image/png');
check('a 6 MB upload is refused', oversized.status === 400 || oversized.status === 413,
  `status ${oversized.status}`);

const avatarUrl = JSON.parse(good.text || '{}').avatarUrl;
if (avatarUrl) {
  const fetched = await fetch(`${API}/${avatarUrl}`);
  const bytes = Buffer.from(await fetched.arrayBuffer());
  check('the stored avatar is served back as an image',
    fetched.headers.get('content-type') === 'image/png', fetched.headers.get('content-type'));
  check('it was re-encoded by the server, not stored as uploaded',
    bytes.length !== onePixelPng.length, `${onePixelPng.length}B in, ${bytes.length}B out`);
  check('the response forbids content-type sniffing',
    fetched.headers.get('x-content-type-options') === 'nosniff');
}

console.log(`\n=== profile editing ===`);

const updated = await call('/api/profile', {
  method: 'PUT', token: aliceToken,
  body: { fullName: 'Alice the Bold', country: 'Norway', phoneNumber: '+47 900 00 000' },
});
check('a profile edit is saved', updated.json?.fullName === 'Alice the Bold', updated.json?.fullName);

const emailAttempt = await call('/api/profile', {
  method: 'PUT', token: aliceToken,
  body: { fullName: 'Alice the Bold', email: 'hijack@example.com', country: 'Norway' },
});
check('an email sneaked into the body is ignored',
  emailAttempt.json?.email === alice.email, emailAttempt.json?.email);

const stats = await call('/api/profile/stats', {
  method: 'POST', token: aliceToken,
  body: { currentCharacter: 'The Fallen Mage', highestLevel: 7, totalGold: 900, totalPlayTimeSeconds: 600 },
});
check('game statistics are recorded', stats.json?.stats?.highestLevel === 7, JSON.stringify(stats.json?.stats));

const regress = await call('/api/profile/stats', {
  method: 'POST', token: aliceToken,
  body: { currentCharacter: 'The Fallen Mage', highestLevel: 2, totalGold: 5, totalPlayTimeSeconds: 60 },
});
check('a lower level cannot erase a higher one', regress.json?.stats?.highestLevel === 7,
  `level ${regress.json?.stats?.highestLevel}`);
check('playtime accumulates rather than overwriting',
  regress.json?.stats?.totalPlayTimeSeconds === 660, `${regress.json?.stats?.totalPlayTimeSeconds}s`);

console.log(`\n=== sign out and password change ===`);

const throwaway = await call('/api/auth/signin', {
  method: 'POST', body: { email: mallory.email, password: mallory.password },
});
const throwawayToken = throwaway.json?.token;
check('the extra session works', (await call('/api/auth/me', { token: throwawayToken })).status === 200);
await call('/api/auth/signout', { method: 'POST', token: throwawayToken });
check('after sign-out the token is dead (real revocation, not just forgotten)',
  (await call('/api/auth/me', { token: throwawayToken })).status === 401);
check("the player's other session still works",
  (await call('/api/auth/me', { token: malloryToken })).status === 200);

const changed = await call('/api/profile/password', {
  method: 'POST', token: aliceToken,
  body: { currentPassword: 'wrong-current-password', newPassword: 'a-brand-new-passphrase',
          confirmPassword: 'a-brand-new-passphrase' },
});
check('changing a password requires the current one', changed.status === 400, `status ${changed.status}`);

const changedOk = await call('/api/profile/password', {
  method: 'POST', token: aliceToken,
  body: { currentPassword: alice.password, newPassword: 'a-brand-new-passphrase',
          confirmPassword: 'a-brand-new-passphrase' },
});
check('the right current password is accepted', changedOk.status === 204, `status ${changedOk.status}`);
check('every session ends when the password changes',
  (await call('/api/auth/me', { token: aliceToken })).status === 401);
check('the old password no longer works',
  (await call('/api/auth/signin', { method: 'POST', body: { email: alice.email, password: alice.password } })).status === 401);
check('the new password works',
  (await call('/api/auth/signin', { method: 'POST', body: { email: alice.email, password: 'a-brand-new-passphrase' } })).status === 200);

console.log(`\n=== forgot password tells nothing ===`);
const known = await call('/api/auth/forgot-password', { method: 'POST', body: { email: alice.email } });
const stranger = await call('/api/auth/forgot-password', { method: 'POST', body: { email: `ghost.${stamp}@example.com` } });
check('a known and an unknown address answer identically',
  known.status === stranger.status && known.text === stranger.text,
  `${known.status}/${stranger.status}`);

// The shipped default is 8 credential attempts per 5 minutes per address, which
// this probe would trip long before reaching here — it makes about twenty. So
// the API under test is started with Auth__SignInAttemptsPerWindow raised, and
// the loop below runs past the raised figure to prove the limiter still bites.
console.log(`\n=== throttling (limit under test: ${process.env.LIMIT ?? 'default'}) ===`);
let sawLimit = false, attempts = 0;
for (let i = 0; i < 80 && !sawLimit; i++) {
  attempts++;
  const res = await call('/api/auth/signin', {
    method: 'POST', body: { email: alice.email, password: `guess-number-${i}` },
  });
  if (res.status === 429) sawLimit = true;
}
check('repeated guessing is throttled', sawLimit, `429 after ${attempts} attempts`);

console.log(`\n${failed === 0 ? 'ALL PASS' : 'FAILURES'}: ${passed} passed, ${failed} failed\n`);
process.exit(failed === 0 ? 0 : 1);
