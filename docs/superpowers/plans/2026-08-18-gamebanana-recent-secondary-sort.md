# GameBanana Recent-Updates Secondary Sorting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rank the latest 100 GameBanana character-skin candidates locally by downloads, likes, or comments while preserving the existing Latest Updated behavior when no secondary sort is selected.

**Architecture:** A pure service owns eligibility, page merging, deduplication, the 100-record cap, partial-result signaling, and deterministic sorting. The WinUI browser control owns the dynamic secondary ComboBox, two-page orchestration, request-generation protection, navigation state, mapping, and user feedback. The list API remains the only data source.

**Tech Stack:** C# 14, .NET 10, WinUI 3, System.Text.Json, xUnit, PowerShell 5.1-compatible deployment commands.

---

## File Map

- Create `FlairX-Mod-Manager/Services/GameBananaRecentSecondarySorter.cs`: pure secondary-sort rules and candidate-pool construction.
- Create `FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs`: ordering, eligibility, cap, deduplication, and partial-page tests.
- Modify `FlairX-Mod-Manager/Services/GameBananaService.cs`: add comment-count metadata fallback.
- Modify `FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs`: dynamic control, state, two-page loading, stable request generations, view-model mapping, navigation restore, and warnings.
- Modify `FlairX-Mod-Manager.Tests/GameBananaBrowserXamlTests.cs`: ensure the new ComboBox stays out of runtime-parsed XAML.
- Modify `FlairX-Mod-Manager/Language/GameBananaBrowser/en.json`: English labels and partial-result warning.
- Modify `FlairX-Mod-Manager/Language/GameBananaBrowser/zh-CN.json`: Simplified Chinese labels and warning.
- Modify `FlairX-Mod-Manager/Language/GameBananaBrowser/zh-TW.json`: Traditional Chinese labels and warning.

### Task 1: Pure candidate-pool sorter

**Files:**
- Create: `FlairX-Mod-Manager/Services/GameBananaRecentSecondarySorter.cs`
- Create: `FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs`

- [ ] **Step 1: Write failing ordering and pool tests**

Create tests using real `GameBananaService.ModRecord` values. Include these named facts:

```csharp
[Theory]
[InlineData(GameBananaRecentSecondarySort.MostDownloaded, 30, 20, 10)]
[InlineData(GameBananaRecentSecondarySort.MostLiked, 3, 2, 1)]
[InlineData(GameBananaRecentSecondarySort.MostCommented, 300, 200, 100)]
public void Build_RanksSelectedMetricDescending(
    GameBananaRecentSecondarySort sort,
    int firstExpected,
    int secondExpected,
    int thirdExpected)
{
    var records = new[]
    {
        Record(1, downloads: 10, likes: 3, comments: 200),
        Record(2, downloads: 30, likes: 1, comments: 100),
        Record(3, downloads: 20, likes: 2, comments: 300)
    };

    var result = GameBananaRecentSecondarySorter.Build(records, [], sort)!;
    var values = result.Records.Select(record => sort switch
    {
        GameBananaRecentSecondarySort.MostDownloaded => record.DownloadCount,
        GameBananaRecentSecondarySort.MostLiked => record.LikeCount,
        _ => record.PostCount
    });

    Assert.Equal([firstExpected, secondExpected, thirdExpected], values);
}

[Fact]
public void Build_UsesUpdatedDateThenIdForMetricTies()
{
    var result = GameBananaRecentSecondarySorter.Build(
        [Record(1, downloads: 10, updated: 100), Record(3, downloads: 10, updated: 200)],
        [Record(2, downloads: 10, updated: 200)],
        GameBananaRecentSecondarySort.MostDownloaded)!;

    Assert.Equal([3, 2, 1], result.Records.Select(record => record.Id));
}

[Fact]
public void Build_DeduplicatesAcrossPagesAndCapsAtOneHundred()
{
    var first = Enumerable.Range(1, 50).Select(id => Record(id)).ToArray();
    var second = Enumerable.Range(50, 60).Select(id => Record(id)).ToArray();

    var result = GameBananaRecentSecondarySorter.Build(
        first,
        second,
        GameBananaRecentSecondarySort.None)!;

    Assert.Equal(100, result.Records.Count);
    Assert.Equal(100, result.Records.Select(record => record.Id).Distinct().Count());
    Assert.Equal(1, result.Records[0].Id);
}

[Fact]
public void Build_ReturnsPartialFirstPageWhenSecondPageFailed()
{
    var result = GameBananaRecentSecondarySorter.Build(
        [Record(1)],
        null,
        GameBananaRecentSecondarySort.MostLiked)!;

    Assert.True(result.IsPartial);
    Assert.Single(result.Records);
}

[Fact]
public void Normalize_DisablesSecondarySortOutsideEligibleMode()
{
    Assert.Equal(
        GameBananaRecentSecondarySort.None,
        GameBananaRecentSecondarySorter.Normalize(
            isCharacterSkins: true,
            GameBananaService.CategorySortOrder.LatestUpdated,
            search: "remielle",
            GameBananaRecentSecondarySort.MostLiked));
}
```

