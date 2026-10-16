# GameBanana Updated-Time Window and Local Sorting Design

## Goal

Replace the fixed first-100 candidate pool in eligible GameBanana character-skin browsing with a bounded last-update-time window. The default window is 90 days, membership uses `_tsDateUpdated`, and all eligible candidates are collected before existing content filters and deterministic local ordering are applied.

## Scope and User Experience

The feature is eligible only for Character Skins with primary Latest Updated and an empty search. The toolbar presents two separately labelled concepts:

- **Time Range:** 30 days, 90 days, 180 days, Unlimited; default 90 days.
- **Sort Within Range:** Latest Updated, Most Liked, Most Downloaded, Most Commented; default Latest Updated.

Selecting another primary sort, leaving Character Skins, or entering search hides and normalizes the time-window controls while preserving that mode's existing behavior. Returning from a mod details page restores a compatible range and local sort. English, Simplified Chinese, and Traditional Chinese receive native labels and notices; other locales use the established English fallback.

## Data Flow

1. Start at category page 1 and request `_tsDateUpdated,DESC`, 50 records per page.
2. For finite windows, calculate an inclusive Unix-time cutoff from an injected UTC instant.
3. Feed successful pages to a pure collector that counts pages/raw records, deduplicates by mod ID, accepts only `DateUpdated >= cutoff`, and reports whether collection should continue.
4. Stop on a crossed cutoff, empty page, complete metadata, failed later page, 10 pages, or 500 raw records.
5. Apply the existing character-category constraint, NSFW setting, and submitter/mod-name blacklist to the deduplicated in-window candidates.
6. Locally sort the survivors and map them to cards.

Missing or zero `DateUpdated` is outside every finite window. Publication and modification timestamps are never fallbacks. A cutoff-equal record is included. Unlimited has no time cutoff but still stops at 10 pages/500 raw records.

## Ordering

- Latest Updated: `DateUpdated` descending, mod ID descending.
- Most Liked: like count descending, `DateUpdated` descending, mod ID descending.
- Most Downloaded: download count descending, `DateUpdated` descending, mod ID descending.
- Most Commented: comment count descending, `DateUpdated` descending, mod ID descending.

Deduplication and content filtering happen before local ordering. This guarantees that the displayed ranking is computed over the complete retained candidate set rather than a fixed first-100 subset.

## Components

### Pure window policy

Generalize the existing recent-secondary-sort helper into an incremental, network-free component. It owns the time-range enum, cutoff calculation, page/raw-record caps, deduplication, boundary stop decision, stop reason, and final deterministic ordering. Tests pass a fixed UTC time and real `ModRecord` values.

### Browser orchestration

The WinUI browser keeps responsibility for GameBanana requests, Cloudflare retry, request-generation checks, localized notices, NSFW/blacklist filtering, card mapping, and control visibility. It requests one page at a time and checks the current generation after every await so older selections cannot overwrite newer ones.

The first-page failure retains the current connection-error path. A later failure displays accumulated candidates with a warning. A safety cap returns accumulated candidates and logs/displays a capped-result notice without offering Load More for the bounded pool.

### Navigation state

Add Time Range beside the existing local sort in `NavigationEntry`. Restoration normalizes the pair after category, primary sort, search, and character selection are restored, updates both controls under event-suppression guards, and invokes a single load.

## Testing

Automated coverage includes:

- page continuation beyond the first 100 records;
- cutoff crossing and cutoff equality;
- exclusive use of `_tsDateUpdated`/`DateUpdated`;
- missing update timestamp behavior;
- duplicate IDs across pages;
- all four local orders and deterministic ties;
- 10-page and 500-record caps;
- empty, complete, and later-failure stop paths;
- eligibility, reset, and mode switching;
- details/back navigation restoration without duplicate loads;
- English, Simplified Chinese, and Traditional Chinese keys;
- preservation of dynamic XAML control creation.

Completion requires the full test suite, an x64 Release build, a generated PRI beside the matching DLL/PDB, a recoverable `E:\flairx` backup, hash-verified deployment of all three artifacts from the same build, restart, and a focused installed-app acceptance check.

## Alternatives Considered

Putting the paging loop directly in code-behind would minimize file changes but make cutoff and cap behavior hard to test. Moving network calls into the sorter would make the view smaller but couple deterministic policy to WinUI, Cloudflare, and cancellation. Keeping network orchestration in the browser with a pure incremental policy provides the best regression protection while fitting the existing architecture.

## Acceptance Criteria

1. Eligible character-skin browsing defaults to the latest 90 days and visibly separates range from ordering.
2. Bounded membership uses only `_tsDateUpdated`, includes the exact boundary, and can collect more than 100 candidates.
3. Collection never exceeds 10 pages or 500 raw records.
4. Deduplication and existing content filters precede local ordering.
5. Latest Updated, Likes, Downloads, and Comments produce deterministic orders over the retained pool.
6. Incompatible modes keep their previous behavior and cannot retain invalid time-window state.
7. Returning from details restores compatible range and order state.
8. New user-facing text is present in English, Simplified Chinese, and Traditional Chinese.
9. The deployed DLL, PDB, and PRI are produced by the same verified x64 Release build and can be rolled back from the installation backup.
