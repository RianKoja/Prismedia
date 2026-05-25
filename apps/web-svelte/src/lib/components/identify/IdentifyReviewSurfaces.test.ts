import { fireEvent, render, screen } from "@testing-library/svelte";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { EntityMetadataPatch, EntityMetadataProposal } from "$lib/api/identify";
import type { EntityCard } from "$lib/api/prismedia";
import IdentifyReviewChild from "./IdentifyReviewChild.svelte";
import IdentifyReviewParent from "./IdentifyReviewParent.svelte";

const store = vi.hoisted(() => ({
  applying: false,
  reviewCascadeSelections: {},
  reviewFieldSelections: {},
  reviewImageSelections: {},
  reviewTagSelections: {},
  beginProposalReview: vi.fn(),
  getReviewDetailForProposal: vi.fn(),
  getReviewFieldSelections: vi.fn(),
  getReviewImageSelections: vi.fn(),
  getReviewTagSelections: vi.fn(),
  isReviewProposalSelected: vi.fn(),
  navigateTo: vi.fn(),
  navigateToDashboard: vi.fn(),
  nextQueueItem: vi.fn(),
  reviewDetailEntityIdForProposal: vi.fn(),
  setReviewFieldSelections: vi.fn(),
  setReviewImageSelections: vi.fn(),
  setReviewProposalSelected: vi.fn(),
  setReviewTagSelections: vi.fn(),
  setReviewTagSelected: vi.fn(),
  ensureReviewDetailForProposal: vi.fn(),
  deleteQueueItem: vi.fn(),
  applyProposal: vi.fn(),
}));

vi.mock("./identify-store.svelte", () => ({
  useIdentifyStore: () => store,
}));

describe("Identify review surfaces", () => {
  beforeEach(() => {
    store.applying = false;
    store.beginProposalReview.mockReset();
    store.getReviewDetailForProposal.mockReset();
    store.getReviewFieldSelections.mockReset();
    store.getReviewImageSelections.mockReset();
    store.getReviewTagSelections.mockReset();
    store.isReviewProposalSelected.mockReset();
    store.isReviewProposalSelected.mockReturnValue(true);
    store.navigateTo.mockReset();
    store.navigateToDashboard.mockReset();
    store.nextQueueItem.mockReset();
    store.nextQueueItem.mockReturnValue(null);
    store.reviewDetailEntityIdForProposal.mockReset();
    store.reviewDetailEntityIdForProposal.mockReturnValue(null);
    store.setReviewFieldSelections.mockReset();
    store.setReviewImageSelections.mockReset();
    store.setReviewProposalSelected.mockReset();
    store.setReviewTagSelections.mockReset();
    store.setReviewTagSelected.mockReset();
    store.ensureReviewDetailForProposal.mockReset();
    store.deleteQueueItem.mockReset();
    store.applyProposal.mockReset();
  });

  it("renders parent review relationship thumbnails with the normal card variant", () => {
    const { container } = render(IdentifyReviewParent, {
      props: {
        entity: entity(),
        proposal: proposal("root", {
          relationships: [proposal("person-1", { targetKind: "person", title: "Tim Robinson" })],
          children: [proposal("episode-1", { targetKind: "video", title: "Episode 1" })],
        }),
      },
    });

    const thumbnails = container.querySelectorAll(".entity-thumbnail");
    expect(thumbnails.length).toBeGreaterThan(0);
    expect(container.querySelector(".entity-thumbnail.is-list")).toBeNull();
  });

  it("renders walked child relationship thumbnails with the normal card variant", () => {
    const parentProposal = proposal("root");
    const { container } = render(IdentifyReviewChild, {
      props: {
        entity: entity(),
        parentProposal,
        proposal: proposal("episode-1", {
          targetKind: "video",
          relationships: [proposal("person-1", { targetKind: "person", title: "Tim Robinson" })],
          children: [proposal("clip-1", { targetKind: "video", title: "Clip 1" })],
        }),
      },
    });

    const thumbnails = container.querySelectorAll(".entity-thumbnail");
    expect(thumbnails.length).toBeGreaterThan(0);
    expect(container.querySelector(".entity-thumbnail.is-list")).toBeNull();
  });

  it("renders poster and backdrop artwork as enlarged review groups", () => {
    const { container } = render(IdentifyReviewParent, {
      props: {
        entity: entity(),
        proposal: proposal("root", {
          images: [
            image("poster", "poster-1"),
            image("poster", "poster-2"),
            image("backdrop", "backdrop-1"),
            image("backdrop", "backdrop-2"),
          ],
        }),
      },
    });

    const artworkGroups = container.querySelector<HTMLElement>(".identify-artwork-groups");
    const posterGroup = container.querySelector<HTMLElement>("[data-artwork-kind='poster']");
    const backdropGroup = container.querySelector<HTMLElement>("[data-artwork-kind='backdrop']");

    expect(artworkGroups).not.toBeNull();
    expect(posterGroup?.classList.contains("identify-artwork-group")).toBe(true);
    expect(backdropGroup?.classList.contains("identify-artwork-group")).toBe(true);
    expect(posterGroup?.querySelectorAll(".identify-artwork-tile")).toHaveLength(2);
    expect(backdropGroup?.querySelectorAll(".identify-artwork-tile")).toHaveLength(2);
  });

  it("labels the field diff panel as base fields and collapses panel content from the header", async () => {
    render(IdentifyReviewParent, {
      props: {
        entity: entity(),
        proposal: proposal("root"),
      },
    });

    expect(screen.queryByText("Field diff")).not.toBeInTheDocument();
    const header = screen.getByRole("button", { name: /Base fields/ });
    expect(header).toHaveAttribute("aria-expanded", "true");
    expect(screen.getByText("Current")).toBeInTheDocument();

    await fireEvent.click(header);

    expect(header).toHaveAttribute("aria-expanded", "false");
    expect(screen.queryByText("Current")).not.toBeInTheDocument();
  });

  it("keeps base field actions from toggling the section", async () => {
    render(IdentifyReviewParent, {
      props: {
        entity: entity(),
        proposal: proposal("root"),
      },
    });

    const header = screen.getByRole("button", { name: /Base fields/ });
    await fireEvent.click(screen.getByRole("button", { name: "None" }));

    expect(header).toHaveAttribute("aria-expanded", "true");
  });
});

function entity(): EntityCard {
  return {
    id: "entity-1",
    kind: "video-series",
    title: "The Chair Company",
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
  };
}

function proposal(
  proposalId: string,
  overrides: Partial<EntityMetadataProposal> & { title?: string } = {},
): EntityMetadataProposal {
  const {
    title = "The Chair Company",
    patch,
    ...proposalOverrides
  } = overrides;

  return {
    proposalId,
    provider: "tmdb",
    targetKind: "video-series",
    confidence: 1,
    matchReason: "external-id",
    patch: {
      title,
      description: "A proposal description.",
      externalIds: {},
      urls: [],
      tags: [],
      studio: null,
      credits: [],
      dates: {},
      stats: {},
      positions: {},
      classification: null,
      ...patch,
    } satisfies EntityMetadataPatch,
    images: [{
      kind: "poster",
      url: `https://image.tmdb.org/t/p/original/${proposalId}.jpg`,
      source: "tmdb",
    }],
    children: [],
    relationships: [],
    candidates: [],
    ...proposalOverrides,
  };
}

function image(kind: string, name: string) {
  return {
    kind,
    url: `https://image.tmdb.org/t/p/original/${name}.jpg`,
    source: "tmdb",
  };
}
