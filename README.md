## ProviderStuff — Jellyfin plugin for provider tags, collections, and a handy API

ProviderStuff automates tagging library items with streaming provider tags, keeps optional Box Set collections per provider up to date, and exposes a small REST API to query providers and their items. It’s built for Jellyfin 10.9+ and .NET 8.

### Highlights

- Provider tags like provider:Netflix applied automatically via a scheduled task
- Optional per-provider Box Set collections (e.g., “Netflix”) kept in sync
- Fast, batched updates to reduce library churn
- REST API endpoints to list providers and fetch their items
- Paginated item queries returning rich BaseItemDto via Jellyfin’s DtoService

---

## Installation

Recommended (via Plugin Repository):
1. In Jellyfin, go to Dashboard → Plugins → Repositories
2. Add this URL as a new repository:
    https://raw.githubusercontent.com/kamilkosek/jellyfin-plugin-provider-stuff/main/manifest.json
3. Go to Catalog, search for “ProviderStuff”, install it
4. Restart Jellyfin

Optional: Development install
- Using VS Code tasks: run the “build-and-copy” task to publish and copy to your Jellyfin data directory
- Manual: publish the solution, then copy `Jellyfin.Plugin.ProviderStuff.dll` (and publish contents) to `<jellyfin-data>/plugins/Jellyfin.Plugin.ProviderStuff/`

Requirements:
- Jellyfin Server 10.9+
- .NET SDK 8.0+ (only for local builds)

---

## Configuration

Open Jellyfin Dashboard → Plugins → ProviderStuff to configure.

Global settings:
- TMDB API Key (required)
- TMDB Country (default: DE)

Providers array (repeat per provider):
- Name: Display name (e.g., Netflix)
- ProviderIds: List of integer provider IDs from TMDB
- ProviderLogoUrl: Optional URL to a logo image
- CreateCollection: When true, a Box Set with the provider name is created and maintained

The scheduled task “ProviderStuff: Apply provider tags” runs daily at 03:00 by default. It:
- Resolves providers for items using TMDB
- Adds tags like provider:<name> to matched Movies, Series, and Episodes
- Creates and updates Box Set collections when CreateCollection is enabled, using batched additions

---

## API

All endpoints require normal Jellyfin authentication and are under the base route: `/providerstuff`

1) List providers
- GET `/providerstuff/providers`
- Returns: array of providers with the following fields:
    - name
    - providerIds (int[])
    - providerLogoUrl (string)
    - createCollection (bool)
    - collectionId (string, when CreateCollection has created/found one)

2) List items for a provider (paginated)
- GET `/providerstuff/{providerName}/items`
- Query parameters:
    - userId (Guid, optional): user for DTO shaping
    - includeItemTypes (repeatable, optional): Movie, Series, Episode; defaults to all three
    - limit (int, optional)
    - startIndex (int, optional; default 0)
- Returns: `QueryResult<BaseItemDto>`
    - items: BaseItemDto[]
    - totalRecordCount: number
    - startIndex: number

Example usage:
- Fetch the first 50 Netflix items: `/providerstuff/Netflix/items?limit=50&startIndex=0`
- Only Movies for Prime Video: `/providerstuff/Prime%20Video/items?includeItemTypes=Movie`

Notes:
- Items are filtered by the tag `provider:{providerName}` applied by the scheduled task.
- Use repeated `includeItemTypes` parameters for multiple kinds (e.g., `...&includeItemTypes=Movie&includeItemTypes=Series`).

---

## How it works

- The scheduled task scans Movies, Series, and Episodes and looks up TMDB provider data per item.
- When configured providers match an item’s TMDB providers, the plugin adds a provider:<name> tag.
- If CreateCollection is true, the plugin pre-creates a Box Set named after the provider and batches items into it for performance.
- The API uses Jellyfin’s `ILibraryManager` to query items by tag and `IDtoService` to return `BaseItemDto` with optional user context.

---

## Development quickstart

- Prerequisites: .NET SDK 8.0, Jellyfin 10.9+, VS Code (optional)
- Workspace tasks:
    - build: `dotnet publish` for the solution
    - deploy-remote: runs `./deploy2.sh` (custom to your environment)
    - build-and-copy: builds and deploys in sequence (uses workspace settings)

Tip: Configure your workspace settings (pluginName, jellyfinDir/webDir/dataDir if you adopt the example tasks) to streamline debugging.

### Release Process

The project uses an automated release workflow that:

1. **Auto-computes the next version** using `scripts/next-version.js`
   - Finds the latest git tag with a 3- or 4-part numeric version (e.g., `1.2.3` or `1.2.3.4`)
   - Increments the build number by default, or respects `BUMP` environment variable (`major`, `minor`, `patch`, `build`)
   - Falls back to `1.1.0.0` if no version tags exist

2. **Creates idempotent releases** via the GitHub workflow
   - Re-running the same release won't fail if tags/releases already exist
   - Tags are only created if they don't exist
   - GitHub releases are updated with `--clobber` if they already exist

3. **Updates all version references consistently**
   - Updates `AssemblyVersion` and `FileVersion` in the `.csproj` file
   - Names the ZIP file with the computed version
   - Updates `manifest.json` with the new version entry

To trigger a release, use the "Create release" workflow dispatch in GitHub Actions. The process will automatically determine the next version and handle all steps idempotently.

---

## Changelog (summary)

- 1.1.0
    - Reliable plugin configuration retrieval in scheduled task
    - Optional per-provider Box Set collections
    - Batched item additions for performance
    - New REST endpoints: `/providerstuff/providers` and `/providerstuff/{providerName}/items`
    - Paginated item results with `QueryResult<BaseItemDto>`
    - DTOs via Jellyfin’s `DtoService`

---

## License

GPLv3 – see `LICENSE`.
