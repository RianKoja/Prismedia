<script lang="ts">
  import { onDestroy, onMount } from "svelte";
  import {
    AlertCircle,
    Check,
    ChevronRight,
    Images,
    KeyRound,
    Loader2,
    PanelRightClose,
    RefreshCw,
    ScanSearch,
    Search,
    X,
  } from "@lucide/svelte";
  import { cn } from "@prismedia/ui-svelte";
  import {
    applyIdentifyProposal,
    closeBulkIdentifySession,
    fetchBulkIdentifySession,
    fetchIdentifyEntities,
    fetchIdentifyProviders,
    fetchPluginProviders,
    identifyEntity,
    installPlugin,
    savePluginAuth,
    startBulkIdentify,
    type EntityMetadataProposal,
    type EntitySearchCandidate,
    type ImageCandidate,
    type IdentifyBulkSession,
    type PluginProvider,
  } from "$lib/api/identify";
  import { reviewChildProposals } from "$lib/components/identify-review";
  import type { EntityCard } from "$lib/api/prismedia";

  type IdentifyKind = "video" | "video-series";

  const FIELD_KEYS = [
    "title",
    "description",
    "externalIds",
    "urls",
    "tags",
    "studio",
    "credits",
    "dates",
    "stats",
    "positions",
    "classification",
    "images",
  ] as const;

  const FIELD_LABELS: Record<(typeof FIELD_KEYS)[number], string> = {
    title: "Title",
    description: "Description",
    externalIds: "Provider IDs",
    urls: "Links",
    tags: "Tags",
    studio: "Studio",
    credits: "Credits",
    dates: "Dates",
    stats: "Stats",
    positions: "Positions",
    classification: "Classification",
    images: "Artwork",
  };

  const KIND_LABELS: Record<IdentifyKind, string> = {
    video: "Movies",
    "video-series": "Series",
  };

  let kind = $state<IdentifyKind>("video");
  let query = $state("");
  let providers = $state<PluginProvider[]>([]);
  let providerId = $state<string | null>(null);
  let entities = $state<EntityCard[]>([]);
  let selectedEntityIds = $state<string[]>([]);
  let activeEntity = $state<EntityCard | null>(null);
  let proposal = $state<EntityMetadataProposal | null>(null);
  let selectedFields = $state<Record<string, boolean>>({});
  let selectedImages = $state<Record<string, string | null>>({});
  let loading = $state(true);
  let identifyingId = $state<string | null>(null);
  let applying = $state(false);
  let authValues = $state<Record<string, string>>({});
  let authSaving = $state<string | null>(null);
  let bulkSession = $state<IdentifyBulkSession | null>(null);
  let bulkStarting = $state(false);
  let error = $state<string | null>(null);
  let message = $state<string | null>(null);
  let pollTimer: ReturnType<typeof setTimeout> | null = null;

  const availableProviders = $derived(
    providers.filter((provider) =>
      provider.supports.some((support) => support.entityKind === kind),
    ),
  );
  const selectedProvider = $derived.by(() =>
    availableProviders.find((provider) => provider.id === providerId) ?? availableProviders[0] ?? null,
  );
  const canIdentify = $derived(Boolean(selectedProvider && activeEntity && !selectedProvider.missingAuthKeys.length));
  const bulkResults = $derived(bulkSession?.results ?? []);
  const proposalReviewChildren = $derived(proposal ? reviewChildProposals(proposal) : []);

  onMount(() => {
    void load();
  });

  onDestroy(() => {
    if (pollTimer) clearTimeout(pollTimer);
  });

  async function load() {
    loading = true;
    error = null;
    try {
      const [providerRows, entityRows] = await Promise.all([
        fetchPluginProviders(),
        fetchIdentifyEntities(kind, query),
      ]);
      providers = providerRows;
      entities = entityRows.items;
      selectedEntityIds = selectedEntityIds.filter((id) =>
        entityRows.items.some((entity) => entity.id === id),
      );
    } catch (err) {
      error = readError(err);
    } finally {
      loading = false;
    }
  }

  async function refreshEntities() {
    error = null;
    try {
      const entityRows = await fetchIdentifyEntities(kind, query);
      entities = entityRows.items;
      selectedEntityIds = selectedEntityIds.filter((id) =>
        entityRows.items.some((entity) => entity.id === id),
      );
    } catch (err) {
      error = readError(err);
    }
  }

  async function install(provider: PluginProvider) {
    error = null;
    try {
      const installed = await installPlugin(provider.id);
      providers = providers.map((row) => (row.id === installed.id ? installed : row));
      providerId = installed.id;
      message = `${installed.name} installed`;
    } catch (err) {
      error = readError(err);
    }
  }

  async function saveAuth(provider: PluginProvider) {
    authSaving = provider.id;
    error = null;
    try {
      const values: Record<string, string | null> = {};
      for (const field of provider.auth) {
        values[field.key] = authValues[`${provider.id}:${field.key}`] ?? null;
      }

      await savePluginAuth(provider.id, values);
      providers = await fetchIdentifyProviders(kind);
      providerId = provider.id;
      message = `${provider.name} credentials saved`;
    } catch (err) {
      error = readError(err);
    } finally {
      authSaving = null;
    }
  }

  async function runIdentify(entity: EntityCard, candidate?: EntitySearchCandidate) {
    const provider = selectedProvider;
    if (!provider) return;
    activeEntity = entity;
    identifyingId = entity.id;
    error = null;
    try {
      const result = await identifyEntity(entity.id, provider.id, candidate
        ? { externalIds: candidate.externalIds }
        : undefined);
      openProposal(result);
    } catch (err) {
      error = readError(err);
    } finally {
      identifyingId = null;
    }
  }

  function openProposal(result: EntityMetadataProposal) {
    proposal = result;
    selectedFields = Object.fromEntries(
      FIELD_KEYS.map((key) => [key, hasField(result, key)]),
    );
    selectedImages = defaultImageSelection(result.images);
  }

  async function applyProposal(closeAfter = true) {
    if (!activeEntity || !proposal) return;
    applying = true;
    error = null;
    try {
      const fields = Object.entries(selectedFields)
        .filter(([, enabled]) => enabled)
        .map(([field]) => field);
      const updated = await applyIdentifyProposal(
        activeEntity.id,
        proposal,
        fields,
        selectedImages,
      );
      entities = entities.map((entity) => (entity.id === updated.id ? updated : entity));
      activeEntity = updated;
      message = `${updated.title} updated`;
      if (closeAfter) closeReview();
    } catch (err) {
      error = readError(err);
    } finally {
      applying = false;
    }
  }

  async function startBulk() {
    const provider = selectedProvider;
    if (!provider || selectedEntityIds.length === 0) return;
    bulkStarting = true;
    error = null;
    try {
      bulkSession = await startBulkIdentify(provider.id, selectedEntityIds);
      schedulePoll();
    } catch (err) {
      error = readError(err);
    } finally {
      bulkStarting = false;
    }
  }

  function schedulePoll() {
    if (!bulkSession || bulkSession.status === "completed") return;
    pollTimer = setTimeout(async () => {
      if (!bulkSession) return;
      try {
        bulkSession = await fetchBulkIdentifySession(bulkSession.id);
      } catch (err) {
        error = readError(err);
        return;
      }
      schedulePoll();
    }, 1200);
  }

  async function closeBulk() {
    if (!bulkSession) return;
    const id = bulkSession.id;
    bulkSession = null;
    if (pollTimer) clearTimeout(pollTimer);
    pollTimer = null;
    await closeBulkIdentifySession(id).catch(() => undefined);
  }

  function reviewBulkResult(result: EntityMetadataProposal, entityId: string) {
    const entity = entities.find((row) => row.id === entityId);
    if (!entity) return;
    activeEntity = entity;
    openProposal(result);
  }

  function rerunCandidate(candidate: EntitySearchCandidate) {
    if (!activeEntity) return;
    void runIdentify(activeEntity, candidate);
  }

  function toggleSelected(entityId: string) {
    selectedEntityIds = selectedEntityIds.includes(entityId)
      ? selectedEntityIds.filter((id) => id !== entityId)
      : [...selectedEntityIds, entityId];
  }

  function closeReview() {
    proposal = null;
    activeEntity = null;
    selectedFields = {};
    selectedImages = {};
  }

  function fieldValue(result: EntityMetadataProposal, field: string): string {
    const patch = result.patch;
    if (field === "title") return patch.title ?? "";
    if (field === "description") return patch.description ?? "";
    if (field === "externalIds") return entries(patch.externalIds).join(", ");
    if (field === "urls") return patch.urls.join(", ");
    if (field === "tags") return patch.tags.join(", ");
    if (field === "studio") return patch.studio ?? "";
    if (field === "credits") return patch.credits.map((credit) => credit.character ? `${credit.name} as ${credit.character}` : credit.name).join(", ");
    if (field === "dates") return entries(patch.dates).join(", ");
    if (field === "stats") return entries(patch.stats).join(", ");
    if (field === "positions") return entries(patch.positions).join(", ");
    if (field === "classification") return patch.classification ?? "";
    if (field === "images") return result.images.length > 0 ? `${result.images.length} candidate${result.images.length === 1 ? "" : "s"}` : "";
    return "";
  }

  function hasField(result: EntityMetadataProposal, field: string): boolean {
    const value = fieldValue(result, field);
    return value.trim().length > 0;
  }

  function imageGroups(images: ImageCandidate[]): Array<{ kind: string; images: ImageCandidate[] }> {
    const groups: Record<string, ImageCandidate[]> = {};
    for (const image of images) {
      groups[image.kind] = [...(groups[image.kind] ?? []), image];
    }
    return Object.entries(groups).map(([groupKind, rows]) => ({ kind: groupKind, images: rows }));
  }

  function defaultImageSelection(images: ImageCandidate[]): Record<string, string | null> {
    const selected: Record<string, string | null> = {};
    for (const group of imageGroups(images)) {
      selected[group.kind] = group.images[0]?.url ?? null;
    }
    return selected;
  }

  function entries(record: Record<string, string | number>): string[] {
    return Object.entries(record).map(([key, value]) => `${key}: ${value}`);
  }

  function readError(err: unknown): string {
    if (!(err instanceof Error)) return "Request failed";
    try {
      const parsed = JSON.parse(err.message) as { message?: string; detail?: string };
      return parsed.message ?? parsed.detail ?? err.message;
    } catch {
      return err.message;
    }
  }
