<script lang="ts">
  import {
    Activity,
    ChartNoAxesCombined,
    Clock3,
    Eye,
    History,
    Loader2,
    RotateCcw,
    SkipForward,
    Trophy,
  } from "@lucide/svelte";
  import { Button, cn } from "@prismedia/ui-svelte";
  import { apiAssetUrl } from "$lib/api/orval-fetch";
  import { fetchPlaybackStatistics } from "$lib/api/playback-statistics";
  import {
    ENTITY_KIND,
    PLAYBACK_EVENT_KIND,
    labelForEntityKind,
    resolveEntityHref,
    type EntityKindCode,
    type PlaybackEventKindCode,
  } from "$lib/entities/entity-codes";
  import { useNsfw } from "$lib/nsfw/store.svelte";
  import type {
    PlaybackStatisticsBucket,
    PlaybackStatisticsEntity,
    PlaybackStatisticsEvent,
    PlaybackStatisticsResponse,
  } from "$lib/api/generated/model";

  const ALL_FILTER = "all" as const;

  type TimeframeKey = "30d" | "90d" | "year" | "all";
  type KindFilter = typeof ALL_FILTER | EntityKindCode;
  type EventFilter = typeof ALL_FILTER | PlaybackEventKindCode;

  interface TimeframeOption {
    key: TimeframeKey;
    label: string;
    days: number | null;
  }

  const TIMEFRAMES: TimeframeOption[] = [
    { key: "30d", label: "30D", days: 30 },
    { key: "90d", label: "90D", days: 90 },
    { key: "year", label: "Year", days: 365 },
    { key: "all", label: "All", days: null },
  ];

  const KIND_FILTERS: ReadonlyArray<{ value: KindFilter; label: string }> = [
    { value: ALL_FILTER, label: "All" },
    { value: ENTITY_KIND.video, label: "Videos" },
    { value: ENTITY_KIND.movie, label: "Movies" },
    { value: ENTITY_KIND.videoSeries, label: "Series" },
    { value: ENTITY_KIND.audioTrack, label: "Tracks" },
    { value: ENTITY_KIND.audioLibrary, label: "Audio" },
    { value: ENTITY_KIND.book, label: "Books" },
    { value: ENTITY_KIND.gallery, label: "Galleries" },
    { value: ENTITY_KIND.image, label: "Images" },
  ];

  const EVENT_FILTERS: ReadonlyArray<{ value: EventFilter; label: string }> = [
    { value: ALL_FILTER, label: "All" },
    { value: PLAYBACK_EVENT_KIND.completed, label: "Plays" },
    { value: PLAYBACK_EVENT_KIND.skipped, label: "Skips" },
  ];

  const nsfw = useNsfw();

  let timeframe = $state<TimeframeKey>("year");
  let kindFilter = $state<KindFilter>(ALL_FILTER);
  let eventFilter = $state<EventFilter>(ALL_FILTER);
  let stats = $state<PlaybackStatisticsResponse | null>(null);
  let loading = $state(true);
  let error = $state<string | null>(null);
  let activeRequest = 0;

  const topEntities = $derived(stats?.topEntities ?? []);
  const recentEvents = $derived(stats?.recentEvents ?? []);
  const dailyEvents = $derived(stats?.dailyEvents ?? []);
  const maxDailyEvents = $derived(
    Math.max(1, ...dailyEvents.map((bucket) => countBucketEvents(bucket))),
  );
  const summaryFrom = $derived(stats ? formatDate(stats.from) : "");
  const summaryTo = $derived(stats ? formatDate(stats.to) : "");
  const showEmpty = $derived(!loading && !error && (stats?.totalEvents ?? 0) === 0);

  $effect(() => {
    const params = buildQuery(timeframe, kindFilter, eventFilter, nsfw.mode === "off");
    const requestId = ++activeRequest;
    const controller = new AbortController();

    loading = true;
    error = null;

    fetchPlaybackStatistics(params, { signal: controller.signal })
      .then((response) => {
        if (requestId !== activeRequest) return;
        stats = response;
      })
      .catch((err) => {
        if (requestId !== activeRequest || isAbortError(err)) return;
        stats = null;
        error = err instanceof Error ? err.message : "Failed to load playback statistics";
      })
      .finally(() => {
        if (requestId === activeRequest) loading = false;
      });

    return () => controller.abort();
  });

  function buildQuery(
    selectedTimeframe: TimeframeKey,
    selectedKind: KindFilter,
    selectedEvent: EventFilter,
    hideNsfw: boolean,
  ) {
    const to = new Date();
    const from = fromForTimeframe(selectedTimeframe, to);
    return {
      from: from.toISOString(),
      to: to.toISOString(),
      kind: selectedKind === ALL_FILTER ? undefined : selectedKind,
      eventKind: selectedEvent === ALL_FILTER ? undefined : selectedEvent,
      hideNsfw,
    };
  }

  function fromForTimeframe(selectedTimeframe: TimeframeKey, to: Date): Date {
    const option = TIMEFRAMES.find((item) => item.key === selectedTimeframe);
    if (!option || option.days == null) return new Date("1970-01-01T00:00:00.000Z");

    const from = new Date(to);
    from.setUTCDate(from.getUTCDate() - option.days);
    return from;
  }

  function isAbortError(err: unknown): boolean {
    return err instanceof DOMException && err.name === "AbortError";
  }

  function countBucketEvents(bucket: PlaybackStatisticsBucket): number {
    return Number(bucket.completedCount) + Number(bucket.skippedCount);
  }

  function formatNumber(value: number | string | null | undefined): string {
    return Number(value ?? 0).toLocaleString();
  }

  function formatDate(value: string): string {
    return new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric", year: "numeric" })
      .format(new Date(value));
  }

  function formatShortDate(value: string): string {
    return new Intl.DateTimeFormat(undefined, { month: "short", day: "numeric" })
      .format(new Date(value));
  }

  function formatEventTime(value: string): string {
    return new Intl.DateTimeFormat(undefined, {
      month: "short",
      day: "numeric",
      hour: "numeric",
      minute: "2-digit",
    }).format(new Date(value));
  }

  function eventLabel(kind: string): string {
    return kind === PLAYBACK_EVENT_KIND.skipped ? "Skipped" : "Played";
  }

  function eventTone(kind: string): string {
    return kind === PLAYBACK_EVENT_KIND.skipped
      ? "border-amber-400/30 bg-amber-400/10 text-amber-100"
      : "border-emerald-400/30 bg-emerald-400/10 text-emerald-100";
  }

  function coverFor(entity: Pick<PlaybackStatisticsEntity | PlaybackStatisticsEvent, "coverUrl">): string | undefined {
    return apiAssetUrl(entity.coverUrl);
  }

  function entityHref(entity: Pick<PlaybackStatisticsEntity, "id" | "kind">): string | undefined {
    return resolveEntityHref(entity.kind, entity.id);
  }

  function eventHref(event: Pick<PlaybackStatisticsEvent, "entityId" | "entityKind">): string | undefined {
    return resolveEntityHref(event.entityKind, event.entityId);
  }

  function selectTimeframe(value: TimeframeKey) {
    timeframe = value;
  }

  function selectKind(value: KindFilter) {
    kindFilter = value;
  }

  function selectEvent(value: EventFilter) {
    eventFilter = value;
  }
