// =====================================================================
//  The canvas layer's half of the translation.
//
//  The Razor side owns the table — it loads i18n/<code>.json and hands it
//  here at start-up, so the HUD and the windows around it can never disagree
//  about what a skill or an item is called.
//
//  Keyed by the English string, exactly as the C# side is: a line with no
//  translation yet draws in English rather than drawing a key.
// =====================================================================

let table = new Map();
let code = "en-US";

/// Called once from C# before the first frame.
export function install(languageCode, entries) {
    code = languageCode || "en-US";
    table = new Map(Object.entries(entries || {}));
    document.documentElement.lang = code;
}

/// The player's wording for an English string.
export function t(english) {
    const translated = table.get(english);
    return translated ? translated : english;
}

/// Translate, then fill in — a translation may order its placeholders
/// differently from English.
export function tf(english, ...args) {
    return t(english).replace(/\{(\d+)\}/g, (m, i) => (args[+i] ?? m));
}

export const languageCode = () => code;