</script>

<svelte:head>
  <title>Identify · Prismedia</title>
</svelte:head>

<div class="space-y-4 pb-16">
  <!-- ── Header ── -->
  <div class="flex items-start justify-between gap-4">
    <div>
      <h1 class="flex items-center gap-2.5">
        <ScanSearch class="h-5 w-5 text-text-accent" />
        Identify
      </h1>
      <p class="mt-1 text-[0.82rem] text-text-muted">
        Provider IDs first, title search only when needed.
      </p>
    </div>
    <button
      type="button"
      onclick={() => void load()}
      class="flex h-9 w-9 items-center justify-center rounded-xs border border-border-default bg-surface-2 text-text-muted transition-colors hover:bg-surface-3 hover:text-text-primary"
      aria-label="Refresh identify data"
    >
      {#if loading}
        <Loader2 class="h-4 w-4 animate-spin" />
      {:else}
        <RefreshCw class="h-4 w-4" />
      {/if}
    </button>
  </div>

  <!-- ── Notices ── -->
  {#if error}
    <div
      class="flex items-center gap-2.5 rounded-xs border border-status-error/40 bg-surface-1 px-3 py-2.5 text-[0.82rem] text-text-primary"
      role="alert"
    >
      <AlertCircle class="h-4 w-4 shrink-0 text-status-error-text" />
      <span class="min-w-0 flex-1">{error}</span>
      <button
        type="button"
        class="shrink-0 text-text-disabled transition-colors hover:text-text-primary"
        onclick={() => (error = null)}
        aria-label="Dismiss error"
      >
        <X class="h-3.5 w-3.5" />
      </button>
    </div>
  {/if}

  {#if message}
    <div
      class="flex items-center gap-2.5 rounded-xs border border-border-accent bg-surface-1 px-3 py-2.5 text-[0.82rem] text-text-primary"
    >
      <Check class="h-4 w-4 shrink-0 text-text-accent" />
      <span class="min-w-0 flex-1">{message}</span>
      <button
        type="button"
        class="shrink-0 text-text-disabled transition-colors hover:text-text-primary"
        onclick={() => (message = null)}
        aria-label="Dismiss message"
      >
        <X class="h-3.5 w-3.5" />
      </button>
    </div>
  {/if}

  <!-- ── Controls: kind toggle + search ── -->
  <div class="surface-panel grid grid-cols-1 items-center gap-3 p-3 md:grid-cols-[auto_minmax(16rem,1fr)_auto]">
    <div class="grid grid-cols-2 rounded-xs border border-border-default" aria-label="Entity kind">
      {#each Object.entries(KIND_LABELS) as [kindCode, label] (kindCode)}
        <button
          type="button"
          class={cn(
            "border-0 border-r border-border-default px-3 py-2 text-[0.78rem] last:border-r-0",
            "transition-colors",
            kind === kindCode
              ? "bg-surface-3 text-text-accent shadow-[inset_0_0_0_1px_rgba(242,194,106,0.5)]"
              : "bg-transparent text-text-muted hover:bg-surface-2 hover:text-text-primary",
          )}
          onclick={() => {
            kind = kindCode as IdentifyKind;
            selectedEntityIds = [];
            activeEntity = null;
            proposal = null;
            void load();
          }}
        >
          {label}
        </button>
      {/each}
    </div>

    <label class="control-input flex items-center gap-2">
      <Search class="h-4 w-4 shrink-0 text-text-disabled" />
      <input
        class="allow-compact-input-text min-w-0 flex-1 border-0 bg-transparent text-[0.85rem] text-text-primary outline-none"
        placeholder="Search"
        bind:value={query}
        onkeydown={(event) => {
          if (event.key === "Enter") void refreshEntities();
        }}
      />
    </label>

    <button
      type="button"
      class="inline-flex h-9 items-center gap-1.5 rounded-xs border border-border-default bg-surface-2 px-3 text-[0.78rem] text-text-primary transition-colors hover:bg-surface-3"
      onclick={() => void refreshEntities()}
    >
      <Search class="h-4 w-4" />
      Search
    </button>
  </div>

  <!-- ── Provider strip ── -->
  <div class="surface-panel grid grid-cols-1 gap-2.5 p-3 md:grid-cols-[repeat(auto-fit,minmax(18rem,1fr))]">
    {#if availableProviders.length === 0}
      <p class="text-[0.82rem] text-text-muted">No providers for {KIND_LABELS[kind]}.</p>
    {:else}
      {#each availableProviders as provider (provider.id)}
        <article
          class={cn(
            "surface-card rounded-xs",
            provider.id === selectedProvider?.id && "active",
          )}
        >
          <button
            type="button"
            class="flex w-full items-center justify-between border-0 bg-transparent px-3 py-2.5 text-left text-text-primary"
            onclick={() => (providerId = provider.id)}
          >
            <span class="text-[0.82rem] font-medium">{provider.name}</span>
            <small class="text-[0.7rem] text-text-muted">v{provider.version}</small>
          </button>
          <div class="flex flex-col gap-2 px-3 pb-3">
            {#if !provider.installed || !provider.enabled}
              <button
                type="button"
                class="inline-flex h-7 items-center justify-center rounded-xs border border-border-default bg-surface-2 px-2 text-[0.72rem] text-text-primary transition-colors hover:bg-surface-3"
                onclick={() => void install(provider)}
              >
                Install
              </button>
            {/if}
            {#if provider.auth.length > 0}
              <div class="flex items-center gap-2 rounded-xs border border-border-default bg-surface-1 p-1.5">
                <KeyRound class="h-3.5 w-3.5 shrink-0 text-text-disabled" />
                {#each provider.auth as field (field.key)}
                  <input
                    type="password"
                    class="allow-compact-input-text min-w-0 flex-1 border-0 bg-transparent text-[0.78rem] text-text-primary outline-none"
                    placeholder={field.label}
                    bind:value={authValues[`${provider.id}:${field.key}`]}
                  />
                {/each}
                <button
                  type="button"
                  class="inline-flex h-7 items-center justify-center rounded-xs border border-border-default bg-surface-2 px-2 text-[0.72rem] text-text-primary transition-colors hover:bg-surface-3 disabled:opacity-40"
                  disabled={authSaving === provider.id}
                  onclick={() => void saveAuth(provider)}
                >
                  {#if authSaving === provider.id}
                    <Loader2 class="h-3.5 w-3.5 animate-spin" />
                  {:else}
                    Save
                  {/if}
                </button>
              </div>
            {/if}
          </div>
        </article>
      {/each}
    {/if}
  </div>

  <!-- ── Bulk bar ── -->
  <div class="flex items-center justify-between rounded-xs border border-border-default bg-surface-1 px-3 py-2.5 text-[0.78rem] text-text-muted">
    <span>{selectedEntityIds.length} selected</span>
    <button
      type="button"
      class="inline-flex h-9 items-center gap-1.5 rounded-xs border border-border-default bg-surface-2 px-3 text-[0.78rem] text-text-primary transition-colors hover:bg-surface-3 disabled:cursor-not-allowed disabled:opacity-40"
      disabled={!selectedProvider || selectedEntityIds.length === 0 || bulkStarting}
      onclick={() => void startBulk()}
    >
      {#if bulkStarting}
        <Loader2 class="h-4 w-4 animate-spin" />
      {:else}
        <ScanSearch class="h-4 w-4" />
      {/if}
      Bulk Identify
    </button>
  </div>

  <!-- ── Entity table ── -->
  <div class="grid gap-2">
    {#each entities as entity (entity.id)}
      <article
        class={cn(
          "grid grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-3 rounded-xs border border-border-default bg-surface-1 p-2.5 transition-shadow",
          activeEntity?.id === entity.id &&
            "shadow-[inset_0_0_0_1px_rgba(242,194,106,0.5),0_0_18px_rgba(242,194,106,0.12)]",
        )}
      >
        <input
          type="checkbox"
          class="h-4 w-4 accent-accent-500"
          checked={selectedEntityIds.includes(entity.id)}
          aria-label={`Select ${entity.title}`}
          onchange={() => toggleSelected(entity.id)}
        />
        <button
          type="button"
          class="flex min-w-0 flex-col gap-0.5 border-0 bg-transparent text-left text-text-primary"
          onclick={() => (activeEntity = entity)}
        >
          <span class="truncate text-[0.88rem]">{entity.title}</span>
          <small class="text-[0.7rem] text-text-muted">{entity.kind}</small>
        </button>
        <button
          type="button"
          class="inline-flex h-9 items-center gap-1.5 rounded-xs border border-border-default bg-surface-2 px-3 text-[0.78rem] text-text-primary transition-colors hover:bg-surface-3 disabled:cursor-not-allowed disabled:opacity-40"
          disabled={!selectedProvider || identifyingId === entity.id || selectedProvider.missingAuthKeys.length > 0}
          onclick={() => void runIdentify(entity)}
        >
          {#if identifyingId === entity.id}
            <Loader2 class="h-4 w-4 animate-spin" />
          {:else}
            <ChevronRight class="h-4 w-4" />
          {/if}
          Identify
        </button>
      </article>
    {/each}
  </div>

  <!-- ── Bulk results ── -->
  {#if bulkSession}
    <section class="surface-panel p-3.5">
      <header class="flex items-center justify-between gap-4">
        <div>
          <h2>Bulk Session</h2>
          <p class="mt-0.5 text-[0.78rem] text-text-muted">
            {bulkSession.status} · {bulkResults.length}/{bulkSession.entityIds.length}
          </p>
        </div>
        <button
          type="button"
          class="flex h-9 w-9 items-center justify-center rounded-xs border border-border-default bg-surface-2 text-text-muted transition-colors hover:bg-surface-3 hover:text-text-primary"
          onclick={() => void closeBulk()}
          aria-label="Close bulk session"
        >
          <X class="h-4 w-4" />
        </button>
      </header>
      <div class="mt-3 grid gap-1.5">
        {#each bulkResults as result (result.entityId)}
          <button
            type="button"
            class="flex items-center justify-between gap-4 rounded-xs border border-border-default bg-surface-2 px-3 py-2 text-left text-text-primary transition-colors hover:bg-surface-3 disabled:cursor-not-allowed disabled:opacity-40"
            disabled={!result.response.ok || !result.response.result}
            onclick={() => result.response.result && reviewBulkResult(result.response.result, result.entityId)}
          >
            <span class="text-[0.82rem]">{entities.find((entity) => entity.id === result.entityId)?.title ?? result.entityId}</span>
            <small class="text-[0.7rem] text-text-muted">{result.response.ok ? result.response.result?.matchReason : result.response.error}</small>
          </button>
        {/each}
      </div>
    </section>
  {/if}
</div>

<!-- ── Review drawer ── -->
{#if proposal && activeEntity}
  <aside
    class="fixed inset-y-0 right-0 z-60 flex w-[min(100vw,520px)] flex-col gap-4 overflow-auto border-l border-border-default p-4"
    style="background: rgba(9,12,18,0.94); backdrop-filter: blur(18px); -webkit-backdrop-filter: blur(18px);"
    aria-label="Review identify result"
  >
    <header class="flex items-center justify-between gap-4">
      <div>
        <p class="text-[0.72rem] text-text-muted">{proposal.provider} · {proposal.matchReason ?? "match"}</p>
        <h2>{proposal.patch.title ?? activeEntity.title}</h2>
      </div>
      <button
        type="button"
        class="flex h-9 w-9 items-center justify-center rounded-xs border border-border-default bg-surface-2 text-text-muted transition-colors hover:bg-surface-3 hover:text-text-primary"
        onclick={closeReview}
        aria-label="Close review"
      >
        <PanelRightClose class="h-4 w-4" />
      </button>
    </header>

    {#if proposal.candidates.length > 1}
      <section class="grid gap-3">
        <h3 class="text-kicker">Candidates</h3>
        <div class="grid grid-cols-[repeat(auto-fill,minmax(8rem,1fr))] gap-2">
          {#each proposal.candidates as candidate (candidate.externalIds.tmdb ?? candidate.title)}
            <button
              type="button"
              class="grid gap-1.5 rounded-xs border border-border-default bg-surface-1 p-1.5 text-left text-text-primary transition-colors hover:border-border-accent hover:bg-surface-2"
              onclick={() => rerunCandidate(candidate)}
            >
              {#if candidate.posterUrl}
                <img class="aspect-[2/3] w-full rounded-xs object-cover" src={candidate.posterUrl} alt="" />
              {/if}
              <span class="text-[0.78rem]">{candidate.title}</span>
              <small class="text-[0.68rem] text-text-muted">{candidate.year ?? ""}</small>
            </button>
          {/each}
        </div>
      </section>
    {/if}

    <section class="grid gap-3">
      <h3 class="text-kicker">Fields</h3>
      <div class="grid gap-1.5">
        {#each FIELD_KEYS as field (field)}
          {#if hasField(proposal, field)}
            <label class="grid grid-cols-[auto_7rem_minmax(0,1fr)] items-start gap-2 rounded-xs border border-border-default bg-surface-1 p-2.5">
              <input type="checkbox" class="mt-0.5 h-4 w-4 accent-accent-500" bind:checked={selectedFields[field]} />
              <span class="text-[0.78rem] text-text-primary">{FIELD_LABELS[field]}</span>
              <small class="truncate text-[0.72rem] text-text-muted">{fieldValue(proposal, field)}</small>
            </label>
          {/if}
        {/each}
      </div>
    </section>

    {#if proposal.images.length > 0}
      <section class="grid gap-3">
        <h3 class="text-kicker">
          <Images class="h-4 w-4" />
          Artwork
        </h3>
        {#each imageGroups(proposal.images) as group (group.kind)}
          <div class="grid gap-1.5">
            <p class="text-[0.72rem] font-medium uppercase tracking-wider text-text-muted">{group.kind}</p>
            <div class="grid grid-cols-[repeat(auto-fill,minmax(5rem,1fr))] gap-1.5">
              {#each group.images as image (image.url)}
                <button
                  type="button"
                  class={cn(
                    "rounded-xs border bg-surface-1 p-1 transition-all",
                    selectedImages[group.kind] === image.url
                      ? "border-border-accent-strong shadow-[0_0_16px_rgba(242,194,106,0.2)]"
                      : "border-border-default hover:border-border-accent",
                  )}
                  onclick={() => (selectedImages[group.kind] = image.url)}
                >
                  <img class="aspect-[2/3] w-full rounded-xs object-cover" src={image.url} alt="" />
                </button>
              {/each}
            </div>
          </div>
        {/each}
      </section>
    {/if}

    {#if proposalReviewChildren.length > 0}
      <section class="grid gap-3">
        <h3 class="text-kicker">Related Results</h3>
        <div class="grid gap-1.5">
          {#each proposalReviewChildren as child (child.proposalId)}
            <div class="flex items-center justify-between gap-4 rounded-xs border border-border-default bg-surface-1 px-2.5 py-2">
              <span class="text-[0.82rem] text-text-primary">{child.patch.title}</span>
              <small class="text-[0.68rem] text-text-muted">{child.targetKind}</small>
            </div>
          {/each}
        </div>
      </section>
    {/if}

    <footer
      class="sticky -bottom-4 -mx-4 -mb-4 mt-auto flex items-center justify-between gap-4 border-t border-border-default p-4"
      style="background: rgba(9,12,18,0.96);"
    >
      <button
        type="button"
        class="inline-flex h-9 items-center gap-1.5 rounded-xs border border-border-default bg-transparent px-3 text-[0.78rem] text-text-muted transition-colors hover:bg-surface-2 hover:text-text-primary"
        onclick={closeReview}
      >
        Reject
      </button>
      <button
        type="button"
        class="inline-flex h-9 items-center gap-1.5 rounded-xs border border-border-accent-strong px-3 text-[0.78rem] text-text-primary transition-all disabled:cursor-not-allowed disabled:opacity-40"
        style="background: linear-gradient(135deg, rgba(242,194,106,0.24), rgba(242,194,106,0.1)); box-shadow: 0 0 18px rgba(242,194,106,0.16);"
        disabled={!canIdentify || applying}
        onclick={() => void applyProposal()}
      >
        {#if applying}
          <Loader2 class="h-4 w-4 animate-spin" />
        {:else}
          <Check class="h-4 w-4" />
        {/if}
        Apply
      </button>
    </footer>
  </aside>
{/if}
