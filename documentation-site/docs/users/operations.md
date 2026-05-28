---
sidebar_position: 7
title: Jobs And Operations
description: Worker status, queues, scans, failures, and maintenance.
---

# Jobs And Operations

The Jobs page shows what Prismedia is doing in the background. It is the first place to check when a scan, thumbnail, identify run, HLS render, subtitle extraction, or import feels slow.

![Jobs](/img/screenshots/jobs.png)

## Worker status

The worker heartbeat badge tells you whether the .NET worker is online. If the API is running but the worker is offline, the app can still browse existing data, but queued work will not move.

## Queue families

| Queue | Typical work |
| --- | --- |
| **Library scan** | Walk watched roots, classify files, remove missing files. |
| **Media probe** | Read technical metadata, duration, dimensions, codecs, audio info. |
| **Preview** | Generate thumbnails, sprites, waveforms, and preview assets. |
| **HLS** | Create adaptive playback assets on demand. |
| **Subtitles** | Extract embedded subtitles and normalize tracks. |
| **Identify** | Provider searches, bulk identify, proposal hydration. |
| **Collections** | Refresh dynamic and hybrid collection rules. |
| **Maintenance** | Cleanup and diagnostic backfills. |

## Running a scan

You can run a scan from:

- **Jobs**, for a general library scan.
- **Settings -> Watched Libraries**, for root-level management.
- **Files**, for a specific root, folder, or file context.

Scans are idempotent. They pick up new files, update file metadata, and remove catalog rows for files that disappeared from disk. User edits, ratings, visibility, relationships, and progress are preserved unless an explicit action changes them.

## Failures

Open failure rows to read the error message. Common causes:

- A bad or unsupported media file.
- A media path that no longer exists.
- A read-only mount for an operation that needs write access.
- Disk full under `/data`.
- Plugin or endpoint network/API errors.
- Missing local helper tools in development.

After fixing the cause, retry the action or rescan. Clearing failures hides acknowledged rows from the dashboard; it does not erase historical database records.

## Stuck work

If work appears stuck:

1. Check the worker heartbeat.
2. Read the active queue row for the target label and message.
3. Check container logs with `docker compose logs prismedia --tail 200`.
4. Restart the container if the worker was killed mid-job.

Running jobs from an old worker process are recovered when the worker restarts.

## Worker concurrency

The worker concurrency setting is global. Raising it can speed up independent jobs, but it may also increase disk, CPU, and ffmpeg pressure. Keep it modest on small NAS or single-board systems.

## Generated storage

Generated assets live under `/data`: thumbnails, HLS renditions, waveform data, sprites, extracted subtitles, and plugin artwork. Settings includes diagnostics and rebuild actions for generated storage when assets need to be refreshed.
