<script lang="ts">
  import { Minus, Plus } from "@lucide/svelte";
  import { cn } from "@prismedia/ui-svelte";

  interface Props {
    label: string;
    description: string;
    value: number;
    min: number;
    max: number;
    step: number;
    class?: string;
    onChange: (value: number) => void;
  }

  let {
    label,
    description,
    value,
    min,
    max,
    step,
    class: className,
    onChange,
  }: Props = $props();

  let draftText = $state("");

  $effect(() => {
    draftText = String(value);
  });

  function commitText() {
    const parsed = parseInt(draftText, 10);
    if (Number.isNaN(parsed)) {
      draftText = String(value);
      return;
    }
    const clamped = Math.max(min, Math.min(max, Math.round(parsed)));
    draftText = String(clamped);
    onChange(clamped);
  }
</script>

<div
  class={cn(
    "surface-card no-lift flex h-full min-h-[104px] flex-col justify-between p-3.5",
    className,
  )}
>
  <div class="mb-3">
    <div class="control-label mb-1">{label}</div>
    <p class="text-[0.68rem] text-text-muted">{description}</p>
  </div>
  <div
    class="flex items-center rounded-xs bg-surface-1 border border-border-default shadow-[inset_0_2px_6px_rgba(0,0,0,0.5)]"
  >
    <button
      type="button"
      onclick={() => onChange(Math.max(min, value - step))}
      class="rounded-l-xs px-3 py-1.5 text-text-muted hover:text-text-primary hover:bg-surface-2 transition-colors border-r border-border-subtle"
      aria-label="Decrement"
    >
      <Minus class="h-3.5 w-3.5" />
    </button>
    <input
      type="text"
      inputmode="numeric"
      bind:value={draftText}
      onblur={commitText}
      onkeydown={(e) => { if (e.key === "Enter") (e.currentTarget as HTMLInputElement).blur(); }}
      class="flex-1 bg-transparent text-center font-mono text-[0.85rem] text-text-primary py-1.5 outline-none"
      aria-label={label}
    />
    <button
      type="button"
      onclick={() => onChange(Math.min(max, value + step))}
      class="rounded-r-xs px-3 py-1.5 text-text-muted hover:text-text-primary hover:bg-surface-2 transition-colors border-l border-border-subtle"
      aria-label="Increment"
    >
      <Plus class="h-3.5 w-3.5" />
    </button>
  </div>
</div>
