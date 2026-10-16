## 1. Pure updated-window policy

- [x] 1.1 Add failing tests for 30/90/180-day cutoff calculation, inclusive boundary handling, and exclusive use of `DateUpdated`.
- [x] 1.2 Implement time-range, selection, stop-reason, and incremental collector types in `GameBananaRecentSecondarySorter.cs`.
- [x] 1.3 Add failing tests for cross-page deduplication, cutoff pagination, empty/complete pages, and continuation beyond 100 records.
- [x] 1.4 Implement collector continuation and stop decisions while retaining at most one record per mod ID.
- [x] 1.5 Add failing tests for 10-page and 500-raw-record caps, then implement both hard limits.
- [x] 1.6 Add failing tests for Latest Updated/Likes/Downloads/Comments ordering and content-filter-before-sort behavior, then implement deterministic ordering.

## 2. Browser state and controls

- [x] 2.1 Add failing static UI and localization tests for distinct time-range/sort controls and English, Simplified Chinese, and Traditional Chinese keys.
- [x] 2.2 Create dynamic Time Range and Sort Within Range controls with a 90-day/latest-updated default.
- [x] 2.3 Replace old secondary-sort labels with the new localized range, sort, partial, and capped-result text.
- [x] 2.4 Add failing eligibility/mode-switch tests and implement state normalization for category, primary sort, and search transitions.

## 3. Paginated collection and navigation

- [x] 3.1 Replace the fixed page-1/page-2 path with page-by-page `_tsDateUpdated,DESC` collection and request-generation checks after every await.
- [x] 3.2 Apply deduplication/window membership first, existing NSFW/blacklist filtering second, and local ordering third; suppress Load More for active window pools.
- [x] 3.3 Preserve first-page connection errors, accumulated results on later-page failure, and localized partial/capped notices.
- [x] 3.4 Add Time Range to every mod-list navigation capture and restore compatible range/sort controls without duplicate loads.
- [x] 3.5 Run all targeted GameBanana tests and fix regressions without weakening assertions.

## 4. Verification, deployment, and completion

- [x] 4.1 Run OpenSpec strict validation, whitespace checks, and the complete x64 Release test suite.
- [x] 4.2 Build `FlairX-Mod-Manager.csproj` in x64 Release and verify matching DLL/PDB/PRI timestamps and paths.
- [x] 4.3 Stop the installed manager, back up the installed DLL/PDB/PRI under `E:\flairx\backups`, deploy the same-build artifact set, and verify source/target SHA-256 hashes.
- [x] 4.4 Restart through the FlairX launcher and verify clean startup logs; focused UI acceptance was explicitly waived by the user.
- [x] 4.5 Mark OpenSpec tasks complete, archive the completed change into durable specs, rerun strict validation/status checks, and create the final implementation commit.
