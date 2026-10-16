# GameBanana Category Thumbnail Reliability Design

**Date:** 2026-08-18

## Problem

FlairX Mod Manager 4.0.8 loads the GameBanana category tree by opening the JavaScript-rendered `games/cattree/{gameId}` page in an off-screen WebView2. The WebView is not attached to the hidden visual container and the same 30-second budget covers navigation plus fixed 5-second and 2-second render waits. Application logs show navigation can complete shortly before the outer timeout, causing a false failure before any thumbnail download starts.

## Approved approach

1. Use GameBanana's JSON category-tree endpoint as the normal path:
   `https://gamebanana.com/apiv13/Util/ModCategory/NestedStructure?_idGameRow={gameId}`.
2. Deserialize and recursively flatten `_aChildren`, preserving category IDs, names, profile URLs, and icon URLs while de-duplicating IDs.
3. Retain the HTML/WebView2 scraper as a compatibility fallback if the JSON request fails or yields no categories.
4. Attach the fallback WebView2 to the application's existing hidden container so it receives a real viewport.
5. Give fallback navigation and post-navigation DOM rendering separate timeout budgets so a completed navigation cannot lose a race against fixed render delays.
6. Cache only a non-empty result.

## Scope

The change is limited to GameBanana category discovery. It does not modify mod folders, downloaded files, user configuration, character mappings, or the thumbnail image writer.

## Failure handling

- Unknown game tag: log and return `null`, as today.
- JSON endpoint network/HTTP/parse failure: log a warning and try the WebView2 fallback.
- Empty JSON tree: log a warning and try the WebView2 fallback.
- WebView2 navigation/render timeout: log which phase timed out and return `null`.
- Successful non-empty JSON or fallback result: cache and return it.

## Verification

- Unit-test JSON tree flattening, including nested children and duplicate IDs.
- Unit-test empty/malformed input behavior.
- Build the WinUI application in Release x64.
- Deploy only after backing up the installed application binaries.
- Start the deployed application and verify that the Zenless Zone Zero category request succeeds and includes `Remielle Dan` (category ID `47714`) without `WebView2 rendering timed out`.

