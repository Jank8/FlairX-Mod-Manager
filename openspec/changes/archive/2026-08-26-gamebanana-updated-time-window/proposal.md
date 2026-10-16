## Why

The current GameBanana character-skin secondary sorting ranks only a fixed pool of the first 100 recently updated records. That limit omits eligible mods from a requested time window and cannot guarantee that “recent” means the GameBanana last-update timestamp.

## What Changes

- Add explicit 30-day, 90-day, 180-day, and unlimited time-range choices for character-skin browsing, with 90 days as the default.
- Keep time range visually and semantically separate from the within-range sort choice.
- Fetch bounded ranges in GameBanana `_tsDateUpdated,DESC` order until the update-time boundary is crossed, the API is exhausted, or a 10-page/500-record safety limit is reached.
- Merge pages and deduplicate by mod ID before applying the inclusive update-time boundary, character category, NSFW, and blacklist filters.
- Locally order bounded candidates by latest update, likes, downloads, or comments with deterministic update-time and ID tie-breakers.
- Preserve compatible time-range and sort state across details navigation and normalize it safely when category, search, or mode changes.
- Add English, Simplified Chinese, and Traditional Chinese text plus regression coverage for pagination, boundaries, timestamp selection, sorting, caps, and mode changes.

## Capabilities

### New Capabilities

- `gamebanana-updated-window-browsing`: Defines bounded last-update-time collection, filtering, ordering, safety limits, UI state, and navigation restoration for GameBanana character skins.

### Modified Capabilities

None. The repository has no existing OpenSpec capability definitions; existing GameBanana behavior is captured in legacy design documents and tests.

## Impact

- GameBanana browser orchestration and navigation state.
- The pure recent-candidate sorting/pagination policy.
- GameBanana browser localization resources for English, Simplified Chinese, and Traditional Chinese.
- Unit and static UI regression tests.
- The installed x64 Release DLL, PDB, and PRI artifact set under `E:\flairx`.
