---
sidebar_position: 1
title: Authentication & User Accounts
description: How Prismedia authenticates the web app, API calls, Jellyfin clients, and OPDS readers with per-user accounts.
---

# Authentication & User Accounts

Prismedia uses real user accounts. Every surface — the web app, direct `/api/*` calls, Jellyfin-compatible clients, and OPDS readers — signs in with the same **username and password** and then uses a **session token**. There is no shared app API key.

## First-run setup

A fresh install shows a **setup wizard** on first visit: it creates the administrator account and signs you in. Until an administrator exists, the app serves only the wizard, so complete setup promptly after exposing the server.

Upgrading from a pre-2.0 install? See [the upgrade notes](#upgrading-from-pre-20) below — your former Jellyfin sign-in profiles become real accounts automatically.

## Accounts and roles

There are two roles:

- **Administrators** manage the server (settings, users, libraries, files, jobs, identify, requests, plugins) and implicitly see every library.
- **Members** browse and play. Per member, an administrator controls: which **libraries** they can see, whether they may view **NSFW** content, and whether they may **create their own libraries**.

Manage accounts in **Settings → Users**. Each user changes their own display name, password, content visibility, and connected devices on the **Account** page. The last enabled administrator cannot be demoted, disabled, or deleted.

## The web app

Signing in at `/login` sets a same-origin, **HttpOnly** cookie (`prismedia-session`, `Secure` over HTTPS) carrying the session token. Sessions use a **90-day sliding window** — every visit extends them — so a browser on the couch effectively stays signed in.

## Direct API access

Sign in once to get a bearer token, then send it on every call. The same flow works for scripts and native apps:

```bash
TOKEN=$(curl -s -X POST http://localhost:8008/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"you","password":"your-password"}' | jq -r .accessToken)

curl -H "Authorization: Bearer $TOKEN" http://localhost:8008/api/library/stats
```

Accepted token transports:

| Method | Example |
| --- | --- |
| Bearer token | `Authorization: Bearer <token>` |
| Dedicated header | `X-Prismedia-Api-Key: <token>` |
| Query string | `?ApiKey=<token>` or `?api_key=<token>` |
| Cookie | `prismedia-session=<token>` (what the browser uses) |

Jellyfin clients additionally use the standard `X-Emby-Authorization` (with `Token=`), `X-Emby-Token`, and `X-MediaBrowser-Token` headers — handled automatically by those apps.

Treat tokens as secrets; prefer headers over query strings, and revoke a device's session from the Account page if a token leaks.

## Jellyfin clients and OPDS readers

Jellyfin-compatible clients (Infuse, Swiftfin, Manet, Finamp, Symfonium, …) sign in with a Prismedia **username and password**, exactly like a real Jellyfin server. See [Users & NSFW Servers](../jellyfin/profiles.md).

OPDS readers use HTTP **Basic Auth** with the same credentials:

```bash
curl -u "you:your-password" http://localhost:8008/opds
```

Per-user library access and NSFW permissions apply to Jellyfin and OPDS responses the same way they do in the web app.

## Rate limiting

Repeated failed sign-in attempts from an address are throttled and return `429 Too Many Requests`, so an exposed endpoint can't be brute-forced quickly.

## Public (no-auth) routes

A small set of routes are intentionally reachable without a session, so health checks, first-run setup, and Jellyfin sign-in work:

```text
/api/health
GET  /api/auth/setup-status
POST /api/auth/setup                 (only while no administrator exists)
POST /api/auth/login
GET  /System/Info/Public
GET/POST /System/Ping
GET  /Branding/Configuration
GET  /Branding/Css   /Branding/Css.css
GET  /QuickConnect/Enabled
GET  /Users/Public
POST /Users/AuthenticateByName
POST /Users/{id}/Authenticate
GET  /Items/{id}/Images/...          (artwork is anonymous, like real Jellyfin)
```

Everything else under `/api/*` and the Jellyfin route prefixes requires a signed-in user. All `/opds` routes require authentication and return a Basic Auth challenge when credentials are missing.

## Upgrading from pre-2.0

Upgrading a pre-2.0 install migrates authentication automatically:

- Each former Jellyfin sign-in profile becomes a **member account with access to every existing library**, and its password is the **previous server API key** — so Infuse, OPDS readers, and other connected apps keep working without reconfiguration. Signed-in client sessions survive the upgrade.
- The next browser visit shows the setup wizard to create your administrator account (reusing a migrated username promotes that account instead). Existing watch history, favorites, and ratings are copied to every account.
- Set new per-user passwords whenever you're ready, from the wizard's migrated-accounts step or **Settings → Users**.

## Password recovery

Locked out? Set environment variables on the container and restart:

| Variable | Effect |
| --- | --- |
| `PRISMEDIA_RECOVERY_PASSWORD` | On boot, resets (or creates) an enabled administrator with this password and signs out its other sessions. |
| `PRISMEDIA_RECOVERY_USERNAME` | The account to reset or create. Defaults to `admin`. |

The reset repeats on **every** boot while the variable is set, and a loud warning is logged — unset it after signing back in.

## The encryption secret (`PRISMEDIA_SECRET`)

Plugin credentials (for example a TMDB API key) are encrypted at rest with **AES-256-GCM**, using a key derived from `PRISMEDIA_SECRET`.

You normally don't set this. The container's entrypoint:

1. Uses `PRISMEDIA_SECRET` if you provide it.
2. Otherwise reads a previously generated secret from `/data/.prismedia-secret`.
3. Otherwise generates a random secret and persists it to `/data/.prismedia-secret` (mode `600`).

So stored credentials survive container recreation as long as `/data` persists. Set `PRISMEDIA_SECRET` explicitly only if you want to control the key yourself (e.g. to move credentials between environments, or store the secret in a secrets manager).

:::caution
If `PRISMEDIA_SECRET` changes and the old value is gone (the env var changed *and* `/data/.prismedia-secret` was lost), previously encrypted credentials become unreadable and you'll re-enter them. Back up `/data` (which includes the secret file) the same way you back up the database.
:::

## See also

- [Users & NSFW Servers](../jellyfin/profiles.md)
- [OPDS Reader Apps](../library/opds.md)
- [Reverse Proxy & Auth Middleware](./reverse-proxy.md)
