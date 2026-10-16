# GameBanana Character Filter Design

**Date:** 2026-08-18
**Status:** Approved interaction design; pending written-spec review

## Context

The GameBanana browser currently has a top-level filter (`All Mods` or `Character Skins`) and, in character-skin mode, a server-side sort selector. The sort selector already supports latest update, newest, oldest, most liked, most viewed, most downloaded, and most commented. It does not provide a way to restrict results to one character.

## Goal

Add a character selector that lets the user browse one GameBanana character category while retaining the existing sort, search, pagination, details, and back-navigation behavior.

## Non-goals

- Do not add duplicate liked/download controls; preserve the existing sort selector.
- Do not infer GameBanana characters from local mod-folder names.
- Do not download an entire parent category and filter only the currently loaded page.
- Do not change mod installation, extraction, or thumbnail behavior.

## User interface

In `Character Skins` mode, the top row is ordered as:

`Character Skins | All Characters / <character> | Latest Updated / Most Liked / Most Downloaded / ... | Search | Starter Pack`

- The character selector is placed between the top-level category filter and sort selector.
- It is visible only when `Character Skins` is selected.
- Its default option is the localized `All Characters` label.
- Character names come from GameBanana and are not translated locally.
- Selecting a character immediately resets pagination and reloads results.
- Returning to `All Mods` hides the selector and clears its selected character.

While the character tree is loading, the selector shows `All Characters` but remains disabled. A failure leaves `All Characters` selected and the existing parent-category browsing path usable.

## Data source and selection model

Use the existing `GameBananaService.GetCharacterCategoriesAsync(gameTag)` category-tree request and cache. The previously added nested-category API path supplies category IDs and hierarchy without scraping the visible page.

1. Resolve the game's character parent with `GetCharacterCategoryId(gameTag)`.
2. Find that parent in the returned category tree.
3. Enumerate its descendant character categories in source order.
4. Prefer leaf categories when nested grouping nodes are present.
5. De-duplicate by category ID.
6. Build selector options containing the category ID and display name.

`All Characters` maps to the existing character parent ID. A concrete character maps to its own GameBanana category ID.

## Query flow

- Initial/all-character results continue to call `GetModsByCategoryAsync` with the character parent ID.
- A selected character calls the same method with the selected character category ID.
- Existing `_currentSortOrder`, search text, and page number are passed unchanged.
- `Load More` uses the same selected category ID as the first page.
- Changing character resets the page to 1 and clears loaded-item de-duplication state through the existing reload path.

This keeps filtering and sorting server-side, so pagination is complete and stable.

## Navigation state

Extend the browser's list navigation entry with the selected character category ID. When navigating from a result to details or an author and then back:

- restore the top-level filter;
- restore the character selector and selected category ID;
- restore the sort order, search, page, and scroll position;
- avoid duplicate reloads while controls are being restored.

If the saved category no longer exists, fall back to `All Characters`.

## Error handling

- Unknown/empty category tree: keep `All Characters`, keep browsing via the parent category, and log a warning.
- Character-tree request failure: preserve the existing GameBanana error handling and WebView2 fallback, then degrade to `All Characters` if still unavailable.
- Character selection with an invalid ID: normalize to the parent category ID before issuing a request.
- No results for a character: show the existing empty-results state.

## Localization

Add browser-language keys for `All Characters` and the character selector's accessible name. Provide Simplified Chinese, Traditional Chinese, and English values. For other languages, the browser control explicitly detects a missing key and falls back to the English label until translations are added.

## Test strategy

Automated tests cover observable selection behavior through a small category-selection module:

1. A category tree produces ordered, de-duplicated character options.
2. Nested grouping nodes produce leaf character options rather than group headings.
3. `All Characters` resolves to the parent category ID.
4. A concrete character resolves to its category ID.
5. An invalid saved selection falls back to the parent category ID.
6. The selected ID is used consistently for initial load and pagination.
7. Navigation-state restoration retains character, sort, search, page, and scroll state.

Verification also includes a Release x64 build and a UI acceptance pass.

## Acceptance criteria

- In Zenless Zone Zero, selecting `Character Skins` displays a character selector.
- The selector includes `All Characters` and `Remielle Dan`.
- Choosing `Remielle Dan` loads only category `47714` results.
- `Most Liked` and `Most Downloaded` both work with the selected character.
- `Load More` remains within the selected character.
- Opening a mod and returning restores the character and sort selection.
- `All Mods` behavior is unchanged.
