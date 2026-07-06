<script lang="ts">
  import { Eye, EyeOff } from "@lucide/svelte";
  import type { Component } from "svelte";
  import { cn } from "@prismedia/ui-svelte";
  import FormField from "./FormField.svelte";

  interface Props {
    value: string;
    onChange: (value: string) => void;
    label?: string;
    icon?: Component;
    placeholder?: string;
    helper?: string;
    error?: string;
    required?: boolean;
    disabled?: boolean;
    autocomplete?: AutoFill;
  }

  let {
    value,
    onChange,
    label,
    icon,
    placeholder,
    helper,
    error,
    required = false,
    disabled = false,
    autocomplete = "current-password",
  }: Props = $props();

  const id = `password-${Math.random().toString(36).slice(2, 9)}`;
  let revealed = $state(false);
</script>

<FormField {label} {icon} {helper} {error} {required} htmlFor={id}>
  <div class="relative">
    <input
      {id}
      type={revealed ? "text" : "password"}
      {disabled}
      {placeholder}
      {autocomplete}
      {value}
      oninput={(e) => onChange((e.currentTarget as HTMLInputElement).value)}
      aria-invalid={error ? "true" : undefined}
      class={cn(
        "w-full rounded-xs border border-border-subtle bg-surface-2 py-2 pr-10 pl-3 text-sm text-text-primary shadow-[inset_0_2px_8px_rgba(0,0,0,0.30)] transition-colors",
        "placeholder:text-text-disabled",
        "focus:border-border-accent focus:ring-1 focus:ring-accent-500/40 focus:outline-none",
        error && "border-error/60",
        disabled && "cursor-not-allowed opacity-60",
      )}
    />
    <button
      type="button"
      class="absolute inset-y-0 right-0 flex w-10 items-center justify-center text-text-disabled transition-colors hover:text-text-secondary"
      aria-label={revealed ? "Hide password" : "Show password"}
      aria-pressed={revealed}
      onclick={() => (revealed = !revealed)}
      tabindex={-1}
    >
      {#if revealed}
        <EyeOff class="size-4" />
      {:else}
        <Eye class="size-4" />
      {/if}
    </button>
  </div>
</FormField>
