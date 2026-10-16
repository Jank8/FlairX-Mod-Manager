# GameBanana Category Thumbnail Reliability Implementation Plan

> **For Codex:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this plan task-by-task. Use `tdd` for Tasks 1-3 and `verification-before-completion` before declaring success.

**Goal:** Make GameBanana character-category discovery reliable so the existing thumbnail downloader can proceed.

**Architecture:** Keep `GameBananaService` as the orchestration boundary. Add a deterministic JSON category-tree parser, call the nested-category JSON API first, and preserve the current HTML scraper behind a hardened WebView2 fallback. The UI and thumbnail write path remain unchanged.

**Tech Stack:** .NET 10, C#, WinUI 3, `System.Text.Json`, WebView2, xUnit.

---

### Task 1: Add category-tree parser regression tests

**Files:**
- Create: `FlairX-Mod-Manager.Tests/FlairX-Mod-Manager.Tests.csproj`
- Create: `FlairX-Mod-Manager.Tests/GameBananaCategoryTreeTests.cs`
- Modify: `FlairX-Mod-Manager/FlairX-Mod-Manager.sln`

**Steps:**
1. Add a test that parses roots with nested `_aChildren` and confirms all category IDs are returned exactly once in source order.
2. Add an assertion that icon/profile/name fields survive deserialization.
3. Add empty and malformed JSON behavior tests.
4. Run the focused test and confirm RED because the parser API does not yet exist.

**Command:**
`dotnet test FlairX-Mod-Manager.Tests/FlairX-Mod-Manager.Tests.csproj -c Release -p:Platform=x64 --filter GameBananaCategoryTreeTests`

### Task 2: Implement JSON parsing and flattening

**Files:**
- Modify: `FlairX-Mod-Manager/Services/GameBananaService.cs`

**Steps:**
1. Map `_aChildren` on `CategoryRecord`.
2. Add `ParseCategoryTreeFromJson` and recursive flatten/de-duplication logic.
3. Run the focused test and confirm GREEN.

### Task 3: Add the JSON API primary path

**Files:**
- Modify: `FlairX-Mod-Manager/Services/GameBananaService.cs`
- Modify: `FlairX-Mod-Manager.Tests/GameBananaCategoryTreeTests.cs`

**Steps:**
1. Extract category fetching behind a small injectable HTTP seam so orchestration can be tested without live network dependency.
2. Add a failing test proving successful JSON results do not invoke fallback.
3. Add a failing test proving HTTP/empty JSON invokes fallback.
4. Implement the JSON-first orchestration and cache only non-empty results.
5. Run focused tests and confirm GREEN.

### Task 4: Harden WebView2 fallback

**Files:**
- Modify: `FlairX-Mod-Manager/Services/GameBananaService.cs`

**Steps:**
1. Attach the category WebView2 to `MainWindow.GetHiddenWebViewContainer()` before initialization.
2. Separate navigation completion from DOM expansion/extraction.
3. Apply independent navigation and render timeout budgets and use `TrySetResult` to avoid late-handler races.
4. Remove the control from the hidden container before closing it.

### Task 5: Verify and deploy

**Files:**
- Test: `FlairX-Mod-Manager.Tests/GameBananaCategoryTreeTests.cs`
- Build: `FlairX-Mod-Manager/FlairX-Mod-Manager.csproj`
- Deploy: `E:\flairx\app\FlairX Mod Manager.dll` and matching debug symbols if present

**Steps:**
1. Run all new unit tests.
2. Build Release x64.
3. Save current installed binaries under a timestamped `E:\flairx\backups\...` directory.
4. Gracefully close FlairX, replace only the changed application binaries, and restart it.
5. Trigger the Zenless Zone Zero thumbnail flow and inspect `E:\flairx\app\Settings\Application.log` for JSON API success and category `47714` availability.
6. If validation fails, restore the backed-up binaries.

