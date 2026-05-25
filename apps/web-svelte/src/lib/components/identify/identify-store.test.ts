import { beforeEach, describe, expect, it, vi } from "vitest";
import { IdentifyStore } from "./identify-store.svelte";
import type { EntityMetadataProposal } from "$lib/api/identify";
import type { EntityCard, EntityDetailCard } from "$lib/api/prismedia";
import { MAIN_SCROLL_TOP_EVENT } from "$lib/stores/main-scroll";

const fetchPluginProviders = vi.fn();
const fetchIdentifyQueue = vi.fn();
const fetchIdentifyEntity = vi.fn();

vi.mock("$lib/api/identify", async (importOriginal) => {
  const actual = await importOriginal<typeof import("$lib/api/identify")>();
  return {
    ...actual,
    fetchPluginProviders: (...args: unknown[]) => fetchPluginProviders(...args),
    fetchIdentifyQueue: (...args: unknown[]) => fetchIdentifyQueue(...args),
    fetchIdentifyEntity: (...args: unknown[]) => fetchIdentifyEntity(...args),
  };
});

describe("IdentifyStore", () => {
  beforeEach(() => {
    fetchPluginProviders.mockReset();
    fetchIdentifyQueue.mockReset();
    fetchIdentifyEntity.mockReset();
    fetchPluginProviders.mockResolvedValue([]);
    fetchIdentifyQueue.mockResolvedValue([]);
    fetchIdentifyEntity.mockResolvedValue(null);
  });

  it("resets stale review state when entering the dashboard route", async () => {
    const store = new IdentifyStore();
    store.view = {
      kind: "review-parent",
      entity: entity("series-1"),
      proposal: proposal("proposal-1"),
      detail: null,
    };

    await store.enterDashboardRoute();

    expect(store.view.kind).toBe("dashboard");
    expect(fetchPluginProviders).toHaveBeenCalledOnce();
    expect(fetchIdentifyQueue).toHaveBeenCalledOnce();
  });

  it("hydrates relationship proposal current detail from the scoped entity relationships", async () => {
    const store = new IdentifyStore();
    const creditProposal = proposal("tmdb:person:1500059", {
      targetKind: "person",
      title: "Tim Robinson",
    });
    const personDetail = detail("person-tim", {
      kind: "person",
      title: "Tim Robinson",
    });
    store.queue = [
      {
        id: "queue-1",
        entityId: "series-1",
        entityKind: "video-series",
        title: "Series",
        state: "proposal",
        action: "search",
        candidates: [],
        proposal: proposal("series-proposal"),
        entity: entity("series-1"),
        detail: detail("series-1", {
          kind: "video-series",
          title: "Series",
          relationships: [
            {
              kind: "person",
              label: "Credits",
              entities: [entity("person-tim", { kind: "person", title: "Tim Robinson" })],
            },
          ],
        }),
      },
    ];
    fetchIdentifyEntity.mockResolvedValue(personDetail);

    await store.ensureReviewDetailForProposal("series-1", creditProposal);

    expect(fetchIdentifyEntity).toHaveBeenCalledWith("person-tim");
    expect(store.getReviewDetailForProposal("series-1", creditProposal)?.id).toBe(personDetail.id);
    expect(store.getReviewDetailForProposal("series-1", creditProposal)?.title).toBe(personDetail.title);
  });

  it("requests a main scroll reset when navigating between review views", () => {
    const store = new IdentifyStore();
    const dispatchEvent = vi.spyOn(window, "dispatchEvent");

    store.navigateTo({
      kind: "review-child",
      entity: entity("series-1"),
      proposal: proposal("child-proposal", { targetKind: "video-episode", title: "Episode" }),
      parentProposal: proposal("parent-proposal"),
      ancestors: [proposal("parent-proposal")],
    });

    expect(dispatchEvent.mock.calls.some(([event]) => event.type === MAIN_SCROLL_TOP_EVENT)).toBe(true);
  });
});

function entity(id: string, options: Partial<EntityCard> = {}): EntityCard {
  return {
    id,
    kind: options.kind ?? "video-series",
    title: options.title ?? "Series",
    parentEntityId: null,
    sortOrder: null,
    coverUrl: null,
    hoverKind: "none",
    hoverUrl: null,
    hoverImages: [],
    meta: [],
    rating: null,
    isFavorite: false,
    isNsfw: false,
    isOrganized: false,
    ...options,
  };
}

function detail(id: string, options: Partial<EntityDetailCard> = {}): EntityDetailCard {
  return {
    id,
    kind: options.kind ?? "video-series",
    title: options.title ?? "Series",
    parentEntityId: null,
    sortOrder: null,
    capabilities: [],
    childrenByKind: [],
    relationships: [],
    ...options,
  };
}

function proposal(
  proposalId: string,
  options: { targetKind?: string; title?: string } = {},
): EntityMetadataProposal {
  return {
    proposalId,
    provider: "tmdb",
    targetKind: options.targetKind ?? "video-series",
    confidence: 1,
    matchReason: "test",
    patch: {
      title: options.title ?? "Series",
      description: null,
      externalIds: {},
      urls: [],
      tags: [],
      studio: null,
      credits: [],
      dates: {},
      stats: {},
      positions: {},
      classification: null,
    },
    images: [],
    children: [],
    relationships: [],
    candidates: [],
    targetEntityId: null,
  };
}