Add a local `Record` factory with optional `downloads`, `likes`, `comments`, and `updated` arguments. Add companion assertions for None preserving source order, a null first page returning null, an empty second page not being partial, and eligible Latest Updated/no-search preserving the requested metric.

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
& 'C:\Users\Administrator\Documents\Codex\2026-08-18\wo-d\work\dotnet-sdk\dotnet.exe' test 'FlairX-Mod-Manager.Tests\FlairX-Mod-Manager.Tests.csproj' -c Release --no-restore -p:Platform=x64 --filter GameBananaRecentSecondarySorterTests
```

Expected: compilation fails because `GameBananaRecentSecondarySort` and `GameBananaRecentSecondarySorter` do not exist.

- [ ] **Step 3: Implement the minimal pure service**

Create:

```csharp
namespace FlairX_Mod_Manager.Services;

internal enum GameBananaRecentSecondarySort
{
    None,
    MostDownloaded,
    MostLiked,
    MostCommented
}

internal sealed record GameBananaRecentPoolResult(
    IReadOnlyList<GameBananaService.ModRecord> Records,
    bool IsPartial);

internal static class GameBananaRecentSecondarySorter
{
    internal static GameBananaRecentSecondarySort Normalize(
        bool isCharacterSkins,
        GameBananaService.CategorySortOrder primarySort,
        string? search,
        GameBananaRecentSecondarySort requested)
    {
        return isCharacterSkins &&
               primarySort == GameBananaService.CategorySortOrder.LatestUpdated &&
               string.IsNullOrWhiteSpace(search)
            ? requested
            : GameBananaRecentSecondarySort.None;
    }

    internal static GameBananaRecentPoolResult? Build(
        IReadOnlyList<GameBananaService.ModRecord>? firstPage,
        IReadOnlyList<GameBananaService.ModRecord>? secondPage,
        GameBananaRecentSecondarySort sort)
    {
        if (firstPage is null)
        {
            return null;
        }

        var candidates = firstPage
            .Concat(secondPage ?? [])
            .DistinctBy(record => record.Id)
            .Take(100);

        var ordered = sort switch
        {
            GameBananaRecentSecondarySort.MostDownloaded => Order(candidates, record => record.GetDownloadCount()),
            GameBananaRecentSecondarySort.MostLiked => Order(candidates, record => record.GetLikeCount()),
            GameBananaRecentSecondarySort.MostCommented => Order(candidates, record => record.PostCount),
            _ => candidates.ToList()
        };

        return new GameBananaRecentPoolResult(ordered, secondPage is null);
    }

