// Merge tools/i18n-patch.json into pt-BR.json, appending the new pairs
// before the closing brace so the existing hand-grouped layout survives.
//
//     node tools/i18n-merge.mjs
import fs from 'node:fs';
import path from 'node:path';

const target = path.resolve('../src/AlfarQuest.Client/wwwroot/i18n/pt-BR.json');
const patch = JSON.parse(fs.readFileSync('i18n-patch.json', 'utf8'));
let text = fs.readFileSync(target, 'utf8');
const existing = JSON.parse(text);

const fresh = Object.entries(patch).filter(([k]) => existing[k] === undefined);
if (fresh.length === 0) { console.log('nothing to merge'); process.exit(0); }

const lines = fresh.map(([k, v]) => `  ${JSON.stringify(k)}: ${JSON.stringify(v)}`).join(',\n');
const close = text.lastIndexOf('}');
let head = text.slice(0, close).replace(/\s*$/, '');
if (!head.endsWith(',') && !head.endsWith('{')) head += ',';
text = head + '\n\n' + lines + '\n' + text.slice(close);

JSON.parse(text); // must still be valid before we touch the file
fs.writeFileSync(target, text);
console.log(`${fresh.length} pairs merged into pt-BR.json (${Object.entries(patch).length - fresh.length} already present)`);
