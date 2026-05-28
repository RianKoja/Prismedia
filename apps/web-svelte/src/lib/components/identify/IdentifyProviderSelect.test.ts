import { fireEvent, render, screen, within } from "@testing-library/svelte";
import { describe, expect, it, vi } from "vitest";
import IdentifyProviderSelect from "./IdentifyProviderSelect.svelte";
import type { PluginProvider } from "$lib/api/identify-types";

describe("IdentifyProviderSelect", () => {
  it("filters providers by search text and commits the selected provider id", async () => {
    const onChange = vi.fn();
    render(IdentifyProviderSelect, {
      providers: [
        provider("tmdb", "The Movie Database"),
        provider("youtube", "YouTube Metadata"),
        provider("musicbrainz", "MusicBrainz"),
      ],
      selectedId: "tmdb",
      onChange,
    });

    await fireEvent.click(screen.getByRole("button", { name: "Provider: The Movie Database" }));
    await fireEvent.input(screen.getByLabelText("Search providers"), {
      target: { value: "you" },
    });

    const listbox = screen.getByRole("listbox");
    expect(within(listbox).getByText("YouTube Metadata")).toBeInTheDocument();
    expect(within(listbox).queryByText("MusicBrainz")).not.toBeInTheDocument();

    await fireEvent.mouseDown(within(listbox).getByRole("option", { name: /youtube metadata/i }));

    expect(onChange).toHaveBeenCalledWith("youtube");
  });
});

function provider(id: string, name: string): PluginProvider {
  return {
    id,
    name,
    version: "1.0.0",
    installed: true,
    enabled: true,
    isNsfw: false,
    supports: [{ entityKind: "video", actions: ["search"] }],
    auth: [],
    missingAuthKeys: [],
  };
}