    private static IReadOnlyList<GameBananaService.ModRecord> Order(
        IEnumerable<GameBananaService.ModRecord> records,
        Func<GameBananaService.ModRecord, int> metric)
    {
        return records
            .OrderByDescending(metric)
            .ThenByDescending(record => record.DateUpdated)
            .ThenByDescending(record => record.Id)
            .ToList();
    }
}
```

- [ ] **Step 4: Run tests and verify GREEN**

Run the Task 1 command. Expected: all `GameBananaRecentSecondarySorterTests` pass.

- [ ] **Step 5: Commit**

```powershell
git add -- 'FlairX-Mod-Manager/Services/GameBananaRecentSecondarySorter.cs' 'FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs'
git commit -m 'feat: add recent GameBanana secondary sorter'
```

### Task 2: Count fallback and card mapping

**Files:**
- Modify: `FlairX-Mod-Manager/Services/GameBananaService.cs`
- Modify: `FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs`
- Modify: `FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs`

- [ ] **Step 1: Add failing count-mapping tests**

Add:

```csharp
[Fact]
public void GetPostCount_ReadsMetadataFallback()
{
    var record = new GameBananaService.ModRecord
    {
        Metadata = new Dictionary<string, object>
        {
            ["_nPostCount"] = JsonDocument.Parse("17").RootElement.Clone()
        }
    };

    Assert.Equal(17, record.GetPostCount());
}

