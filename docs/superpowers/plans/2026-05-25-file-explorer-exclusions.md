# File Explorer Exclusions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let users exclude and re-include watched-root files or directories from library scans through the Files explorer.

**Architecture:** The existing `media_file_ignores` table becomes a library-root-scoped exclusion table consumed by Files APIs and scan discovery. The API exposes explicit add/remove exclusion operations, scan handlers prune excluded paths before upsert, and the Svelte explorer renders excluded entries as muted filesystem-only items. The context menu layering fix stays in `FileTreePane` because that component owns the menu rendering.

**Tech Stack:** .NET 10, EF Core/Npgsql migrations, xUnit, Svelte 5, Vitest, Orval-generated API clients.

---

### Task 1: Backend Exclusion Contract And Persistence

**Files:**
- Modify: `apps/backend/src/Prismedia.Contracts/Files/FileContracts.cs`
- Modify: `apps/backend/src/Prismedia.Application/Files/FileApplicationModels.cs`
- Modify: `apps/backend/src/Prismedia.Application/Files/FilesService.cs`
- Modify: `apps/backend/src/Prismedia.Infrastructure/Files/EfFilesPersistence.cs`
- Modify: `apps/backend/src/Prismedia.Infrastructure/Persistence/Entities/SystemModelRows.cs`
- Modify: `apps/backend/src/Prismedia.Infrastructure/Persistence/PrismediaSystemModelConfiguration.cs`
- Create: EF migration under `apps/backend/src/Prismedia.Infrastructure/Persistence/Migrations`
- Test: `apps/backend/tests/Prismedia.Infrastructure.Tests/FilesServiceTests.cs`
- Test: `apps/backend/tests/Prismedia.Infrastructure.Tests/EfFilesPersistenceTests.cs`

- [ ] Write failing tests for add/remove exclusion, detail/list flags, linked entity suppression, and cascade persistence.
- [ ] Implement request/DTO fields, persistence methods, service use cases, and migration.
- [ ] Run the focused backend tests and commit.

### Task 2: Scan Exclusion Filtering

**Files:**
- Modify: `apps/backend/src/Prismedia.Application/Jobs/Ports/IFileDiscovery.cs`
- Modify: `apps/backend/src/Prismedia.Application/Jobs/Ports/ILibraryScanPersistence.cs`
- Modify: `apps/backend/src/Prismedia.Infrastructure/Media/Processing/FileDiscoveryService.cs`
- Modify: `apps/backend/src/Prismedia.Infrastructure/Media/Adapters/FileDiscoveryAdapter.cs`
- Modify: `apps/backend/src/Prismedia.Application/Jobs/Handlers/Scan/ScanLibraryJobHandler.cs`
- Modify: `apps/backend/src/Prismedia.Application/Jobs/Handlers/Scan/ScanGalleryJobHandler.cs`
- Modify: `apps/backend/src/Prismedia.Application/Jobs/Handlers/Scan/ScanAudioJobHandler.cs`
- Modify: `apps/backend/src/Prismedia.Application/Jobs/Handlers/Scan/ScanBookJobHandler.cs`
- Modify: `apps/backend/src/Prismedia.Infrastructure/Media/Persistence/LibraryScanPersistenceService.cs`
- Test: `apps/backend/tests/Prismedia.Api.Tests/ScanJobHandlerTests.cs`
- Test: `apps/backend/tests/Prismedia.Infrastructure.Tests/LibraryScanPersistenceServiceTests.cs`

- [ ] Write failing tests for discovery pruning and stale cleanup of excluded sources.
- [ ] Implement exclusion loading, discovery pruning, and defensive stale checks.
- [ ] Run focused scan tests and commit.

### Task 3: Files API Endpoints And Generated Client Surface

**Files:**
- Modify: `apps/backend/src/Prismedia.Api/Endpoints/Files/FilesMutationEndpoints.cs`
- Modify: `apps/backend/tests/Prismedia.Api.Tests/FilesEndpointTests.cs`
- Modify or regenerate: `apps/web-svelte/src/lib/api/generated/*`
- Modify: `apps/web-svelte/src/lib/api/prismedia.ts`

- [ ] Write failing endpoint/client wrapper tests for add/remove exclusion.
- [ ] Add endpoint mappings and generated/wrapper client functions.
- [ ] Run focused API/frontend API tests and commit.

### Task 4: Files Explorer UI

**Files:**
- Modify: `apps/web-svelte/src/lib/files/file-actions.ts`
- Modify: `apps/web-svelte/src/lib/files/file-actions.test.ts`
- Modify: `apps/web-svelte/src/lib/files/file-tree-state.ts`
- Modify: `apps/web-svelte/src/lib/files/file-tree-state.test.ts`
- Modify: `apps/web-svelte/src/lib/components/files/FileTreePane.svelte`
- Modify: `apps/web-svelte/src/lib/components/files/FileDetailPane.svelte`
- Modify: `apps/web-svelte/src/routes/files/+page.svelte`
- Test: `apps/web-svelte/src/lib/components/files/FileDetailPane.test.ts`

- [ ] Write failing Vitest coverage for action switching, metadata propagation, and excluded detail behavior.
- [ ] Add exclude/remove-exclusion actions, muted tree rows, detail toolbar/status, and raised menu z-index.
- [ ] Run Svelte autofixer and focused frontend tests.

### Task 5: Final Verification

**Files:**
- Modify: `CHANGELOG.md`

- [ ] Add the changelog entry.
- [ ] Run `dotnet build apps/backend/Prismedia.slnx`.
- [ ] Run focused backend and frontend tests.
- [ ] Run `pnpm --filter @prismedia/web-svelte typecheck` if Svelte components changed.
- [ ] Commit the final changelog and any generated artifacts.
