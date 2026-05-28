---
sidebar_position: 6
title: Identify And Plugins
description: Use the Identify queue, provider proposals, plugins, and StashBox endpoints.
---

# Identify And Plugins

Identify is Prismedia's metadata review workflow. It is queue-based, durable, and designed so you can inspect provider suggestions before applying them to your library.

![Identify queue](/img/screenshots/identify.png)

## Provider types

| Provider | Use |
| --- | --- |
| **Native TypeScript plugin** | Rich Prismedia provider packages. |
| **Python plugin** | Providers implemented as Python packages/scripts. |
| **Stash-compatible scraper** | YAML scraper packages adapted into Prismedia's provider model. |
| **StashBox endpoint** | Fingerprint-based lookup and contribution through the StashBox GraphQL protocol. |

Plugins and endpoints appear together in Identify provider pickers, but they are configured in **Plugins**.

## Identify queue

The Identify page has a queue of entities that need review. Add items from the Identify page or from detail-page actions. Queue state survives navigation and backend restarts.

Each queued item can move through these states:

| State | Meaning |
| --- | --- |
| **Queued** | The entity is waiting for a provider run. |
| **Searching** | Prismedia is fetching candidate matches. |
| **Pending choice** | Multiple candidates need a user decision. |
| **Pending review** | A proposal is ready to inspect. |
| **Complete** | The accepted proposal has been applied. |

## Reviewing a proposal

The review surface shows what will change before anything is saved:

- Base fields such as title, date, description, rating, and flags.
- Tags, people, studios, links, and provider IDs.
- Poster and backdrop artwork choices.
- Child items such as seasons, episodes, chapters, or related entities.
- Existing vs proposed values.

You can walk into child proposals, disable relationship cards, choose artwork, and accept only when the proposal looks right.

## Bulk identify

Bulk identify runs as a background job. Progress appears in Jobs, and results feed back into the Identify review queue. This keeps larger provider runs resilient to navigation and app restarts.

## Plugin management

![Plugins](/img/screenshots/plugins.png)

Use **Plugins** to:

- Browse installed plugins.
- Install community packages.
- Enable or disable providers.
- Configure credentials.
- Add, test, edit, disable, or delete StashBox endpoints.

Set `PRISMEDIA_SECRET` before storing credentials in a production container so encrypted values survive container recreation.

## StashBox endpoints

StashBox endpoints use fingerprints such as MD5, OpenSubtitles hash, and pHash to identify videos by content. Accepting a StashBox-origin proposal can link the local video to the remote record and queue fingerprint contribution when configured.

See [StashBox Endpoints](../advanced/stashbox.md) and [pHash Contribution](../advanced/phash-contribution.md) for details.

## NSFW visibility

Provider lists respect the current visibility mode. NSFW providers and endpoint results are hidden when visibility is off, unless the endpoint or plugin is explicitly safe-for-work.

Visibility also affects queue totals and review rows so hidden content does not leak through Identify.
