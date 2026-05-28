---
sidebar_position: 8
title: Settings
description: Watched libraries, visibility, playback, subtitles, generation, storage, worker, and diagnostics.
---

# Settings

Settings is where Prismedia describes and edits app-wide behavior. Most controls are descriptor-driven so the UI, defaults, and persistence stay aligned.

![Settings](/img/screenshots/settings.png)

## Watched libraries

A watched library root is a container path plus scan behavior. Typical examples:

| Root | Scan toggles |
| --- | --- |
| `/media/videos` | Videos |
| `/media/images` | Images, Galleries |
| `/media/books` | Books |
| `/media/music` | Audio |

Root settings:

| Setting | Meaning |
| --- | --- |
| **Enabled** | Disabled roots stay configured but are skipped. |
| **Recursive** | Walk subfolders. |
| **Scan videos/images/galleries/books/audio** | Independent media-type scanners. |
| **NSFW** | Marks media under the root as restricted by default. |

## Content visibility

Visibility controls whether NSFW content appears in browse pages, search, files, identify, plugin providers, and relationship surfaces.

Modes are designed for private LAN use:

- Hide restricted content by default.
- Show restricted content when explicitly enabled.
- Optionally auto-enable on trusted LAN access.

## Playback

Playback settings cover direct/HLS behavior, generated preview defaults, player preferences, and resume behavior. Videos use direct playback where possible and HLS when transcoding is needed.

## Subtitles

![Subtitle view options](/img/screenshots/settings-subtitles.png)

Subtitle settings control:

- Auto-enable on playback.
- Preferred language order.
- Caption style.
- Text size.
- Vertical position.
- Transparency.

Video pages can still apply local per-browser overrides from the player.

## Generation pipeline

Generation settings control work such as thumbnails, sprites, waveforms, subtitles, pHash, and HLS output. These settings affect future scans and rebuild actions.

## Generated storage

Generated storage diagnostics help you understand and refresh cached assets under `/data`, including thumbnails, sprites, HLS renditions, waveform data, plugin artwork, and extracted subtitles.

## Worker

Worker settings include concurrency and scheduling behavior. Higher concurrency can help on large machines and hurt on small disks; raise it carefully.

## Diagnostics

Diagnostics show build/version information, runtime state, update checks, storage actions, and maintenance controls. Include this information when filing bugs.