</script>

<svelte:head>
  <title>Playback Stats · Prismedia</title>
</svelte:head>

<div class="space-y-4">
  <section class="surface-card overflow-hidden rounded-md border-border-subtle">
    <div class="relative isolate px-4 py-4 sm:px-5">
      <div class="absolute inset-0 -z-10 bg-[radial-gradient(circle_at_top_left,rgba(242,194,106,0.14),transparent_34%),linear-gradient(135deg,rgba(18,18,22,0.96),rgba(8,8,10,0.98))]"></div>
      <div class="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div class="min-w-0 space-y-2">
          <div class="flex items-center gap-2 text-mono-sm uppercase tracking-[0.18em] text-accent-300">
            <ChartNoAxesCombined class="h-4 w-4" />
            Playback History
          </div>
          <h1 class="font-heading text-2xl text-text-primary sm:text-3xl">Playback Stats</h1>
          <p class="max-w-2xl text-sm text-text-muted">
            {#if stats}
              {summaryFrom} - {summaryTo}
            {:else}
              Loading playback history
            {/if}
          </p>
        </div>

        <div class="grid gap-2 sm:grid-cols-[auto_auto_auto]">
          <div class="flex flex-wrap gap-1.5 rounded-sm border border-border-subtle bg-surface-1/70 p-1">
            {#each TIMEFRAMES as option (option.key)}
              <Button
                variant={timeframe === option.key ? "primary" : "ghost"}
                size="sm"
                class="h-7 px-2.5"
                onclick={() => selectTimeframe(option.key)}
              >
                {option.label}
              </Button>
            {/each}
          </div>

          <div class="flex flex-wrap gap-1.5 rounded-sm border border-border-subtle bg-surface-1/70 p-1">
            {#each EVENT_FILTERS as option (option.value)}
              <Button
                variant={eventFilter === option.value ? "primary" : "ghost"}
                size="sm"
                class="h-7 px-2.5"
                onclick={() => selectEvent(option.value)}
              >
                {option.label}
              </Button>
            {/each}
          </div>
        </div>
      </div>

      <div class="mt-4 flex gap-1.5 overflow-x-auto pb-1">
        {#each KIND_FILTERS as option (option.value)}
          <Button
            variant={kindFilter === option.value ? "primary" : "secondary"}
            size="sm"
            class="h-7 shrink-0 px-2.5"
            onclick={() => selectKind(option.value)}
          >
            {option.label}
          </Button>
        {/each}
      </div>
    </div>
  </section>

  {#if error}
    <div class="surface-card rounded-md border-error/30 bg-error-muted/20 px-4 py-3 text-sm text-error-text">
      {error}
    </div>
  {/if}

  <section class="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
    <article class="surface-card rounded-md border-border-subtle p-4">
      <div class="flex items-center justify-between text-text-muted">
        <span class="text-mono-sm uppercase tracking-[0.14em]">Total</span>
        <Activity class="h-4 w-4" />
      </div>
      <div class="mt-3 text-3xl font-semibold text-text-primary">
        {loading ? "-" : formatNumber(stats?.totalEvents)}
      </div>
      <div class="mt-1 text-sm text-text-muted">Events in range</div>
    </article>

    <article class="surface-card rounded-md border-border-subtle p-4">
      <div class="flex items-center justify-between text-text-muted">
        <span class="text-mono-sm uppercase tracking-[0.14em]">Plays</span>
        <Eye class="h-4 w-4" />
      </div>
      <div class="mt-3 text-3xl font-semibold text-emerald-100">
        {loading ? "-" : formatNumber(stats?.completedCount)}
      </div>
      <div class="mt-1 text-sm text-text-muted">Completed playback events</div>
    </article>

    <article class="surface-card rounded-md border-border-subtle p-4">
      <div class="flex items-center justify-between text-text-muted">
        <span class="text-mono-sm uppercase tracking-[0.14em]">Skips</span>
        <SkipForward class="h-4 w-4" />
      </div>
      <div class="mt-3 text-3xl font-semibold text-amber-100">
        {loading ? "-" : formatNumber(stats?.skippedCount)}
      </div>
      <div class="mt-1 text-sm text-text-muted">Fast abandons and explicit skips</div>
    </article>

    <article class="surface-card rounded-md border-border-subtle p-4">
      <div class="flex items-center justify-between text-text-muted">
        <span class="text-mono-sm uppercase tracking-[0.14em]">Items</span>
        <Trophy class="h-4 w-4" />
      </div>
      <div class="mt-3 text-3xl font-semibold text-text-primary">
        {loading ? "-" : formatNumber(stats?.distinctEntityCount)}
      </div>
      <div class="mt-1 text-sm text-text-muted">Distinct entities touched</div>
    </article>
  </section>

  {#if loading}
    <div class="surface-card flex min-h-52 items-center justify-center rounded-md border-border-subtle">
      <Loader2 class="h-6 w-6 animate-spin text-accent-300" />
    </div>
  {:else if showEmpty}
    <div class="surface-card flex min-h-52 flex-col items-center justify-center rounded-md border-border-subtle px-4 text-center">
      <History class="h-7 w-7 text-text-muted" />
      <h2 class="mt-3 font-heading text-lg text-text-primary">No playback history yet</h2>
      <p class="mt-1 max-w-md text-sm text-text-muted">
        Completed and skipped events will appear here as playback history is recorded.
      </p>
    </div>
  {:else}
    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_minmax(340px,0.8fr)]">
      <article class="surface-card rounded-md border-border-subtle p-4">
        <div class="flex items-center justify-between gap-3">
          <div>
            <h2 class="font-heading text-lg text-text-primary">Daily Activity</h2>
            <p class="text-sm text-text-muted">Completed and skipped events by day</p>
          </div>
          <RotateCcw class="h-4 w-4 text-text-muted" />
        </div>

        <div class="mt-5 flex h-52 items-end gap-1 overflow-x-auto border-b border-border-subtle pb-2">
          {#each dailyEvents as bucket (bucket.date)}
            {@const completed = Number(bucket.completedCount)}
            {@const skipped = Number(bucket.skippedCount)}
            {@const total = completed + skipped}
            {@const height = Math.max(4, Math.round((total / maxDailyEvents) * 100))}
            <div class="group flex min-w-4 flex-1 flex-col items-center justify-end gap-1">
              <div
                class="flex w-full max-w-8 flex-col justify-end overflow-hidden rounded-xs border border-border-subtle bg-surface-2"
                style="height: {height}%"
                title={`${formatShortDate(bucket.date)}: ${total} events`}
              >
                {#if skipped > 0}
                  <div
                    class="bg-amber-300/70"
                    style="height: {Math.max(2, Math.round((skipped / Math.max(1, total)) * height))}%"
                  ></div>
                {/if}
                {#if completed > 0}
                  <div class="flex-1 bg-accent-300/80"></div>
                {/if}
              </div>
            </div>
          {/each}
        </div>

        <div class="mt-3 flex items-center gap-4 text-xs text-text-muted">
          <span class="inline-flex items-center gap-1.5"><span class="h-2 w-2 rounded-xs bg-accent-300"></span>Plays</span>
          <span class="inline-flex items-center gap-1.5"><span class="h-2 w-2 rounded-xs bg-amber-300"></span>Skips</span>
        </div>
      </article>

      <article class="surface-card rounded-md border-border-subtle p-4">
        <div class="flex items-center justify-between gap-3">
          <div>
            <h2 class="font-heading text-lg text-text-primary">Top Entities</h2>
            <p class="text-sm text-text-muted">Ranked by events in this window</p>
          </div>
          <Trophy class="h-4 w-4 text-accent-300" />
        </div>

        <div class="mt-4 space-y-2">
          {#each topEntities as item, index (item.id)}
            {@const href = entityHref(item)}
            {@const cover = coverFor(item)}
            <a
              href={href ?? undefined}
              class={cn(
                "group flex items-center gap-3 rounded-sm border border-border-subtle bg-surface-1/70 p-2 transition-colors",
                href ? "hover:border-border-accent hover:bg-surface-2" : "pointer-events-none",
              )}
            >
              <div class="flex h-14 w-10 shrink-0 items-center justify-center overflow-hidden rounded-xs border border-border-subtle bg-surface-3 text-sm font-semibold text-text-muted">
                {#if cover}
                  <img src={cover} alt="" class="h-full w-full object-cover" loading="lazy" />
                {:else}
                  {index + 1}
                {/if}
              </div>
              <div class="min-w-0 flex-1">
                <div class="truncate text-sm font-medium text-text-primary">{item.title}</div>
                <div class="text-xs text-text-muted">{labelForEntityKind(item.kind)}</div>
              </div>
              <div class="grid shrink-0 grid-cols-2 gap-2 text-right text-xs">
                <span class="text-emerald-100">{formatNumber(item.completedCount)}</span>
                <span class="text-amber-100">{formatNumber(item.skippedCount)}</span>
              </div>
            </a>
          {/each}
        </div>
      </article>
    </section>

    <section class="surface-card rounded-md border-border-subtle p-4">
      <div class="flex items-center justify-between gap-3">
        <div>
          <h2 class="font-heading text-lg text-text-primary">Recent Events</h2>
          <p class="text-sm text-text-muted">Latest timestamped playback history</p>
        </div>
        <Clock3 class="h-4 w-4 text-text-muted" />
      </div>

      <div class="mt-4 divide-y divide-border-subtle">
        {#each recentEvents as event (event.id)}
          {@const href = eventHref(event)}
          {@const cover = coverFor(event)}
          <a
            href={href ?? undefined}
            class={cn(
              "flex items-center gap-3 py-2.5 transition-colors",
              href && "hover:bg-surface-2/50",
            )}
          >
            <div class="flex h-11 w-8 shrink-0 items-center justify-center overflow-hidden rounded-xs border border-border-subtle bg-surface-3 text-xs text-text-muted">
              {#if cover}
                <img src={cover} alt="" class="h-full w-full object-cover" loading="lazy" />
              {:else}
                <Activity class="h-3.5 w-3.5" />
              {/if}
            </div>
            <div class="min-w-0 flex-1">
              <div class="truncate text-sm font-medium text-text-primary">{event.entityTitle}</div>
              <div class="text-xs text-text-muted">
                {labelForEntityKind(event.entityKind)} · {formatEventTime(event.occurredAt)}
              </div>
            </div>
            <span class={cn("shrink-0 rounded-xs border px-2 py-1 text-xs font-medium", eventTone(event.kind))}>
              {eventLabel(event.kind)}
            </span>
          </a>
        {/each}
      </div>
    </section>
  {/if}
</div>
