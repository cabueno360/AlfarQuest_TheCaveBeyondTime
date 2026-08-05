// Every user-facing string the canvas or the windows can show, checked against
// pt-BR.json — the missing ones written to a work list.
//
//     node tools/i18n-audit.mjs
//
// Sources swept: Pages/PageFlags-carrying Examinables in every .tmx (split on
// the ␟ page separator), item Flavour strings and merchant greetings in the
// C# catalogues. Titles, names and UI strings were already covered; this is
// the long-prose tail the first pass skipped.
import fs from 'node:fs';
import path from 'node:path';

const WWW = path.resolve('../src/AlfarQuest.Client/wwwroot');
const SRC = path.resolve('../src/AlfarQuest.Client');
const ptBR = JSON.parse(fs.readFileSync(path.join(WWW, 'i18n/pt-BR.json'), 'utf8'));

const missing = new Map();      // text -> where first seen
const note = (text, where) => {
    const t = text.trim();
    if (t.length === 0 || ptBR[t] !== undefined || missing.has(t)) return;
    missing.set(t, where);
};

// 1. Pages in every map.
const mapDirs = ['Maps/Regions', 'Maps/Interiors', 'Maps/Cave'];
for (const dir of mapDirs) {
    const full = path.join(WWW, dir);
    if (!fs.existsSync(full)) continue;
    for (const f of fs.readdirSync(full).filter(f => f.endsWith('.tmx'))) {
        const xml = fs.readFileSync(path.join(full, f), 'utf8');
        for (const m of xml.matchAll(/name="Pages" value="([^"]+)"/g)) {
            const decoded = m[1].replace(/&quot;/g, '"').replace(/&#39;|&apos;/g, "'")
                .replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&amp;/g, '&');
            for (const page of decoded.split('␟')) note(page, f);
        }
    }
}

// 2. Flavour strings in the item catalogues. C# concatenations
// ("..." + "...") are joined, because the runtime key is the joined text.
const flavour = /Flavour\s*[:=]\s*((?:"(?:[^"\\]|\\.)*"\s*(?:\+\s*)?)+)/g;
const joinLiterals = raw => [...raw.matchAll(/"((?:[^"\\]|\\.)*)"/g)]
    .map(m => m[1].replace(/\\"/g, '"')).join('');
for (const f of fs.readdirSync(path.join(SRC, 'Services/Character')).filter(f => f.endsWith('.cs'))) {
    const cs = fs.readFileSync(path.join(SRC, 'Services/Character', f), 'utf8');
    for (const m of cs.matchAll(flavour)) note(joinLiterals(m[1]), f);
}

// 3. Merchant greetings.
const merchants = fs.readFileSync(path.join(SRC, 'Game/MerchantCatalog.cs'), 'utf8');
for (const m of merchants.matchAll(/Greeting\s*[:=]\s*"((?:[^"\\]|\\.)*)"/g))
    note(m[1].replace(/\\"/g, '"'), 'MerchantCatalog.cs');

// 4. Region display names the slot picker prettifies.
for (const name of ['Ashwold', 'Deepdelve']) note(name, 'Home.Prettify');

const list = [...missing.entries()].map(([t, w]) => `[${w}]\n${t}`).join('\n\n');
fs.writeFileSync('i18n-missing.txt', list);
console.log(`${missing.size} strings missing from pt-BR.json — written to tools/i18n-missing.txt`);
