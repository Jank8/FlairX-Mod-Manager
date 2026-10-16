# GameBanana Recent-Updates Secondary Sorting Design

## Goal

Let users browse the 100 most recently updated character-skin mods, then rank that fixed candidate pool locally by downloads, likes, or comments. GameBanana remains responsible for selecting the recent candidates; FlairX performs the secondary ranking.

## Scope

The feature applies only when all of these conditions are true:

- the active category filter is **Character Skins**;
- the primary sort is **Latest Updated**;
- the search box is empty.

The secondary-sort choices are:

- None;
- Most Downloaded;
- Most Liked;
- Most Commented.

All metric sorts are descending. This change does not add secondary sorting to All Mods, author results, search results, or other primary sort modes.

## User Interface

A secondary-sort ComboBox appears beside the existing primary-sort ComboBox whenever the active view is eligible. It remains visible but defaults to **None** so the current behavior is unchanged until the user selects a metric.

Selecting another primary sort, leaving Character Skins, or starting a search resets the secondary sort to **None** and hides the secondary-sort control. Returning through the browser navigation stack restores the prior selection only when the restored state is still eligible.

When a metric is selected, FlairX loads and displays the complete candidate pool at once. The normal Load More control is hidden because the secondary-sort mode is explicitly limited to the latest 100 candidates.

English, Simplified Chinese, and Traditional Chinese receive native labels. Other locales use the existing English fallback behavior.

## Candidate Pool and Ordering

GameBanana is queried for page 1 and page 2 with:

- the selected character category;
- primary order `_tsDateUpdated,DESC`;
- 50 records per page;
- no search term.

The two pages are combined in page order and deduplicated by Mod ID. The candidate pool therefore contains at most 100 records before local content filtering. Existing NSFW and blacklist filters are then applied.

The remaining records are ordered by:

1. selected metric descending;
2. `DateUpdated` descending;
3. Mod ID descending.

The last two keys make ties deterministic while preserving recency. Selecting **None** uses the existing single-page, server-ordered flow and existing pagination.

## Components

### Secondary-sort model

Add a small enum representing None, Downloads, Likes, and Comments. Include the selection in the browser navigation entry so Back navigation can restore it safely.

### Local sorter

Add a focused, pure helper that accepts `GameBananaService.ModRecord` values and returns a deterministically ordered sequence. It owns page merging, deduplication, the 100-record limit, metric selection, and tie-breaking; it does not perform network or UI work.

### View model mapping

Populate `DownloadCount` and a new `CommentCount` on every list card from the list API response. No per-mod detail requests are made. The existing card layout does not change.

### Loading orchestration

The browser control decides between two paths:

- **None:** retain the existing page-one load and Load More behavior.
- **Metric selected:** request the first two Latest Updated pages, merge, filter, locally sort, and replace the displayed collection once.

The same mapping and filtering code should be shared by the initial and load-more paths to avoid count-field drift.

## Failure Handling

- If page 1 fails, use the existing connection-error behavior and display no stale results.
- If page 1 succeeds but page 2 fails, sort and display page 1, then show a non-blocking warning that only the first 50 recent candidates were available.
- If either page is empty, use all successfully returned records without treating the empty page as an error.
- Rapid filter or sort changes must not allow an older request to overwrite the newer selection. A monotonically increasing request generation is captured by each load, and only the current generation may update the displayed collection or error state.

## Testing

Automated tests will cover:

- downloads, likes, and comments descending;
- `DateUpdated` and Mod ID tie-breaking;
- None preserving source order;
- merging at most two 50-record pages;
- duplicate Mod IDs across page boundaries;
- existing NSFW and blacklist filtering before final ordering;
- page-2 failure falling back to page 1;
- secondary-sort eligibility and reset rules;
- navigation-state restoration;
- mapping download and comment counts into the card model.

Verification also includes the full existing test suite, an x64 Release build, deployment of matching DLL/PDB/PRI artifacts, and a manual UI acceptance pass in the installed FlairX application.

## Acceptance Criteria

1. In Character Skins with Latest Updated and no search, the new secondary-sort control is available.
2. None behaves exactly like the existing Latest Updated mode.
3. Each metric option ranks the latest candidate pool by the chosen count, descending, with deterministic ties.
4. Metric mode uses no more than the first 100 GameBanana Latest Updated records and does not offer Load More.
5. Other category, primary-sort, and search modes cannot retain an incompatible secondary sort.
6. A failed second page still yields a usable, visibly identified 50-record result.
7. Existing character selection, primary sorts, image quality, and download behavior remain unchanged.