[Fact]
public void CreateModViewModel_MapsDownloadAndCommentCounts()
{
    var record = Record(1, downloads: 42, comments: 7);

    var viewModel = GameBananaBrowserUserControl.CreateModViewModel(
        record,
        installedText: "Installed",
        isInstalled: false);

    Assert.Equal(42, viewModel.DownloadCount);
    Assert.Equal(7, viewModel.CommentCount);
}
```

Add `using System.Text.Json;` and `using FlairX_Mod_Manager.Pages;`.

- [ ] **Step 2: Run tests and verify RED**

Run the Task 1 test command. Expected: compilation fails because `GetPostCount`, `CommentCount`, and `CreateModViewModel` do not exist.

- [ ] **Step 3: Implement metadata fallback and one mapping function**

Add beside the existing count helpers in `ModRecord`:

```csharp
public int GetPostCount()
{
    if (PostCount > 0) return PostCount;
    if (Metadata != null && Metadata.TryGetValue("_nPostCount", out var value) &&
        value is JsonElement element && element.ValueKind == JsonValueKind.Number)
    {
        return element.GetInt32();
    }

    return 0;
}
```

Add `CommentCount` to `ModViewModel`, then add this internal factory to the browser control:

```csharp
internal static ModViewModel CreateModViewModel(
    GameBananaService.ModRecord record,
    string installedText,
    bool isInstalled)
{
    var image = record.PreviewMedia?.Images?.FirstOrDefault();
    return new ModViewModel
    {
        Id = record.Id,
        Name = record.Name ?? "",
        AuthorName = record.Submitter?.Name ?? "Unknown",
        ProfileUrl = record.ProfileUrl ?? "",
        ImageUrl = image is null ? null : GameBananaService.GetListPreviewImageUrl(image),
        DownloadCount = record.GetDownloadCount(),
        LikeCount = record.GetLikeCount(),
        CommentCount = record.GetPostCount(),
        ViewCount = record.GetViewCount(),
        DateAdded = record.DateAdded,
        DateModified = record.DateModified,
        DateUpdated = record.DateUpdated,
        IsRated = record.HasAnyContentWarning,
        IsInstalled = isInstalled,
        InstalledText = installedText
    };
}
```

Replace both duplicated initial-load and load-more view-model initializers with this factory. Preserve the initial synchronous installed check and the load-more lazy installed check by passing the appropriate boolean.

Extract the shared content filters as well:

```csharp
private bool ShouldIncludeMod(GameBananaService.ModRecord record)
{
    if (record.HasAnyContentWarning && SettingsManager.Current.HideNSFWMods)
        return false;

    return !IsModBlacklisted(record.Submitter?.Name ?? "", record.Name ?? "");
}
```

Use `ShouldIncludeMod` in both initial-load and load-more loops before calling the factory. Finally, change the Task 1 comment selector from `record.PostCount` to `record.GetPostCount()`.

- [ ] **Step 4: Run tests and verify GREEN**

Run the Task 1 test command. Expected: all sorter and mapping tests pass.

- [ ] **Step 5: Commit**

```powershell
git add -- 'FlairX-Mod-Manager/Services/GameBananaService.cs' 'FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs' 'FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs'
git commit -m 'refactor: centralize GameBanana card mapping'
```

### Task 3: Dynamic secondary-sort control and localization

**Files:**
- Modify: `FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs`
- Modify: `FlairX-Mod-Manager.Tests/GameBananaBrowserXamlTests.cs`
- Modify: `FlairX-Mod-Manager/Language/GameBananaBrowser/en.json`
- Modify: `FlairX-Mod-Manager/Language/GameBananaBrowser/zh-CN.json`
- Modify: `FlairX-Mod-Manager/Language/GameBananaBrowser/zh-TW.json`

- [ ] **Step 1: Extend the XAML regression test and verify RED**

Add assertions that XAML contains no element named `SecondarySortComboBox`, while code-behind contains `CreateSecondarySortComboBox();` and `private void CreateSecondarySortComboBox()`.

Run:

```powershell
& 'C:\Users\Administrator\Documents\Codex\2026-08-18\wo-d\work\dotnet-sdk\dotnet.exe' test 'FlairX-Mod-Manager.Tests\FlairX-Mod-Manager.Tests.csproj' -c Release --no-restore -p:Platform=x64 --filter GameBananaBrowserXamlTests
```

Expected: FAIL because the creation method is absent.

- [ ] **Step 2: Create and populate the dynamic ComboBox**

Add fields for `SecondarySortComboBox`, `_currentSecondarySort`, and `_isUpdatingSecondarySort`. Call `CreateSecondarySortComboBox()` immediately after `CreateCharacterFilterComboBox()`.

The method must insert one Auto column at index 3, move `SearchBox` to column 4 and `StarterPackButton` to column 5, then add a collapsed ComboBox in column 3 with 160 minimum width, the existing 12-pixel right margin, and `SecondarySortComboBox_SelectionChanged` attached.

After loading `_lang`, populate indices in this exact order:

```csharp
SecondarySortComboBox.Items.Add(GetTranslationOrDefault("SecondarySort_None", "No Secondary Sort"));
SecondarySortComboBox.Items.Add(GetTranslationOrDefault("SecondarySort_Downloads", "Most Downloaded"));
SecondarySortComboBox.Items.Add(GetTranslationOrDefault("SecondarySort_Likes", "Most Liked"));
SecondarySortComboBox.Items.Add(GetTranslationOrDefault("SecondarySort_Comments", "Most Commented"));
SecondarySortComboBox.SelectedIndex = 0;
```

Add language keys plus `SecondarySort_PartialWarning` with these values:

```json
// en.json
"SecondarySort_None": "No Secondary Sort",
"SecondarySort_Downloads": "Most Downloaded",
"SecondarySort_Likes": "Most Liked",
"SecondarySort_Comments": "Most Commented",
"SecondarySort_PartialWarning": "Only the first 50 recent mods could be loaded; the available results were sorted."

// zh-CN.json
"SecondarySort_None": "不二次排序",
"SecondarySort_Downloads": "按下载量",
"SecondarySort_Likes": "按喜爱数",
"SecondarySort_Comments": "按评论数",
"SecondarySort_PartialWarning": "只能加载最近更新的前 50 个 Mod，已对现有结果排序。"

