<script lang="ts">
  import { Ban, Loader2, Play, Square } from "@lucide/svelte";
  import { cn } from "@prismedia/ui-svelte";
  import type { RunCatalogEntry } from "$lib/jobs/run-catalog";
  import type { QueueSummary } from "$lib/jobs/models";

  interface Props {
    entry: RunCatalogEntry;
    queue: QueueSummary | undefined;
    running: boolean;
    stopping: boolean;
    clearing: boolean;
    disabled?: boolean;
    onRun: (jobType: string) => void | Promise<void>;
    onStop: (queueName: string) => void | Promise<void>;
    onClearFailures: (queueName: string) => void | Promise<void>;
  }

  let {
    entry,
    queue,
    running,
    stopping,
    clearing,
    disabled = false,
    onRun,
    onStop,
    onClearFailures,
  }: Props = $props();

  const Icon = $derived(entry.icon);
  const active = $derived(queue?.active ?? 0);
  const backlog = $derived(queue?.backlog ?? 0);
  const failed = $derived(queue?.failed ?? 0);
  const hasPressure = $derived(active > 0 || backlog > 0);
</script>

<div
  class={cn(
    "group flex items-stretch rounded-xs border border-transparent transition-all duration-fast",
    "hover:border-border-accent/30 hover:bg-surface-2/40",
    "focus-within:border-border-accent focus-within:shadow-[var(--shadow-focus-accent)]",
    failed > 0 && "border-l-2 border-l-status-error/40",
    hasPressure && failed === 0 && "border-l-2 border-l-border-accent/40",
  )}
>
  <button
    type="button"
    onclick={() => void onRun(entry.jobType)}
    disabled={disabled || running}
    class={cn(
      "flex flex-1 items-center gap-3 px-3 py-2 text-left",
      "focus-visible:outline-none",
      "disabled:cursor-not-allowed disabled:opacity-50",
    )}
    title={entry.description}
  >
    <Icon
      class={cn(
        "h-4 w-4 shrink-0 transition-colors",
        hasPressure ? "text-text-accent" : "text-text-disabled group-hover:text-text-muted",
      )}
    />
    <div class="min-w-0 flex-1">
      <div class="truncate text-[0.78rem] font-medium text-text-primary">{entry.label}</div>
      {#if hasPressure || failed > 0}
        <div class="mt-0.5 flex items-center gap-2 text-[0.62rem] tabular-nums">
          {#if active > 0}
            <span class="text-text-accent">{active} running</span>
          {/if}
          {#if backlog > 0}
            <span class="text-text-muted">{backlog} queued</span>
          {/if}
          {#if failed > 0}
            <span class="text-status-error-text">{failed} failed</span>
          {/if}
        </div>
      {/if}
    </div>
    <span
      class={cn(
        "shrink-0 transition-opacity",
        running
          ? "opacity-100"
          : "opacity-0 group-hover:opacity-70 group-focus-within:opacity-70",
      )}
    >
      {#if running}
        <Loader2 class="h-3.5 w-3.5 animate-spin text-text-accent" />
      {:else}
        <Play class="h-3.5 w-3.5 text-text-accent" />
      {/if}
    </span>
  </button>

  {#if hasPressure}
    <button
      type="button"
      onclick={() => void onStop(entry.queueName)}
      disabled={stopping}
      class="flex items-center justify-center px-2 text-text-disabled transition-colors hover:text-status-error-text disabled:opacity-40 focus-visible:outline-none focus-visible:text-status-error-text"
      title="Stop running and queued jobs in this queue"
      aria-label="Stop queue"
    >
      {#if stopping}
        <Loader2 class="h-3.5 w-3.5 animate-spin" />
      {:else}
        <Square class="h-3.5 w-3.5" />
      {/if}
    </button>
  {/if}

  {#if failed > 0}
    <button
      type="button"
      onclick={() => void onClearFailures(entry.queueName)}
      disabled={clearing}
      class="flex items-center justify-center px-2 text-status-error-text/60 transition-colors hover:text-status-error-text disabled:opacity-40 focus-visible:outline-none focus-visible:text-status-error-text"
      title="Clear failed jobs in this queue"
      aria-label="Clear failures"
    >
      {#if clearing}
        <Loader2 class="h-3.5 w-3.5 animate-spin" />
      {:else}
        <Ban class="h-3.5 w-3.5" />
      {/if}
    </button>
  {/if}
</div>
