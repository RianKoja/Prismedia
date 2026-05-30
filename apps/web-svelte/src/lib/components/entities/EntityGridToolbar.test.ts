import { render, screen } from "@testing-library/svelte";
import { describe, expect, it, vi } from "vitest";
import { CAPABILITY_KIND } from "$lib/entities/entity-codes";
import EntityGridToolbar from "./EntityGridToolbar.svelte";

describe("EntityGridToolbar", () => {
  it("keeps the reset action in the active filter row", () => {
    const { container } = render(EntityGridToolbar, {
      props: {
        activeFilterIds: ["progress:played:false"],
        activePresetId: null,
        canClearFiltersAndSort: true,
        drawerOpen: false,
        filterOptions: [
          {
            id: "progress:played:false",
            count: 1,
            label: "Unplayed",
            capabilityKind: CAPABILITY_KIND.progress,
            value: "played:false",
          },
        ],
        maxScale: 8,
        minScale: 2,
        onActiveFilterIdsChange: vi.fn(),
        onApplyPreset: vi.fn(),
        onClearFiltersAndSort: vi.fn(),
        onDeletePreset: vi.fn(),
        onDrawerOpenChange: vi.fn(),
        onOverwritePreset: vi.fn(),
        onQueryChange: vi.fn(),
        onMediaWallChange: vi.fn(),
        onSavePreset: vi.fn(),
        onScaleChange: vi.fn(),
        onSortByChange: vi.fn(),
        onSortDirChange: vi.fn(),
        onReshuffle: vi.fn(),
        onViewModeChange: vi.fn(),
        presets: [],
        mediaWall: false,
        query: "",
        scale: 4,
        selectedCount: 0,
        sortBy: "title",
        sortDir: "asc",
        viewMode: "grid",
      },
    });

    const clearButton = screen.getByRole("button", { name: "Clear" });

    expect(container.querySelector(".filter-row")?.contains(clearButton)).toBe(true);
    expect(container.querySelector(".controls-row")?.contains(clearButton)).toBe(false);
  });
});