// zh-TW.json
"SecondarySort_None": "不進行二次排序",
"SecondarySort_Downloads": "依下載量",
"SecondarySort_Likes": "依喜愛數",
"SecondarySort_Comments": "依評論數",
"SecondarySort_PartialWarning": "只能載入最近更新的前 50 個 Mod，已對現有結果排序。"
```

- [ ] **Step 3: Implement eligibility and selection behavior**

Add `UpdateSecondarySortAvailability(bool resetIfIneligible = true)`. Normalize using the pure service, show the control only for Character Skins + Latest Updated + empty search, and set index 0 under `_isUpdatingSecondarySort` when resetting. Map selection indices 1/2/3 to Downloads/Likes/Comments, reset page to 1, and call `LoadModsAsync()`.

Call the availability method after category changes, primary-sort changes, search submissions/text transitions, and navigation restores.

- [ ] **Step 4: Run targeted and full tests**

Run the Task 3 test command, then the full test command from Task 6. Expected: all tests pass.

- [ ] **Step 5: Commit**

```powershell
git add -- 'FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs' 'FlairX-Mod-Manager.Tests/GameBananaBrowserXamlTests.cs' 'FlairX-Mod-Manager/Language/GameBananaBrowser/en.json' 'FlairX-Mod-Manager/Language/GameBananaBrowser/zh-CN.json' 'FlairX-Mod-Manager/Language/GameBananaBrowser/zh-TW.json'
git commit -m 'feat: add recent-mod secondary sort control'
```

### Task 4: Two-page loading, partial fallback, and stale-request protection

**Files:**
- Modify: `FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs`
- Modify: `FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs`

- [ ] **Step 1: Add a failing fixed-pool pagination-policy test**

Add:

```csharp
[Theory]
[InlineData(GameBananaRecentSecondarySort.None, true)]
[InlineData(GameBananaRecentSecondarySort.MostDownloaded, false)]
[InlineData(GameBananaRecentSecondarySort.MostLiked, false)]
[InlineData(GameBananaRecentSecondarySort.MostCommented, false)]
public void CanLoadMore_AllowsOnlyNone(
    GameBananaRecentSecondarySort sort,
    bool expected)
{
    Assert.Equal(expected, GameBananaRecentSecondarySorter.CanLoadMore(sort, hasMorePages: true));
}
```

Run the sorter tests. Expected: compilation fails because `CanLoadMore` does not exist.

- [ ] **Step 2: Add request-generation and eligible metric loading**

Add `private int _loadGeneration;`. At the start of `LoadModsAsync`, capture:

```csharp
var loadGeneration = Interlocked.Increment(ref _loadGeneration);
```

After every awaited network or Cloudflare operation and before every UI success/error mutation, return when `loadGeneration != _loadGeneration`.

Extract the existing category/all-mod request choice into:

```csharp
private Task<GameBananaService.ModListResponse?> FetchModsPageAsync(int page)
{
    if (_currentCategoryFilter == CategoryFilter.CharacterSkins)
    {
        return GameBananaService.GetModsByCategoryAsync(
            _gameTag,
            GetActiveCharacterCategoryId(),
            page,
            _currentSearch,
            _currentSortOrder);
    }

    return GameBananaService.GetModsAsync(
        _gameTag, page, _currentSearch, null, null, null, null, null, null);
}
```

When normalized secondary sort is not None, request page 1 and then page 2 through this method. Pass `response1?.Records` and `response2?.Records` to `GameBananaRecentSecondarySorter.Build`. Treat a null page 2 as partial; treat an empty page 2 as complete. Run the existing NSFW and blacklist filters on the merged ordered records before mapping.

When secondary sort is None, retain the existing one-page response and existing auto-load behavior unchanged.

Add the tested policy and use it whenever setting Load More visibility:

```csharp
internal static bool CanLoadMore(GameBananaRecentSecondarySort sort, bool hasMorePages)
    => sort == GameBananaRecentSecondarySort.None && hasMorePages;
