import { redirect } from "@sveltejs/kit";
import type { LayoutLoad } from "./$types";
import { fetchCurrentUser, fetchSetupStatus, type AuthUser } from "$lib/api/auth";
import { fetchSettingsValues } from "$lib/api/settings";
import { settingKeys, valuesToLibrarySettings } from "$lib/settings/app-settings";
import { readSidebarCookie } from "$lib/stores/app-chrome.svelte";
import type { NsfwMode } from "$lib/nsfw/cookie";

export const ssr = false;

/** Routes reachable without a session (rendered without the app shell). */
function isPublicPath(pathname: string): boolean {
  return pathname === "/login" || pathname === "/setup" || pathname.startsWith("/setup/");
}

/** Only same-origin absolute paths survive as post-login destinations. */
function safeReturnTo(url: URL): string {
  const value = url.searchParams.get("returnTo");
  return value && value.startsWith("/") && !value.startsWith("//") ? value : "/";
}

export const load: LayoutLoad = async ({ url, untrack }) => {
  // Untracked: the guard must not re-run (and re-fetch auth) on every client navigation;
  // beforeNavigate in the layout covers client-side transitions from in-memory state.
  const pathname = untrack(() => url.pathname);
  const search = untrack(() => url.search);
  const initialCollapsed = readSidebarCookie();

  let needsSetup = false;
  let user: AuthUser | null = null;
  try {
    const [setup, currentUser] = await Promise.all([fetchSetupStatus(), fetchCurrentUser()]);
    needsSetup = setup.needsSetup;
    user = currentUser;
  } catch {
    // API unreachable during boot: render anonymously; individual calls surface errors.
    return { initialCollapsed, user: null, needsSetup: false };
  }

  // Setup outranks everything: an install without an admin only shows the wizard.
  if (needsSetup && !pathname.startsWith("/setup")) {
    redirect(307, "/setup");
  }

  if (!needsSetup && pathname.startsWith("/setup")) {
    redirect(307, "/");
  }

  if (!user && !isPublicPath(pathname)) {
    redirect(307, `/login?returnTo=${encodeURIComponent(pathname + search)}`);
  }

  if (user && pathname === "/login") {
    redirect(307, untrack(() => safeReturnTo(url)));
  }

  let initialNsfwMode: NsfwMode | undefined;
  if (user) {
    try {
      initialNsfwMode = valuesToLibrarySettings(
        (await fetchSettingsValues([settingKeys.visibilityDefaultMode])).values,
      ).visibilityDefaultMode;
    } catch {
      // Non-fatal: the NSFW store falls back to its off default.
    }
  }

  return {
    initialCollapsed,
    user,
    needsSetup,
    ...(initialNsfwMode !== undefined ? { initialNsfwMode } : {}),
  };
};
