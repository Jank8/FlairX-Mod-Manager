## Context

The `custom/gamebanana-enhancements` branch already supports character-category browsing and an optional local download/like/comment ranking of exactly two GameBanana pages. The browser fetches category pages, maps records to cards, applies NSFW and blacklist checks, and stores compatible secondary-sort state in its navigation entries.

The fixed two-page pool is the limiting assumption. A time-window result cannot be derived from only the first 100 rows, and the window must use `_tsDateUpdated` rather than `_tsDateAdded` or `_tsDateModified`. The API exposes 50 rows per category page and can fail or return duplicates, so the implementation also needs bounded collection and partial-result behavior.

## Goals / Non-Goals

**Goals:**

- Make 90 days the default updated-time window in eligible character-skin browsing, with 30-day, 180-day, and unlimited choices.
- Fetch in official latest-update order until a precise, inclusive boundary or a safety stop is reached.
- Keep existing category, primary-sort, content filtering, local metric sorting, request-generation, and navigation behavior intact outside the eligible mode.
- Make collection and sorting decisions independently testable without network or WinUI dependencies.
- Deploy a matching x64 Release DLL/PDB/PRI set with a recoverable installation backup.

**Non-Goals:**

- Applying time windows to All Mods, search results, author pages, or non-character categories.
- Adding new GameBanana endpoints, per-mod detail requests, or external dependencies.
- Attempting an unbounded full-history download. “Unlimited” still obeys the 10-page/500-record safety cap.
- Redesigning mod cards or changing existing NSFW/blacklist settings.

## Decisions

### Keep network orchestration in the browser and move policy into a pure collector

The browser control will continue to own asynchronous page requests, Cloudflare retry, request-generation checks, and UI state. A pure recent-window component will own time-range definitions, cutoff calculation, page accumulation, deduplication, stop decisions, safety limits, and deterministic local ordering.

This is preferred over putting the loop entirely in code-behind because page boundaries and caps then remain testable. It is also preferred over moving network access into the sorter because doing so would couple a deterministic policy to WinUI, Cloudflare, and stale-request cancellation.

### Use a bounded incremental collection flow

Eligible time-window loading always starts at page 1 and requests category pages with `_tsDateUpdated,DESC`. Each successful page is offered to the collector. The collector processes no more than 10 pages and 500 raw records, deduplicates accepted rows by mod ID, and reports whether another page is needed.

For 30/90/180-day ranges, the cutoff is computed from an injected UTC instant and converted to Unix seconds. A record is in range when `DateUpdated >= cutoff`; the equality boundary is included. A missing or zero update timestamp is out of range and is never replaced by the publication timestamp. If a page contains any row older than the cutoff, the page is filtered and collection stops after that page because the server order is descending.

An empty page or complete-response metadata stops collection normally. A failed first page uses the existing blocking connection error. A later failed page keeps the accumulated candidates and shows a translated partial-results warning. Hitting a safety cap returns the accumulated candidates and records a capped stop reason for diagnostics without continuing indefinitely.

### Separate collection, content filtering, and final ordering

The transformation order is:

1. Query the selected character category in official latest-update order.
2. Merge pages, enforce the selected update-time window, and deduplicate by mod ID.
3. Apply the existing NSFW and submitter/mod-name blacklist checks.
4. Apply the selected local order.
5. Map the final records to cards.

The local orders are Latest Updated, Most Liked, Most Downloaded, and Most Commented. Metric orders use metric descending, then `DateUpdated` descending, then mod ID descending. Latest Updated uses `DateUpdated` descending, then mod ID descending. This keeps ties deterministic and prevents filtered rows from affecting displayed order.

### Make the two UI concepts explicit

When Character Skins, primary Latest Updated, and an empty search are active, the toolbar will expose separately labelled Time Range and Sort Within Range selectors. The time choices are 30 days, 90 days, 180 days, and Unlimited; 90 days is the initial selection. The sort choices are Latest Updated, Most Liked, Most Downloaded, and Most Commented.

The existing primary-sort selector remains available so Newest, Oldest, Most Viewed, and existing server-ranked modes are not removed. Selecting an incompatible primary sort, leaving Character Skins, or entering search hides the time-window controls and normalizes local ordering without changing the other mode's existing loading behavior. Returning to eligible mode uses the default 90-day/latest-updated state unless a compatible navigation entry is being restored.

English, Simplified Chinese, and Traditional Chinese receive native labels and messages; other locales keep the established English fallback.

### Preserve navigation state as one compatible unit

The mod-list navigation entry will store both the time range and local sort. Restoration first restores category, primary sort, search, and character category, then normalizes the saved range/sort against eligibility, updates controls without firing reload handlers, and performs exactly one list reload. Details and author entries continue to use defaults where they do not own list state.

## Risks / Trade-offs

- **[A 180-day or unlimited range may contain more than 500 updates]** → Stop at 10 pages or 500 raw records, expose capped diagnostics, and describe Unlimited as safety-limited rather than a promise to download all history.
- **[GameBanana returns duplicates or malformed timestamps]** → Count raw rows for safety, deduplicate by ID, and exclude missing/zero timestamps from finite windows without falling back to publication date.
- **[A later API page fails]** → Keep earlier valid candidates, show a non-blocking localized warning, and never present stale results from an older request generation.
- **[More toolbar controls reduce horizontal space]** → Reuse the dynamic-control pattern already used for character and secondary sorting, use compact localized labels, and show the two new controls only in the eligible mode.
- **[Changing the default alters the previous one-page Latest Updated experience]** → Limit the change to eligible character-skin browsing, retain all existing server-sort modes, and restore exact state through navigation.

## Migration Plan

1. Add regression tests and implement the pure time-window policy.
2. Replace the two-page branch with bounded iterative orchestration and post-filter local ordering.
3. Add localized time-range/sort UI and navigation-state support.
4. Run all tests and an x64 Release build; verify the generated PRI exists beside the DLL and PDB.
5. Stop the installed application, back up the current DLL/PDB/PRI, copy the three artifacts from the same build, verify SHA-256 equality, and restart through the launcher.
6. If acceptance fails, stop the application and restore the three backed-up artifacts as one set.

## Open Questions

None. The user approved the 90-day default, four range options, updated-time boundary, 10-page/500-record caps, and separate range/sort presentation.
