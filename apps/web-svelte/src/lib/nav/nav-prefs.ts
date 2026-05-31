import { createListPrefs, isRecord } from "$lib/list-prefs";
import { readCookie } from "$lib/utils/cookie";
import { buildNavCatalog, defaultNavPrefs, MAX_MOBILE_FAVORITES, type NavPrefs } from "./nav-catalog";

/** Cookie name holding the per-device navigation customization. */
export const NAV_PREFS_COOKIE = "prismedia-nav";

function asStringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((v): v is string => typeof v === "string") : [];
}

/**
 * Cookie-backed store for {@link NavPrefs}, reusing the shared list-prefs
 * factory for JSON/URI encoding and read/write/clear. Validation is permissive:
 * malformed shapes fall back to defaults rather than throwing, and unknown
 * sections/items are simply ignored at resolve time.
 */
export const navPrefsStore = createListPrefs<NavPrefs>({
  cookieName: NAV_PREFS_COOKIE,
  defaults: () => defaultNavPrefs(buildNavCatalog()),
  validate: (parsed): NavPrefs | null => {
    if (parsed.v !== 1) return null;
    if (!Array.isArray(parsed.sections)) return null;

    const sections: NavPrefs["sections"] = [];
    for (const raw of parsed.sections) {
      if (!isRecord(raw)) continue;
      if (typeof raw.id !== "string" || typeof raw.label !== "string") continue;
      sections.push({ id: raw.id, label: raw.label, items: asStringArray(raw.items) });
    }
    if (sections.length === 0) return null;

    return {
      v: 1,
      sections,
      hidden: asStringArray(parsed.hidden),
      mobileFavorites: asStringArray(parsed.mobileFavorites).slice(0, MAX_MOBILE_FAVORITES),
    };
  },
});

/** Read the saved customization, falling back to seeded defaults. */
export function readNavPrefs(): NavPrefs {
  return navPrefsStore.parse(readCookie(NAV_PREFS_COOKIE)) ?? navPrefsStore.defaults();
}