```

- [ ] **Step 3: Implement partial warning and Load More rules**

For partial results, set `ConnectionErrorBar.Severity = InfoBarSeverity.Warning`, set its translated partial-warning message, and open it without hiding the list. Restore Error severity for full connection failures and close/reset the bar on the next successful complete load.

Set `_hasMorePages = false` and hide `LoadMoreMainModsButton` whenever secondary sort is active. Do not call `AutoLoadMorePagesAsync` or `CheckIfNeedMoreContent` in that mode.

- [ ] **Step 4: Run tests and build**

Run the full test command from Task 6 and the Release build command from Task 6. Expected: all tests pass and build exits 0.

- [ ] **Step 5: Commit**

```powershell
git add -- 'FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs' 'FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs'
git commit -m 'feat: load and rank one hundred recent mods'
```

### Task 5: Navigation-state restoration

**Files:**
- Modify: `FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs`
- Modify: `FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs`

- [ ] **Step 1: Add failing normalization/restoration cases**

Add theory cases covering every incompatible state: All Mods, non-Latest primary sort, whitespace versus non-empty search, and compatible Latest Updated. Verify RED for any missing normalization behavior.

- [ ] **Step 2: Extend `NavigationEntry` and every capture point**

Add this final positional member with a default:

```csharp
GameBananaRecentSecondarySort SecondarySort = GameBananaRecentSecondarySort.None
```

Every navigation push representing the mod-list state must pass `SecondarySort: _currentSecondarySort`. Detail-only and author-only entries may rely on the default when they do not preserve list filters.

- [ ] **Step 3: Restore compatible state safely**

In `RestoreNavigationEntryAsync`, normalize `entry.SecondarySort` against the restored category, primary sort, and search. Update the ComboBox under `_isUpdatingSecondarySort`, then call `LoadModsAsync()` exactly once. Use this index mapping:

```csharp
GameBananaRecentSecondarySort.MostDownloaded => 1,
GameBananaRecentSecondarySort.MostLiked => 2,
GameBananaRecentSecondarySort.MostCommented => 3,
_ => 0
```

- [ ] **Step 4: Run tests and commit**

Run the full test command from Task 6. Expected: all tests pass.

```powershell
git add -- 'FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs' 'FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs'
git commit -m 'feat: preserve eligible secondary sort navigation'
```

### Task 6: Full verification, deployment, and acceptance

**Files:**
- Verify all modified source and test files.
- Deploy matching artifacts to `E:\flairx\app`.

- [ ] **Step 1: Run formatting and repository checks**

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors; only intentional source changes and local ignored/untracked build artifacts are listed.

- [ ] **Step 2: Run all automated tests**

```powershell
& 'C:\Users\Administrator\Documents\Codex\2026-08-18\wo-d\work\dotnet-sdk\dotnet.exe' test 'FlairX-Mod-Manager.Tests\FlairX-Mod-Manager.Tests.csproj' -c Release --no-restore -p:Platform=x64
```

Expected: all tests pass with zero failures.

- [ ] **Step 3: Produce the x64 Release artifacts**

```powershell
& 'C:\Users\Administrator\Documents\Codex\2026-08-18\wo-d\work\dotnet-sdk\dotnet.exe' build 'FlairX-Mod-Manager\FlairX-Mod-Manager.csproj' -c Release --no-restore -p:Platform=x64
```

Expected: build succeeds and produces matching `FlairX Mod Manager.dll`, `.pdb`, and `.pri` under `FlairX-Mod-Manager\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64`.

- [ ] **Step 4: Back up and deploy the matching artifact set**

Stop only the `FlairX Mod Manager` process. Create `E:\flairx\backups\<timestamp>-recent-secondary-sort`, copy the installed DLL/PDB/PRI into it, then copy all three files from the same build output into `E:\flairx\app`. Compare SHA-256 hashes source-to-target before restarting `E:\flairx\FlairX Mod Manager Launcher.exe`.

- [ ] **Step 5: Perform UI acceptance**

Verify in the installed app:

1. the globe button opens GameBanana;
2. Character Skins + Latest Updated + empty search shows the second sort;
3. None retains existing Load More behavior;
4. each metric option loads at most 100 candidates and removes Load More;
5. visible order matches the chosen count with updated-date/ID ties stable;
6. changing primary sort, category mode, or entering search resets/hides the second sort;
7. Back navigation restores an eligible selection;
8. the application log contains no new XAML or unhandled loading errors.

- [ ] **Step 6: Commit verification adjustments and push**

If acceptance required no code adjustment, do not create an empty commit. Otherwise rerun Steps 1–5 and commit the tested adjustment. Push `custom/gamebanana-enhancements` to the existing `fork` remote only after the installed build passes acceptance.
