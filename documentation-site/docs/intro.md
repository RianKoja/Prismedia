---
sidebar_position: 1
title: About Prismedia
description: What Prismedia is, what it manages, and how the pieces fit together.
---

# About Prismedia

Prismedia is a private, self-hosted media library for a trusted user or household on a private LAN. It is video-first, but images, galleries, books, comics, audio, people, studios, tags, and collections are first-class library entities.

It ships as one Docker image. PostgreSQL 16, ffmpeg, the .NET API, the .NET worker, and the static Svelte frontend all run together behind port `8008`.

![Prismedia dashboard](/img/screenshots/dashboard.png)

## What it is for

- Keeping a local media library organized without handing library state to a cloud service.
- Browsing videos, series, images, galleries, books, comics, audio, and related metadata from one app.
- Managing files and scan exclusions from the browser when your media mount is writable.
- Running local background work for scans, probes, thumbnails, sprites, HLS, subtitles, identify, imports, and collection refreshes.
- Using plugin and Stash-compatible metadata workflows while keeping Prismedia's schema independent.

## Main workspaces

| Workspace | Purpose |
| --- | --- |
| **Dashboard** | Recent media, library counts, queue state, and release/update notices. |
| **Browse** | Videos, series, images, galleries, books, audio, people, studios, tags, and collections. |
| **Files** | Watched-root file tree with open, upload, new folder, rename, move, rescan, exclude, and delete actions. |
| **Identify** | Durable review queue for provider matches and metadata proposals. |
| **Plugins** | Native plugins, Stash-compatible scrapers, and StashBox endpoints. |
| **Jobs** | Worker heartbeat, active queues, recent work, failures, and manual queue actions. |
| **Settings** | Library roots, visibility, playback, subtitles, generation, worker, storage, and diagnostics. |

## Design direction

Prismedia follows **Prism Noir Luxe**: dark material layers, glass for floating and interactive surfaces, controlled radii, brass glow for active state, and dense layouts that remain touch-friendly.

The design language is documented in [Design Language](./developers/design-language.md).

## Runtime model

```text
Browser / LAN
    |
    | HTTP :8008
    v
.NET API
    |-- serves /api/*
    |-- serves the built Svelte app
    |-- streams direct files and HLS assets
    |-- applies EF Core migrations
    |
    +--> PostgreSQL 16
    |
    +--> .NET worker
         scans, probes, previews, HLS, subtitles, identify, imports
```

The frontend is a client only. Public HTTP contracts live in the .NET backend and the Svelte app prefers generated OpenAPI clients.

## What to read next

- [Quick Start](./users/quick-start.md) gets the container running.
- [First Boot](./users/first-boot.md) walks through the first library scan.
- [Browsing The Library](./users/browsing.md) explains the main app surfaces.
- [Library Organization](./users/library-organization.md) describes how folder layout becomes media structure.
