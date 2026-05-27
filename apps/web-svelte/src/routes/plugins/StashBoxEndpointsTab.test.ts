import { fireEvent, render, screen } from "@testing-library/svelte";
import type { ComponentProps } from "svelte";
import { describe, expect, it, vi } from "vitest";
import type { StashBoxEndpoint } from "$lib/api/plugins";
import StashBoxEndpointsTab from "./StashBoxEndpointsTab.svelte";

type StashBoxEndpointsTabProps = ComponentProps<typeof StashBoxEndpointsTab>;

const endpoint: StashBoxEndpoint = {
  id: "stashdb",
  name: "StashDB",
  endpoint: "https://stashdb.org/graphql",
  apiKeyPreview: "****1234",
  enabled: true,
  isNsfw: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

function baseProps(overrides: Partial<StashBoxEndpointsTabProps> = {}): StashBoxEndpointsTabProps {
  const props: StashBoxEndpointsTabProps = {
    apiKey: "",
    editingEndpoint: null,
    endpoint: "",
    endpoints: [endpoint],
    name: "",
    onAdd: vi.fn(),
    onDelete: vi.fn(),
    onEdit: vi.fn(),
    onSave: vi.fn(),
    onTest: vi.fn(),
    onToggleEnabled: vi.fn(),
    saving: false,
    showForm: false,
    testingId: null,
    testResult: null,
  };

  return {
    ...props,
    ...overrides,
  };
}

describe("StashBoxEndpointsTab", () => {
  it("renders configured endpoints with connection actions", async () => {
    const onTest = vi.fn();
    const onEdit = vi.fn();
    render(StashBoxEndpointsTab, {
      props: baseProps({ onEdit, onTest }),
    });

    expect(screen.getByText("StashDB")).toBeInTheDocument();
    expect(screen.getByText(/Key: \*\*\*\*1234/i)).toBeInTheDocument();

    await fireEvent.click(screen.getByRole("button", { name: /test connection/i }));
    await fireEvent.click(screen.getByRole("button", { name: /edit/i }));

    expect(onTest).toHaveBeenCalledWith(endpoint);
    expect(onEdit).toHaveBeenCalledWith(endpoint);
  });

  it("shows the add form on request", async () => {
    const onAdd = vi.fn();
    render(StashBoxEndpointsTab, {
      props: baseProps({
        endpoints: [],
        onAdd,
        showForm: true,
      }),
    });

    await fireEvent.click(screen.getByRole("button", { name: /add endpoint/i }));

    expect(onAdd).toHaveBeenCalled();
    expect(screen.getByText("Add Stash-Box Endpoint")).toBeInTheDocument();
    expect(screen.getByLabelText("GraphQL Endpoint")).toBeInTheDocument();
  });
});
