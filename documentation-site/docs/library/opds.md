---
sidebar_position: 5
title: OPDS Reader Apps
description: Connect third-party ebook and comic readers to Prismedia with OPDS.
---

# OPDS Reader Apps

Prismedia exposes an **OPDS 1.2** catalog for third-party ebook and comic apps. Use it to browse visible books, search, load covers, and download/acquire files from your Prismedia library.

```text
http://your-prismedia-host:8008/opds
```

If Prismedia is behind HTTPS, use the public HTTPS origin instead:

```text
https://prismedia.example.com/opds
```

## What the catalog includes

The root catalog links to:

- Recently Added
- Libraries
- Authors
- Series
- Collections
- Tags
- Search

Publication entries include the title, author credits, summary, tags, series metadata, cover/thumbnail links, and one acquisition link when the source file is in a supported format.

## Supported formats

OPDS publishes downloadable books and comics in these formats:

| Format | File extensions | Media type |
| --- | --- | --- |
| EPUB | `.epub` | `application/epub+zip` |
| PDF | `.pdf` | `application/pdf` |
| CBZ / ZIP comics | `.cbz`, `.zip` | `application/vnd.comicbook+zip` or `application/zip` |
| CBR comics | `.cbr` | `application/vnd.comicbook-rar` |

Unsupported book files are excluded from OPDS feeds and return `404` from direct OPDS download links.

## Authentication

Every `/opds` route requires Prismedia authentication. There are no anonymous OPDS catalog, cover, or download URLs.

Most reader apps should use HTTP Basic Auth:

| Field | Value |
| --- | --- |
| Username | A Jellyfin-compatible profile username, such as `Prismedia`. |
| Password | The Prismedia API key from **Settings -> API Access**. |

OPDS also accepts the same API key and Jellyfin-compatible token transports documented in [Authentication & API Keys](../deployment/authentication.md):

- `X-Prismedia-Api-Key: <key>`
- `Authorization: Bearer <api-key-or-session-token>`
- Jellyfin token headers
- `?ApiKey=<key>` or `?api_key=<key>`

Some reader apps drop auth headers when they fetch covers or acquisition links. If that happens, configure the app to include the API key query parameter on the OPDS base URL, for example `https://prismedia.example.com/opds?ApiKey=...`. Treat URLs containing API keys as secrets.

## NSFW filtering

OPDS uses the same Jellyfin-compatible profile visibility rules as client apps:

- Profiles without NSFW permission only see SFW books and SFW navigation metadata.
- Profiles with NSFW permission can see NSFW books if the library is otherwise available.
- API-key-only OPDS requests are conservative and hide NSFW content.
- Direct book detail, cover, and download routes enforce the same visibility checks as feeds.

Hidden books do not contribute authors, tags, collections, series, counts, covers, search results, or acquisition links.

## Reverse proxies

If your reverse proxy uses SSO or forward auth, bypass that proxy auth for `/opds([/?].*)?$` the same way Jellyfin client routes bypass it. The OPDS routes still require Prismedia auth, but reader apps cannot complete an interactive SSO redirect.

See [Reverse Proxy & Auth Middleware](../deployment/reverse-proxy.md) for Authelia, Authentik, Traefik, and Nginx examples.

## Known limitations

OPDS support is catalog, search, cover, and acquisition focused. It does not provide OPDS 2.0 JSON, OPDS-PSE page streaming, annotations, bookmarks, or cross-reader progress sync. Prismedia's built-in reader progress remains available in the web app.
