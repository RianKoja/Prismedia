import { afterEach, describe, expect, it, vi } from "vitest";
import { load } from "./+layout";

function stubAuthFetch(options: { needsSetup?: boolean; user?: object | null } = {}) {
  const { needsSetup = false, user = null } = options;
  vi.stubGlobal(
    "fetch",
    vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.includes("/auth/setup-status")) {
        return new Response(JSON.stringify({ needsSetup, hasUsers: true }), {
          headers: { "Content-Type": "application/json" },
        });
      }

      if (url.includes("/auth/me")) {
        return user
          ? new Response(JSON.stringify(user), { headers: { "Content-Type": "application/json" } })
          : new Response(null, { status: 401 });
      }

      return new Response(JSON.stringify({ values: {} }), {
        headers: { "Content-Type": "application/json" },
      });
    }),
  );
}

function loadEvent(pathname: string) {
  const url = new URL(`http://localhost${pathname}`);
  return { url, untrack: <T>(fn: () => T) => fn() } as never;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("root layout load", () => {
  it("hydrates the sidebar collapsed state from the browser cookie before rendering", async () => {
    document.cookie = "prismedia-sidebar=collapsed;path=/";
    stubAuthFetch();

    // Anonymous boot on the login page: no redirect, cookie hydration still applies.
    const data = await load(loadEvent("/login"));

    expect((data as App.PageData).initialCollapsed).toBe(true);
  });

  it("redirects anonymous visitors on protected routes to the login page", async () => {
    stubAuthFetch();

    await expect(load(loadEvent("/movies"))).rejects.toMatchObject({
      status: 307,
      location: "/login?returnTo=%2Fmovies",
    });
  });

  it("redirects everything to the setup wizard while no admin exists", async () => {
    stubAuthFetch({ needsSetup: true });

    await expect(load(loadEvent("/movies"))).rejects.toMatchObject({
      status: 307,
      location: "/setup",
    });
  });

  it("returns the signed-in user for authenticated boots", async () => {
    stubAuthFetch({ user: { id: "u1", username: "paul", role: "admin" } });

    const data = await load(loadEvent("/movies"));

    expect((data as App.PageData).user).toMatchObject({ username: "paul" });
    expect((data as App.PageData).needsSetup).toBe(false);
  });
});
