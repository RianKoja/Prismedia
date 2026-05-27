<script lang="ts">
  import {
    AlertCircle,
    Check,
    Loader2,
    Pencil,
    Plug,
    Plus,
    RefreshCw,
    Save,
    ToggleLeft,
    ToggleRight,
    Trash2,
    X,
  } from "@lucide/svelte";
  import { Badge, Button } from "@prismedia/ui-svelte";
  import type { StashBoxEndpoint } from "$lib/api/plugins";

  type StashBoxTestResult = { id: string; valid: boolean; error?: string } | null;

  interface Props {
    apiKey: string;
    editingEndpoint: StashBoxEndpoint | null;
    endpoint: string;
    endpoints: StashBoxEndpoint[];
    name: string;
    onAdd: () => void;
    onDelete: (endpoint: StashBoxEndpoint) => void;
    onEdit: (endpoint: StashBoxEndpoint) => void;
    onSave: () => void;
    onTest: (endpoint: StashBoxEndpoint) => void;
    onToggleEnabled: (endpoint: StashBoxEndpoint) => void;
    saving: boolean;
    showForm: boolean;
    testingId: string | null;
    testResult: StashBoxTestResult;
  }

  let {
    apiKey = $bindable(),
    editingEndpoint,
    endpoint = $bindable(),
    endpoints,
    name = $bindable(),
    onAdd,
    onDelete,
    onEdit,
    onSave,
    onTest,
    onToggleEnabled,
    saving,
    showForm = $bindable(),
    testingId,
    testResult,
  }: Props = $props();

  const endpointPresets = [
    { label: "StashDB", url: "https://stashdb.org/graphql" },
    { label: "FansDB", url: "https://fansdb.cc/graphql" },
    { label: "PMVStash", url: "https://pmvstash.org/graphql" },
    { label: "ThePornDB", url: "https://theporndb.net/graphql" },
  ];

  function closeForm() {
    showForm = false;
  }

  function selectPreset(preset: { label: string; url: string }) {
    endpoint = preset.url;
    if (!name) name = preset.label;
  }
</script>

