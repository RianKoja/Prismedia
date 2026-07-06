<script lang="ts">
  import type { Snippet } from "svelte";
  import { flyUp } from "@prismedia/ui-svelte";

  interface Props {
    title?: string;
    subtitle?: string;
    /** Wider panel for multi-column steps (setup review). */
    wide?: boolean;
    children: Snippet;
  }

  let { title, subtitle, wide = false, children }: Props = $props();
</script>

<!--
  Full-screen entrance chrome shared by /login and /setup: dark ground, brass radial
  glow, and a single glass panel. Uses the plain logo image (never the NSFW-aware
  LogoMark) because nothing NSFW may render before authentication.
-->
<div class="auth-shell relative flex min-h-dvh items-center justify-center overflow-hidden px-4 py-10">
  <div class="auth-glow" aria-hidden="true"></div>

  <div
    class={[
      "glass-3 relative z-10 w-full rounded-2xl border border-border-subtle p-8 shadow-[0_24px_80px_rgba(0,0,0,0.55)]",
      wide ? "max-w-xl" : "max-w-md",
    ]}
    transition:flyUp={{ duration: 300 }}
  >
    <div class="mb-8 flex flex-col items-center gap-3 text-center">
      <div class="auth-brand-mark">
        <img src="/brand/prismedia-logo.png" alt="" class="size-16" />
      </div>
      <h1 class="font-display text-2xl tracking-[0.3em] text-text-primary uppercase">Prismedia</h1>
      {#if title}
        <p class="font-heading text-sm text-text-secondary">{title}</p>
      {/if}
      {#if subtitle}
        <p class="max-w-sm font-mono text-xs text-text-disabled">{subtitle}</p>
      {/if}
    </div>

    {@render children()}
  </div>
</div>

<style>
  .auth-shell {
    background: var(--color-bg, #07080b);
  }

  .auth-glow {
    position: absolute;
    inset: 0;
    background:
      radial-gradient(ellipse 60% 45% at 50% 38%, rgb(244 204 134 / 0.09), transparent 70%),
      radial-gradient(ellipse 80% 60% at 50% 110%, rgb(196 154 90 / 0.06), transparent 70%);
    pointer-events: none;
  }

  .auth-brand-mark {
    position: relative;
    isolation: isolate;
  }

  .auth-brand-mark::before {
    content: "";
    position: absolute;
    inset: -0.5rem;
    z-index: -1;
    background:
      radial-gradient(circle at 50% 47%, rgb(244 204 134 / 0.22), transparent 44%),
      radial-gradient(circle at 50% 52%, rgb(196 154 90 / 0.18), transparent 72%);
    filter: blur(0.25rem);
  }

  .auth-brand-mark img {
    filter:
      drop-shadow(0 0 10px rgb(244 204 134 / 0.4)) drop-shadow(0 0 26px rgb(196 154 90 / 0.22));
  }
</style>
