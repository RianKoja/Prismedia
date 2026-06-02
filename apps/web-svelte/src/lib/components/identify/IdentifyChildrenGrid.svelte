<script lang="ts">
  import { Check, Loader2, ScanSearch, X } from "@lucide/svelte";
  import { cn } from "@prismedia/ui-svelte";
  import { assetUrl } from "$lib/api/orval-fetch";
  import type { EntityMetadataProposal } from "$lib/api/identify-types";
  import type { StructuralChildEntity } from "$lib/components/identify-review";
  import { useIdentifyStore } from "./identify-store.svelte";

  interface Props {
    childEntities: StructuralChildEntity[];
    proposal: EntityMetadataProposal;
    /** Called when a matched (or candidate) child is activated, to drill into its own review. */
    onWalkChild?: (child: EntityMetadataProposal) => void;
  }

  let { childEntities, proposal, onWalkChild }: Props = $props();

  const store = useIdentifyStore();

  function matchedProposal(childId: string): EntityMetadataProposal | null {
    return (proposal.children ?? []).find((child) => child.targetEntityId === childId) ?? null;
  }

  function statusOf(childId: string) {
    return store.childIdentify[childId]?.status ?? "queued";
  }

  function coverFor(childId: string, fallback: string | null): string | undefined {
    const matched = matchedProposal(childId);
    const image = matched?.images?.find((img) => img.kind === "cover" || img.kind === "poster" || img.kind === "thumbnail");
    return assetUrl(image?.url ?? fallback ?? undefined) || undefined;
  }
</script>

<div class="grid grid-cols-2 gap-2 p-3.5 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5">
  {#each childEntities as child (child.id)}
    {@const matched = matchedProposal(child.id)}
    {@const status = matched ? "matched" : statusOf(child.id)}
    {@const cover = coverFor(child.id, child.coverUrl)}
    {@const busy = status === "queued" || status === "loading"}
    {@const selected = matched ? store.isReviewProposalSelected(matched.proposalId) : false}
    <div class={cn("child-tile", selected && "is-selected", busy && "is-busy")}>
      <div class="child-cover-wrap">
        <button
          type="button"
          class="child-cover"
          onclick={() => (matched ? onWalkChild?.(matched) : status === "candidates" ? store.reidentifyChild(child.id) : undefined)}
          aria-label={matched ? `Review ${matched.patch?.title ?? child.title}` : child.title}
        >
          {#if cover}
            <img src={cover} alt={child.title} loading="lazy" />
          {:else}
            <div class="child-cover-empty"></div>
          {/if}

          {#if status === "loading"}
            <div class="child-overlay"><Loader2 class="h-5 w-5 animate-spin" /><span>Identifying…</span></div>
          {:else if status === "queued"}
            <div class="child-overlay child-overlay-muted"><Loader2 class="h-4 w-4" /><span>Queued</span></div>
          {:else if status === "candidates"}
            <div class="child-badge child-badge-warn">{store.childIdentify[child.id]?.candidateCount ?? ""} matches</div>
          {:else if status === "error"}
            <div class="child-badge child-badge-error">Failed</div>
          {:else if status === "cancelled" || status === "none"}
            <div class="child-badge child-badge-muted">{status === "cancelled" ? "Skipped" : "No match"}</div>
          {/if}
        </button>

        {#if status === "matched"}
          <button
            type="button"
            class={cn("child-select", selected && "is-on")}
            onclick={() => matched && store.setReviewProposalSelected(matched.proposalId, !selected)}
            aria-label={selected ? "Deselect" : "Select"}
          >
            {#if selected}<Check class="h-3.5 w-3.5" />{/if}
          </button>
        {/if}
      </div>

      <div class="child-title" title={matched?.patch?.title ?? child.title}>{matched?.patch?.title ?? child.title}</div>

      <div class="child-actions">
        {#if busy}
          <button type="button" class="child-action" onclick={() => store.cancelChild(child.id)}>
            <X class="h-3 w-3" /> Cancel
          </button>
        {:else if status === "cancelled" || status === "error" || status === "none"}
          <button type="button" class="child-action child-action-accent" onclick={() => store.reidentifyChild(child.id)}>
            <ScanSearch class="h-3 w-3" /> Identify
          </button>
        {/if}
      </div>
    </div>
  {/each}
</div>

<style>
  .child-tile { display: grid; gap: 0.3rem; }
  .child-cover-wrap { position: relative; }
  .child-cover { position: relative; display: block; width: 100%; aspect-ratio: 1 / 1; overflow: hidden; border-radius: var(--radius-sm, 6px); border: 1px solid var(--color-border-subtle, #1c2235); background: var(--color-surface-2, #101420); cursor: pointer; }
  .child-cover img { width: 100%; height: 100%; object-fit: cover; }
  .child-cover-empty { width: 100%; height: 100%; background: linear-gradient(135deg, #141925, #0d1119); }
  .is-selected .child-cover { border-color: var(--color-border-accent-strong, #d59a2a); box-shadow: 0 0 0 1px var(--color-border-accent-strong, #d59a2a); }
  .is-busy .child-cover { opacity: 0.92; }
  .child-overlay { position: absolute; inset: 0; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 0.3rem; background: color-mix(in srgb, #060810 60%, transparent); color: var(--color-text-accent, #f2c26a); font-size: 0.66rem; }
  .child-overlay-muted { color: var(--color-text-muted, #8a93a6); }
  .child-badge { position: absolute; left: 0.3rem; bottom: 0.3rem; padding: 0.1rem 0.35rem; border-radius: 3px; font-family: var(--font-mono, monospace); font-size: 0.6rem; font-weight: 600; }
  .child-badge-warn { background: color-mix(in srgb, #d59a2a 22%, #0c0f15); color: var(--color-text-accent, #f2c26a); }
  .child-badge-error { background: color-mix(in srgb, #ef4444 25%, #0c0f15); color: #fca5a5; }
  .child-badge-muted { background: var(--color-surface-3, #151a28); color: var(--color-text-muted, #8a93a6); }
  .child-select { position: absolute; right: 0.3rem; top: 0.3rem; width: 1.15rem; height: 1.15rem; display: flex; align-items: center; justify-content: center; border-radius: 4px; border: 1px solid var(--color-border, #1c2235); background: color-mix(in srgb, #060810 55%, transparent); color: var(--color-text-accent, #f2c26a); cursor: pointer; }
  .child-select.is-on { background: var(--color-border-accent-strong, #d59a2a); border-color: var(--color-border-accent-strong, #d59a2a); color: #0c0f15; }
  .child-title { font-size: 0.72rem; line-height: 1.2; color: var(--color-text-primary, #f2eed8); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .child-actions { min-height: 1.1rem; }
  .child-action { display: inline-flex; align-items: center; gap: 0.2rem; font-size: 0.64rem; color: var(--color-text-muted, #8a93a6); background: none; border: none; cursor: pointer; padding: 0; }
  .child-action-accent { color: var(--color-text-accent, #f2c26a); }
  .child-action:hover { opacity: 0.8; }
</style>