<section class="space-y-2">
  <div class="flex items-center justify-between px-1">
    <p class="text-text-muted text-[0.72rem]">
      Connect to StashDB, ThePornDB, FansDB, and other Stash-Box protocol servers
    </p>
    <Button
      variant="ghost"
      size="sm"
      onclick={onAdd}
      class="h-auto gap-1 px-2 py-1 text-[0.68rem] text-text-accent hover:bg-accent-950/60"
    >
      <Plus class="h-3 w-3" />Add Endpoint
    </Button>
  </div>

  {#if endpoints.length === 0 && !showForm}
    <div class="empty-rack-slot p-6 text-center">
      <Plug class="h-8 w-8 text-text-disabled mx-auto mb-3" />
      <p class="text-[0.75rem] text-text-disabled">
        No endpoints configured. Add one to enable fingerprint-based identification.
      </p>
    </div>
  {/if}

  {#each endpoints as item (item.id)}
    {@const result = testResult}
    <div class="surface-card no-lift p-3.5">
      <div class="flex items-center justify-between gap-3">
        <div class="min-w-0 flex-1">
          <div class="flex items-center gap-2 flex-wrap">
            <span class="text-[0.82rem] font-medium truncate">{item.name}</span>
            <span class="tag-chip text-[0.55rem] bg-status-error/10 text-status-error-text border border-status-error/20">NSFW</span>
            {#if !item.enabled}
              <Badge>
                Disabled
              </Badge>
            {/if}
            {#if result && result.id === item.id}
              <Badge variant={result.valid ? "success" : "error"}>
                {#if result.valid}
                  <Check class="h-2.5 w-2.5" />Connected
                {:else}
                  <AlertCircle class="h-2.5 w-2.5" />{result.error ?? "Failed"}
                {/if}
              </Badge>
            {/if}
          </div>
          <p class="text-[0.65rem] text-text-disabled truncate mt-0.5">
            {item.endpoint} · Key: {item.apiKeyPreview}
          </p>
        </div>
        <div class="flex items-center gap-1 shrink-0">
          <button
            onclick={() => onTest(item)}
            disabled={testingId === item.id}
            aria-label="Test connection"
            class="p-1.5 rounded-xs text-text-muted transition-colors hover:bg-surface-2 hover:text-text-primary"
          >
            {#if testingId === item.id}
              <Loader2 class="h-3.5 w-3.5 animate-spin text-accent-400" />
            {:else}
              <RefreshCw class="h-3.5 w-3.5" />
            {/if}
          </button>
          <button
            onclick={() => onEdit(item)}
            aria-label="Edit"
            class="p-1.5 rounded-xs text-text-muted transition-colors hover:bg-surface-2 hover:text-text-primary"
          >
            <Pencil class="h-3.5 w-3.5" />
          </button>
          <button
            onclick={() => onToggleEnabled(item)}
            aria-label={item.enabled ? "Disable" : "Enable"}
            class="p-1.5 rounded-xs text-text-muted transition-colors hover:bg-surface-2 hover:text-text-primary"
          >
            {#if item.enabled}
              <ToggleRight class="h-3.5 w-3.5 text-text-accent" />
            {:else}
              <ToggleLeft class="h-3.5 w-3.5" />
            {/if}
          </button>
          <button
            onclick={() => onDelete(item)}
            aria-label="Remove"
            class="p-1.5 rounded-xs text-text-muted transition-colors hover:bg-status-error/10 hover:text-status-error-text"
          >
            <Trash2 class="h-3.5 w-3.5" />
          </button>
        </div>
      </div>
    </div>
  {/each}

  {#if showForm}
    <div class="surface-well space-y-3 border border-border-accent/30 p-4">
      <div class="flex items-center justify-between">
        <h4 class="text-[0.78rem] font-medium">
          {editingEndpoint ? "Edit Endpoint" : "Add Stash-Box Endpoint"}
        </h4>
        <button
          onclick={closeForm}
          aria-label="Close"
          class="p-1 text-text-disabled transition-colors hover:text-text-muted"
        >
          <X class="h-3.5 w-3.5" />
        </button>
      </div>
      <div class="grid gap-2.5">
        <div>
          <label for="sb-name" class="text-[0.65rem] text-text-disabled block mb-1">Name</label>
          <input
            id="sb-name"
            type="text"
            bind:value={name}
            placeholder="StashDB"
            class="control-input py-1.5"
          />
        </div>
        <div>
          <label for="sb-endpoint" class="text-[0.65rem] text-text-disabled block mb-1">GraphQL Endpoint</label>
          <input
            id="sb-endpoint"
            type="text"
            bind:value={endpoint}
            placeholder="https://stashdb.org/graphql"
            class="control-input py-1.5"
          />
          <div class="flex gap-1.5 mt-1.5 flex-wrap">
            {#each endpointPresets as preset (preset.url)}
              <button
                onclick={() => selectPreset(preset)}
                class="border border-border-subtle rounded-xs px-1.5 py-0.5 text-[0.6rem] text-text-disabled transition-colors hover:border-border-default hover:text-text-muted"
              >
                {preset.label}
              </button>
            {/each}
          </div>
        </div>
        <div>
          <label for="sb-apikey" class="text-[0.65rem] text-text-disabled block mb-1">
            API Key
            {#if editingEndpoint}
              <span class="text-text-disabled">(leave blank to keep current)</span>
            {/if}
          </label>
          <input
            id="sb-apikey"
            type="password"
            bind:value={apiKey}
            placeholder={editingEndpoint ? "••••••••" : "Paste your API key"}
            class="control-input py-1.5 font-mono"
          />
        </div>
      </div>
      <div class="flex items-center justify-end gap-2 pt-1">
        <Button variant="ghost" size="sm" onclick={closeForm} class="h-auto px-3 py-1.5 text-[0.72rem]">
          Cancel
        </Button>
        <Button
          variant="primary"
          size="sm"
          disabled={saving || !name || !endpoint}
          onclick={onSave}
          class="h-auto gap-1.5 px-3 py-1.5 text-[0.72rem]"
        >
          {#if saving}
            <Loader2 class="h-3 w-3 animate-spin" />
          {:else}
            <Save class="h-3 w-3" />
          {/if}
          {editingEndpoint ? "Update" : "Save"}
        </Button>
      </div>
    </div>
  {/if}
</section>
