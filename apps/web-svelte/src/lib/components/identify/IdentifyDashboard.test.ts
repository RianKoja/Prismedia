import { render, screen } from "@testing-library/svelte";
import { beforeEach, describe, expect, it, vi } from "vitest";
import IdentifyDashboard from "./IdentifyDashboard.svelte";

const store = vi.hoisted(() => ({
  loading: false,
  providers: [] as Array<{
    id: string;
    name: string;
    version: string;
    installed: boolean;
    enabled: boolean;
    supports: Array<{ entityKind: string; actions: string[] }>;
    auth: unknown[];
    missingAuthKeys: string[];
  }>,
  queue: [],
  supportedKinds: [] as Array<{
    kind: string;
    label: string;
    total: number;
    unidentified: number;
    pending: number;
    hasProvider: boolean;
  }>,
  navigateToKind: vi.fn(),
  resumeNext: vi.fn(),
  reviewQueueItem: vi.fn(),
}));

vi.mock("./identify-store.svelte", () => ({
  useIdentifyStore: () => store,
}));

describe("IdentifyDashboard", () => {
  beforeEach(() => {
    store.loading = false;
    store.providers = [provider()];
    store.queue = [];
    store.supportedKinds = [
      {
        kind: "video",
        label: "Videos",
        total: 0,
        unidentified: 0,
        pending: 0,
        hasProvider: true,
      },
    ];
    store.navigateToKind.mockReset();
    store.resumeNext.mockReset();
    store.reviewQueueItem.mockReset();
  });

  it("keeps the provider summary without rendering the plugin inventory panel", () => {
    render(IdentifyDashboard);

    expect(screen.getByText("Providers")).toBeInTheDocument();
    expect(screen.getByText("1/1")).toBeInTheDocument();
    expect(screen.queryByText("Plugins")).not.toBeInTheDocument();
    expect(screen.queryByText("The Movie Database")).not.toBeInTheDocument();
  });
});

function provider() {
  return {
    id: "tmdb",
    name: "The Movie Database",
    version: "1.0.0",
    installed: true,
    enabled: true,
    supports: [{ entityKind: "video", actions: ["search"] }],
    auth: [],
    missingAuthKeys: [],
  };
}
