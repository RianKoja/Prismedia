---
sidebar_position: 3
title: Connecting Infuse & Manet
description: Add Prismedia as a Jellyfin server in client apps and play your library.
---

# Connecting Infuse & Manet

Before you start, make sure you have:

- Prismedia reachable from the client device (e.g. `http://192.168.1.10:8008`).
- A Prismedia **username and password** — your own account, or one created in **Settings → Users**. See [Users & NSFW Servers](./profiles.md).

In every client the pattern is the same: add a **Jellyfin** (Emby/Jellyfin) server, point it at Prismedia's URL, and sign in with your Prismedia **username** and **password**.

## Infuse (video + audio)

1. Open Infuse → **Add Files / Source → Jellyfin** (or Emby).
2. **Address:** your Prismedia URL and port, e.g. `http://192.168.1.10:8008`.
3. **Username:** your Prismedia username (e.g. `me`).
4. **Password:** your Prismedia password.
5. Save. Infuse lists your Movies, Series, Videos, and Collections with artwork.
6. Play a title — compatible files direct-play; others transcode to HLS on demand. Resume position and watched state sync back to Prismedia, per user.

To run a SFW and an NSFW view, add Prismedia **twice** in Infuse, signing in as two different users (one with Allow NSFW off, one on).

## Manet / Finamp / Symfonium (audio)

1. Add a **Jellyfin server** in the app.
2. **Server URL:** `http://192.168.1.10:8008`.
3. **Username:** your Prismedia username.
4. **Password:** your Prismedia password.
5. Browse the Music library — artists, albums, and tracks with cover art, track/disc numbers, and durations.
6. Play — common formats stream directly, others transcode on the fly. Position and play counts sync.

## Troubleshooting

| Symptom | Likely cause / fix |
| --- | --- |
| Can't reach the server | Use the LAN IP and port `8008`, not `localhost`, from another device. Confirm the port is published. |
| Sign-in fails | The account must be **enabled**, and the password is that user's own password. Upgraded from pre-2.0? Migrated accounts keep the previous server API key as their password until it's changed. An admin can reset any password in Settings → Users. |
| A client signed out unexpectedly | Its user's password was changed or its session was revoked (Account → devices, or an admin action). Sign in again with the current credentials. |
| A library is missing | Members only see libraries they've been granted. Check the user's library access in Settings → Users. |
| Adult content shows when it shouldn't (or vice-versa) | Check the user's **Allow NSFW** setting; that account is the one this client signed in as. |
| No artwork | Image requests are anonymous by design; if covers are missing, confirm the items have artwork in Prismedia and that the proxy (if any) isn't blocking `/Items/.../Images`. |
| Behind a reverse proxy and clients can't connect | The Jellyfin routes must bypass the proxy's SSO. See [Reverse Proxy & Auth Middleware](../deployment/reverse-proxy.md). |

For deeper diagnostics see [Troubleshooting](../advanced/troubleshooting.md).
