// Flags class names defined in more than one CSS module.
//
//     node tools/css-collisions.mjs
//
// The stylesheet is split into modules so a change to one screen cannot reach
// another. That guarantee is by convention only — the files are concatenated by
// @import into one global sheet, and the last one to define a name wins.
//
// It failed exactly that way once: the profile page defined .aq-stat, the
// character sheet already had it, and because character.css is imported later
// every statistic tile silently took the character sheet's flex layout. Nothing
// errored; the page just looked wrong.
//
// Names that are *meant* to be shared live in the modules listed as SHARED.

import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const DIR = 'src/AlfarQuest.Client/wwwroot/css/modules';

// Modules that exist to be used from anywhere. A name defined here and used
// elsewhere is the point, not a collision.
const SHARED = new Set(['base.css', 'buttons.css', 'forms.css', 'feedback.css']);

const owners = new Map();          // class name -> [modules that define it]

for (const file of readdirSync(DIR).filter(f => f.endsWith('.css'))) {
  const css = readFileSync(join(DIR, file), 'utf8')
    .replace(/\/\*[\s\S]*?\*\//g, '');          // strip comments: they mention class names

  // A module *owns* the name a selector starts with, and only that one.
  //
  // `.aq-levelup .aq-cw-portrait { … }` resizes a shared component inside its
  // own popup — scoped, deliberate, and not a collision. `.aq-cw-portrait { … }`
  // in a second module would be. The leading class is what tells them apart.
  for (const [, selectors] of css.matchAll(/([^{}]+)\{[^{}]*\}/g)) {
    for (const selector of selectors.split(',')) {
      const head = selector.trim().match(/^\.(aq-[a-z0-9-]+)/);
      if (!head) continue;                    // @media, element selectors, :root

      const name = head[1];
      if (!owners.has(name)) owners.set(name, new Set());
      owners.get(name).add(file);
    }
  }
}

const clashes = [...owners]
  .filter(([, files]) => files.size > 1)
  .filter(([, files]) => ![...files].some(f => SHARED.has(f)))
  .map(([name, files]) => ({ name, files: [...files] }));

if (clashes.length === 0) {
  console.log(`no collisions across ${readdirSync(DIR).filter(f => f.endsWith('.css')).length} modules`);
  process.exit(0);
}

console.log(`${clashes.length} class name(s) defined in more than one module:\n`);
for (const { name, files } of clashes) console.log(`  .${name}  —  ${files.join(', ')}`);
console.log('\nRename one side. Whichever module is imported last in app.css is winning.');
process.exit(1);
