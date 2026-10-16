# GameBanana Updated-Time Window Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fixed first-100 GameBanana character-skin candidate pool with a safe, last-update-based 30/90/180-day or unlimited collector and deterministic within-range ordering.

**Architecture:** Keep asynchronous GameBanana and Cloudflare orchestration in the WinUI browser, while extending the existing pure recent-sort service into an incremental window collector. The collector owns time boundaries, deduplication, stop reasons, and caps; the browser applies existing content filters, invokes pure ordering, owns localized controls/notices, and preserves navigation state.

**Tech Stack:** C# 14, .NET 10, WinUI 3, System.Text.Json, xUnit, OpenSpec 1.4.1, PowerShell 7.

---

## File Map

- Modify `FlairX-Mod-Manager/Services/GameBananaRecentSecondarySorter.cs`: time-range and local-sort enums, selection normalization, cutoff calculation, incremental page collector, stop reasons, filter/order helper.
- Modify `FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs`: dynamic range/sort controls, bounded paging loop, request-generation checks, notices, content-filter/order sequence, navigation capture/restore.
- Modify `FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs`: boundary, timestamp, pagination, deduplication, caps, mode normalization, content filtering, and ordering tests.
- Modify `FlairX-Mod-Manager.Tests/GameBananaBrowserXamlTests.cs`: dynamic-control, navigation-state, and localization regression tests.
- Modify `FlairX-Mod-Manager/Language/GameBananaBrowser/en.json`: English range/sort labels and partial/capped notices.
- Modify `FlairX-Mod-Manager/Language/GameBananaBrowser/zh-CN.json`: Simplified Chinese equivalents.
- Modify `FlairX-Mod-Manager/Language/GameBananaBrowser/zh-TW.json`: Traditional Chinese equivalents.
- Modify `openspec/changes/gamebanana-updated-time-window/tasks.md`: track completed implementation and verification tasks before archival.

### Task 1: Time-range model and cutoff semantics

**Files:**
- Modify: `FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs`
- Modify: `FlairX-Mod-Manager/Services/GameBananaRecentSecondarySorter.cs`

- [ ] **Step 1: Replace the old fixed-pool assumptions with one failing cutoff test**

Add the time-range theory and exact-boundary case:

```csharp
private static readonly DateTimeOffset Now =
    new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

[Theory]
[InlineData(GameBananaUpdatedTimeRange.Last30Days, 30)]
[InlineData(GameBananaUpdatedTimeRange.Last90Days, 90)]
[InlineData(GameBananaUpdatedTimeRange.Last180Days, 180)]
public void GetCutoffUnixSeconds_UsesSelectedRange(
    GameBananaUpdatedTimeRange range,
    int expectedDays)
{
    Assert.Equal(
        Now.AddDays(-expectedDays).ToUnixTimeSeconds(),
        GameBananaRecentSecondarySorter.GetCutoffUnixSeconds(range, Now));
}

[Fact]
public void GetCutoffUnixSeconds_UnlimitedHasNoCutoff()
{
    Assert.Null(GameBananaRecentSecondarySorter.GetCutoffUnixSeconds(
        GameBananaUpdatedTimeRange.Unlimited,
        Now));
}
```

- [ ] **Step 2: Run the targeted tests and verify RED**

Run:

```powershell
& 'C:\Users\Administrator\Documents\Codex\2026-08-18\wo-d\work\dotnet-sdk\dotnet.exe' test 'FlairX-Mod-Manager.Tests\FlairX-Mod-Manager.Tests.csproj' -c Release --no-restore -p:Platform=x64 --filter GameBananaRecentSecondarySorterTests
```

Expected: compilation fails because `GameBananaUpdatedTimeRange` and `GetCutoffUnixSeconds` do not exist.

- [ ] **Step 3: Add the minimal range and selection types**

Replace the `None`-based local-sort enum and add the range/selection types:

```csharp
internal enum GameBananaUpdatedTimeRange
{
    Last30Days,
    Last90Days,
    Last180Days,
    Unlimited
}

internal enum GameBananaRecentSecondarySort
{
    LatestUpdated,
    MostLiked,
    MostDownloaded,
    MostCommented
}

internal readonly record struct GameBananaRecentSelection(
    GameBananaUpdatedTimeRange TimeRange,
    GameBananaRecentSecondarySort Sort)
{
    internal static GameBananaRecentSelection Default => new(
        GameBananaUpdatedTimeRange.Last90Days,
        GameBananaRecentSecondarySort.LatestUpdated);
}
```

Implement the pure cutoff:

```csharp
internal static long? GetCutoffUnixSeconds(
    GameBananaUpdatedTimeRange range,
    DateTimeOffset nowUtc) => range switch
{
    GameBananaUpdatedTimeRange.Last30Days => nowUtc.AddDays(-30).ToUnixTimeSeconds(),
    GameBananaUpdatedTimeRange.Last90Days => nowUtc.AddDays(-90).ToUnixTimeSeconds(),
    GameBananaUpdatedTimeRange.Last180Days => nowUtc.AddDays(-180).ToUnixTimeSeconds(),
    _ => null
};
```

- [ ] **Step 4: Run the targeted tests and verify GREEN**

Before running, update existing `GameBananaRecentSecondarySort.None` test references to `LatestUpdated` while retaining the current `Build` compatibility path until Task 5 replaces its last browser caller. Run the Task 1 command. Expected: all `GameBananaRecentSecondarySorterTests` pass.

- [ ] **Step 5: Commit the vertical slice**

```powershell
git add -- 'FlairX-Mod-Manager/Services/GameBananaRecentSecondarySorter.cs' 'FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs'
git commit -m 'feat: define GameBanana updated-time ranges'
```

### Task 2: Incremental pagination, deduplication, boundary, and caps

**Files:**
- Modify: `FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs`
- Modify: `FlairX-Mod-Manager/Services/GameBananaRecentSecondarySorter.cs`

- [ ] **Step 1: Add one failing boundary/continuation tracer test**

```csharp
[Fact]
public void AddPage_KeepsCutoffBoundaryAndStopsAfterOlderRecord()
{
    var cutoff = Now.AddDays(-90).ToUnixTimeSeconds();
    var collector = new GameBananaRecentWindowCollector(
        GameBananaUpdatedTimeRange.Last90Days,
        Now);

    var first = collector.AddPage(
        [Record(1, updated: cutoff + 2), Record(2, updated: cutoff)],
        sourceComplete: false);
    var second = collector.AddPage(
        [Record(3, updated: cutoff - 1)],
        sourceComplete: false);

    Assert.True(first.ShouldContinue);
    Assert.False(second.ShouldContinue);
    Assert.Equal(GameBananaRecentStopReason.CutoffReached, second.StopReason);
    Assert.Equal([1, 2], collector.Records.Select(record => record.Id));
}
```

- [ ] **Step 2: Run the targeted test and verify RED**

Run the Task 1 command. Expected: compilation fails because the collector and stop-decision types do not exist.

- [ ] **Step 3: Implement the incremental collector interface**

Add these types in the existing service file:

```csharp
internal enum GameBananaRecentStopReason
{
    None,
    CutoffReached,
    SourceComplete,
    EmptyPage,
    PageLimit,
    RecordLimit
}

internal readonly record struct GameBananaRecentPageDecision(
    bool ShouldContinue,
    GameBananaRecentStopReason StopReason);

internal sealed class GameBananaRecentWindowCollector
{
    internal const int MaxPages = 10;
    internal const int MaxRawRecords = 500;

    private readonly long? _cutoff;
    private readonly HashSet<int> _seenIds = [];
    private readonly List<GameBananaService.ModRecord> _records = [];

    internal GameBananaRecentWindowCollector(
        GameBananaUpdatedTimeRange range,
        DateTimeOffset nowUtc)
    {
        _cutoff = GameBananaRecentSecondarySorter.GetCutoffUnixSeconds(range, nowUtc);
    }

    internal IReadOnlyList<GameBananaService.ModRecord> Records => _records;
    internal int PagesProcessed { get; private set; }
    internal int RawRecordsProcessed { get; private set; }
    internal GameBananaRecentStopReason StopReason { get; private set; }

    internal GameBananaRecentPageDecision AddPage(
        IReadOnlyList<GameBananaService.ModRecord> page,
        bool sourceComplete)
    {
        if (StopReason != GameBananaRecentStopReason.None)
        {
            return new(false, StopReason);
        }

        PagesProcessed++;
        if (page.Count == 0)
        {
            return Stop(GameBananaRecentStopReason.EmptyPage);
        }

        var remainingCapacity = MaxRawRecords - RawRecordsProcessed;
        var rowsToProcess = Math.Min(page.Count, remainingCapacity);
        var cutoffReached = false;

        for (var index = 0; index < rowsToProcess; index++)
        {
            var record = page[index];
            RawRecordsProcessed++;

            if (_cutoff.HasValue && record.DateUpdated < _cutoff.Value)
            {
                cutoffReached = true;
                continue;
            }

            if (_seenIds.Add(record.Id))
            {
                _records.Add(record);
            }
        }

        if (page.Count > rowsToProcess || RawRecordsProcessed >= MaxRawRecords)
        {
            return Stop(GameBananaRecentStopReason.RecordLimit);
        }

        if (cutoffReached)
        {
            return Stop(GameBananaRecentStopReason.CutoffReached);
        }

        if (sourceComplete)
        {
            return Stop(GameBananaRecentStopReason.SourceComplete);
        }

        if (PagesProcessed >= MaxPages)
        {
            return Stop(GameBananaRecentStopReason.PageLimit);
        }

        return new(true, GameBananaRecentStopReason.None);
    }

    private GameBananaRecentPageDecision Stop(GameBananaRecentStopReason reason)
    {
        StopReason = reason;
        return new(false, reason);
    }
}
```

The method processes at most `MaxRawRecords - RawRecordsProcessed` rows from a response. It does not replace a zero `DateUpdated` with `DateAdded` or `DateModified`.

- [ ] **Step 4: Run the boundary test and verify GREEN**

Run the Task 1 command. Expected: the boundary tracer passes.

- [ ] **Step 5: Add and pass pagination/deduplication tests one at a time**

Add these behaviors as separate facts, running the targeted command after each addition:

```csharp
[Fact]
public void AddPage_DeduplicatesAcrossPagesAndContinuesPastOneHundred()
{
    var collector = new GameBananaRecentWindowCollector(
        GameBananaUpdatedTimeRange.Unlimited,
        Now);

    Assert.True(collector.AddPage(
        Enumerable.Range(1, 100).Select(id => Record(id, updated: id)).ToArray(),
        false).ShouldContinue);
    Assert.True(collector.AddPage(
        Enumerable.Range(100, 51).Select(id => Record(id, updated: id)).ToArray(),
        false).ShouldContinue);

    Assert.Equal(150, collector.Records.Count);
    Assert.Equal(150, collector.Records.Select(record => record.Id).Distinct().Count());
}

[Fact]
public void AddPage_StopsOnEmptyPage()
{
    var collector = new GameBananaRecentWindowCollector(
        GameBananaUpdatedTimeRange.Unlimited,
        Now);

    var result = collector.AddPage([], sourceComplete: false);

    Assert.Equal(GameBananaRecentStopReason.EmptyPage, result.StopReason);
    Assert.False(result.ShouldContinue);
}

[Fact]
public void AddPage_StopsWhenSourceIsComplete()
{
    var collector = new GameBananaRecentWindowCollector(
        GameBananaUpdatedTimeRange.Unlimited,
        Now);

    var result = collector.AddPage([Record(1)], sourceComplete: true);

    Assert.Equal(GameBananaRecentStopReason.SourceComplete, result.StopReason);
}
```

- [ ] **Step 6: Add and pass both hard-limit tests one at a time**

```csharp
[Fact]
public void AddPage_StopsAfterTenPages()
{
    var collector = new GameBananaRecentWindowCollector(
        GameBananaUpdatedTimeRange.Unlimited,
        Now);

    GameBananaRecentPageDecision result = default;
    for (var page = 1; page <= 10; page++)
        result = collector.AddPage([Record(page)], sourceComplete: false);

    Assert.False(result.ShouldContinue);
    Assert.Equal(GameBananaRecentStopReason.PageLimit, result.StopReason);
    Assert.Equal(10, collector.PagesProcessed);
}

[Fact]
public void AddPage_ProcessesAtMostFiveHundredRawRecords()
{
    var collector = new GameBananaRecentWindowCollector(
        GameBananaUpdatedTimeRange.Unlimited,
        Now);

    var result = collector.AddPage(
        Enumerable.Range(1, 600).Select(id => Record(id)).ToArray(),
        sourceComplete: false);

    Assert.False(result.ShouldContinue);
    Assert.Equal(GameBananaRecentStopReason.RecordLimit, result.StopReason);
    Assert.Equal(500, collector.RawRecordsProcessed);
    Assert.Equal(500, collector.Records.Count);
}
```

- [ ] **Step 7: Add the update-field serialization regression**

```csharp
[Fact]
public void ModRecord_MapsGameBananaUpdatedTimestampSeparatelyFromPublishedTimestamp()
{
    var record = JsonSerializer.Deserialize<GameBananaService.ModRecord>(
        """{"_idRow":7,"_tsDateAdded":111,"_tsDateUpdated":222}""")!;

    Assert.Equal(111, record.DateAdded);
    Assert.Equal(222, record.DateUpdated);
}
```

Run the Task 1 command. Expected: all collector and mapping tests pass.

- [ ] **Step 8: Commit the collector**

```powershell
git add -- 'FlairX-Mod-Manager/Services/GameBananaRecentSecondarySorter.cs' 'FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs'
git commit -m 'feat: collect bounded GameBanana update windows'
```

### Task 3: Content filtering and deterministic local ordering

**Files:**
- Modify: `FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs`
- Modify: `FlairX-Mod-Manager/Services/GameBananaRecentSecondarySorter.cs`

- [ ] **Step 1: Add one failing ordering tracer**

```csharp
[Theory]
[InlineData(GameBananaRecentSecondarySort.LatestUpdated, 3)]
[InlineData(GameBananaRecentSecondarySort.MostLiked, 2)]
[InlineData(GameBananaRecentSecondarySort.MostDownloaded, 1)]
[InlineData(GameBananaRecentSecondarySort.MostCommented, 2)]
public void FilterAndOrder_RanksSelectedField(
    GameBananaRecentSecondarySort sort,
    int expectedFirstId)
{
    var records = new[]
    {
        Record(1, downloads: 30, likes: 1, comments: 1, updated: 100),
        Record(2, downloads: 20, likes: 30, comments: 30, updated: 200),
        Record(3, downloads: 10, likes: 20, comments: 20, updated: 300)
    };

    var result = GameBananaRecentSecondarySorter.FilterAndOrder(
        records,
        _ => true,
        sort);

    Assert.Equal(expectedFirstId, result[0].Id);
}
```

- [ ] **Step 2: Run the targeted tests and verify RED**

Run the Task 1 command. Expected: compilation fails because `FilterAndOrder` does not exist.

- [ ] **Step 3: Implement filter-before-order and stable tie-breakers**

```csharp
internal static IReadOnlyList<GameBananaService.ModRecord> FilterAndOrder(
    IEnumerable<GameBananaService.ModRecord> records,
    Func<GameBananaService.ModRecord, bool> include,
    GameBananaRecentSecondarySort sort)
{
    var filtered = records.Where(include);
    return sort switch
    {
        GameBananaRecentSecondarySort.MostLiked =>
            Order(filtered, record => record.GetLikeCount()),
        GameBananaRecentSecondarySort.MostDownloaded =>
            Order(filtered, record => record.GetDownloadCount()),
        GameBananaRecentSecondarySort.MostCommented =>
            Order(filtered, record => record.GetPostCount()),
        _ => filtered
            .OrderByDescending(record => record.DateUpdated)
            .ThenByDescending(record => record.Id)
            .ToList()
    };
}
```

Retain the existing metric tie-breaker of `DateUpdated` descending and ID descending.

- [ ] **Step 4: Add filtering and tie tests, one per RED/GREEN cycle**

```csharp
[Fact]
public void FilterAndOrder_AppliesContentFilterBeforeReturningOrder()
{
    var result = GameBananaRecentSecondarySorter.FilterAndOrder(
        [Record(1, downloads: 100), Record(2, downloads: 10)],
        record => record.Id != 1,
        GameBananaRecentSecondarySort.MostDownloaded);

    Assert.Equal([2], result.Select(record => record.Id));
}

[Fact]
public void FilterAndOrder_UsesUpdatedDateThenIdForMetricTies()
{
    var result = GameBananaRecentSecondarySorter.FilterAndOrder(
        [
            Record(1, downloads: 10, updated: 100),
            Record(2, downloads: 10, updated: 200),
            Record(3, downloads: 10, updated: 200)
        ],
        _ => true,
        GameBananaRecentSecondarySort.MostDownloaded);

    Assert.Equal([3, 2, 1], result.Select(record => record.Id));
}
```

Run the Task 1 command after each test. Expected: every new test passes before the next is added.

- [ ] **Step 5: Commit local ordering**

```powershell
git add -- 'FlairX-Mod-Manager/Services/GameBananaRecentSecondarySorter.cs' 'FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs'
git commit -m 'feat: order filtered GameBanana window candidates'
```

### Task 4: Eligibility, dynamic controls, and localization

**Files:**
- Modify: `FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs`
- Modify: `FlairX-Mod-Manager.Tests/GameBananaBrowserXamlTests.cs`
- Modify: `FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs`
- Modify: `FlairX-Mod-Manager/Language/GameBananaBrowser/en.json`
- Modify: `FlairX-Mod-Manager/Language/GameBananaBrowser/zh-CN.json`
- Modify: `FlairX-Mod-Manager/Language/GameBananaBrowser/zh-TW.json`

- [ ] **Step 1: Add failing mode-normalization tests**

```csharp
[Theory]
[InlineData(false, GameBananaService.CategorySortOrder.LatestUpdated, null)]
[InlineData(true, GameBananaService.CategorySortOrder.MostLiked, null)]
[InlineData(true, GameBananaService.CategorySortOrder.LatestUpdated, "ellen")]
public void Normalize_ResetsIncompatibleModeToDefault(
    bool isCharacterSkins,
    GameBananaService.CategorySortOrder primarySort,
    string? search)
{
    var selection = GameBananaRecentSecondarySorter.Normalize(
        isCharacterSkins,
        primarySort,
        search,
        new(GameBananaUpdatedTimeRange.Last180Days,
            GameBananaRecentSecondarySort.MostLiked));

    Assert.Equal(GameBananaRecentSelection.Default, selection);
    Assert.False(GameBananaRecentSecondarySorter.IsEligible(
        isCharacterSkins, primarySort, search));
}

[Fact]
public void Normalize_PreservesCompatibleRangeAndSort()
{
    var requested = new GameBananaRecentSelection(
        GameBananaUpdatedTimeRange.Last180Days,
        GameBananaRecentSecondarySort.MostCommented);

    Assert.Equal(requested, GameBananaRecentSecondarySorter.Normalize(
        true,
        GameBananaService.CategorySortOrder.LatestUpdated,
        null,
        requested));
}
```

- [ ] **Step 2: Implement `IsEligible` and selection normalization, then verify GREEN**

Use Character Skins + Latest Updated + whitespace-only search as eligible. Return `GameBananaRecentSelection.Default` for all incompatible modes. Run the Task 1 targeted command and expect all tests to pass.

- [ ] **Step 3: Extend static UI tests and verify RED**

Require both controls to stay out of runtime-parsed XAML, require `CreateRecentWindowComboBoxes();`, and parse all three JSON dictionaries to assert these keys:

```text
TimeRange_Header
TimeRange_Last30Days
TimeRange_Last90Days
TimeRange_Last180Days
TimeRange_Unlimited
WindowSort_Header
WindowSort_LatestUpdated
WindowSort_MostLiked
WindowSort_MostDownloaded
WindowSort_MostCommented
RecentWindow_PartialWarning
RecentWindow_CappedWarning
```

Run:

```powershell
& 'C:\Users\Administrator\Documents\Codex\2026-08-18\wo-d\work\dotnet-sdk\dotnet.exe' test 'FlairX-Mod-Manager.Tests\FlairX-Mod-Manager.Tests.csproj' -c Release --no-restore -p:Platform=x64 --filter GameBananaBrowserXamlTests
```

Expected: FAIL because the new control creator and translation keys are absent.

- [ ] **Step 4: Create the two dynamic, separately labelled controls**

Replace `CreateSecondarySortComboBox()` with `CreateRecentWindowComboBoxes()`. Insert two Auto columns before the search column, move SearchBox to column 5 and StarterPackButton to column 6, and add:

```csharp
private ComboBox TimeRangeComboBox = null!;
private ComboBox SecondarySortComboBox = null!;
private GameBananaUpdatedTimeRange _currentTimeRange =
    GameBananaUpdatedTimeRange.Last90Days;
private GameBananaRecentSecondarySort _currentSecondarySort =
    GameBananaRecentSecondarySort.LatestUpdated;
private bool _isUpdatingRecentWindowControls;
```

Set `Header` to the localized Time Range and Sort Within Range labels, populate the four items in enum order, select 90 days (index 1) and Latest Updated (index 0), and attach independent selection handlers under the shared event-suppression guard.

- [ ] **Step 5: Add native text and replace old secondary-sort keys**

Use these exact English values:

```json
"TimeRange_Header": "Time Range",
"TimeRange_Last30Days": "Last 30 Days",
"TimeRange_Last90Days": "Last 90 Days",
"TimeRange_Last180Days": "Last 180 Days",
"TimeRange_Unlimited": "Unlimited (up to 500)",
"WindowSort_Header": "Sort Within Range",
"WindowSort_LatestUpdated": "Latest Updated",
"WindowSort_MostLiked": "Most Liked",
"WindowSort_MostDownloaded": "Most Downloaded",
"WindowSort_MostCommented": "Most Commented",
"RecentWindow_PartialWarning": "A later GameBanana page could not be loaded; the available results are shown.",
"RecentWindow_CappedWarning": "Results reached the safety limit of 10 pages or 500 records."
```

Use these exact Simplified Chinese values:

```json
"TimeRange_Header": "时间范围",
"TimeRange_Last30Days": "最近 30 天",
"TimeRange_Last90Days": "最近 90 天",
"TimeRange_Last180Days": "最近 180 天",
"TimeRange_Unlimited": "不限时间（最多 500 条）",
"WindowSort_Header": "范围内排序",
"WindowSort_LatestUpdated": "最近更新",
"WindowSort_MostLiked": "喜爱最多",
"WindowSort_MostDownloaded": "下载最多",
"WindowSort_MostCommented": "评论最多",
"RecentWindow_PartialWarning": "GameBanana 后续页面加载失败，已显示当前可用结果。",
"RecentWindow_CappedWarning": "结果已达到 10 页或 500 条的安全上限。"
```

Use these exact Traditional Chinese values:

```json
"TimeRange_Header": "時間範圍",
"TimeRange_Last30Days": "最近 30 天",
"TimeRange_Last90Days": "最近 90 天",
"TimeRange_Last180Days": "最近 180 天",
"TimeRange_Unlimited": "不限時間（最多 500 筆）",
"WindowSort_Header": "範圍內排序",
"WindowSort_LatestUpdated": "最近更新",
"WindowSort_MostLiked": "最多喜愛",
"WindowSort_MostDownloaded": "最多下載",
"WindowSort_MostCommented": "最多評論",
"RecentWindow_PartialWarning": "GameBanana 後續頁面載入失敗，已顯示目前可用結果。",
"RecentWindow_CappedWarning": "結果已達到 10 頁或 500 筆的安全上限。"
```

Remove the now-unused `SecondarySort_*` keys from these three dictionaries.

- [ ] **Step 6: Implement availability and handlers, then verify GREEN**

Replace `UpdateSecondarySortAvailability` with `UpdateRecentWindowAvailability`. It calls `Normalize`, resets both state fields and control indices only when ineligible, and sets both controls to Visible only when `IsEligible` is true. Call it from category, primary sort, search, initial-search, and navigation paths.

Run the Task 1 and Task 4 targeted commands. Expected: all policy, static UI, and translation tests pass.

- [ ] **Step 7: Commit UI state and text**

```powershell
git add -- 'FlairX-Mod-Manager/Services/GameBananaRecentSecondarySorter.cs' 'FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs' 'FlairX-Mod-Manager.Tests/GameBananaRecentSecondarySorterTests.cs' 'FlairX-Mod-Manager.Tests/GameBananaBrowserXamlTests.cs' 'FlairX-Mod-Manager/Language/GameBananaBrowser/en.json' 'FlairX-Mod-Manager/Language/GameBananaBrowser/zh-CN.json' 'FlairX-Mod-Manager/Language/GameBananaBrowser/zh-TW.json'
git commit -m 'feat: add GameBanana time range controls'
```

### Task 5: Iterative loading, notices, and navigation restoration

**Files:**
- Modify: `FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs`
- Modify: `FlairX-Mod-Manager.Tests/GameBananaBrowserXamlTests.cs`

- [ ] **Step 1: Extend navigation static tests and verify RED**

Require `NavigationEntry` to contain `GameBananaUpdatedTimeRange TimeRange`, every mod-list capture to include `TimeRange: _currentTimeRange`, restoration to read `entry.TimeRange`, and the code-behind to contain a bounded loop whose continuation is driven by `decision.ShouldContinue`.

Run the Task 4 XAML test command. Expected: FAIL because `TimeRange` and iterative collection are absent.

- [ ] **Step 2: Replace the fixed page-two branch with an incremental collector**

At the start of `LoadModsAsync`, normalize the current selection and compute:

```csharp
var windowEligible = GameBananaRecentSecondarySorter.IsEligible(
    _currentCategoryFilter == CategoryFilter.CharacterSkins,
    _currentSortOrder,
    _currentSearch);
var selection = GameBananaRecentSecondarySorter.Normalize(
    _currentCategoryFilter == CategoryFilter.CharacterSkins,
    _currentSortOrder,
    _currentSearch,
    new(_currentTimeRange, _currentSecondarySort));
var firstPage = windowEligible ? 1 : _currentPage;
```

Keep the existing first-page Cloudflare retry. For an eligible window, create `GameBananaRecentWindowCollector(selection.TimeRange, DateTimeOffset.UtcNow)`, add the first response, and request page 2 onward only while `decision.ShouldContinue`. After every request await, return immediately when `loadGeneration != _loadGeneration`. A null later response sets `partialWindow = true` and ends the loop.

Pass `response.Metadata?.IsComplete ?? records.Count < 50` to `AddPage`. Do not request page 11 and do not process more than 500 raw rows.

- [ ] **Step 3: Apply content filters before local ordering and card mapping**

For eligible windows, call:

```csharp
records = GameBananaRecentSecondarySorter.FilterAndOrder(
    collector.Records,
    ShouldIncludeMod,
    selection.Sort);
```

For non-window modes, retain the existing server-order/page behavior and existing NSFW/blacklist checks. In the eligible path set `_hasMorePages = false`, hide Load More, and skip automatic load-more checks.

- [ ] **Step 4: Display exact terminal-state notices**

If page 1 fails, retain the existing error InfoBar and no stale results. If a later page fails, show `RecentWindow_PartialWarning` with Warning severity while displaying accumulated results. If `StopReason` is PageLimit or RecordLimit, show `RecentWindow_CappedWarning`. A complete/empty/cutoff stop closes old notices and displays the full collected result normally.

- [ ] **Step 5: Preserve and restore both state fields exactly once**

Add the final defaulted navigation member:

```csharp
GameBananaUpdatedTimeRange TimeRange = GameBananaUpdatedTimeRange.Last90Days,
GameBananaRecentSecondarySort SecondarySort =
    GameBananaRecentSecondarySort.LatestUpdated
```

Add `TimeRange: _currentTimeRange` beside every existing `SecondarySort` capture. During ModsList restoration, normalize `new(entry.TimeRange, entry.SecondarySort)`, update both selected indices under `_isUpdatingRecentWindowControls`, call `UpdateRecentWindowAvailability(resetIfIneligible: false)`, load character options if needed, and call `LoadModsAsync()` once.

- [ ] **Step 6: Run targeted and full tests**

Run:

```powershell
& 'C:\Users\Administrator\Documents\Codex\2026-08-18\wo-d\work\dotnet-sdk\dotnet.exe' test 'FlairX-Mod-Manager.Tests\FlairX-Mod-Manager.Tests.csproj' -c Release --no-restore -p:Platform=x64 --filter 'GameBananaRecentSecondarySorterTests|GameBananaBrowserXamlTests'
& 'C:\Users\Administrator\Documents\Codex\2026-08-18\wo-d\work\dotnet-sdk\dotnet.exe' test 'FlairX-Mod-Manager.Tests\FlairX-Mod-Manager.Tests.csproj' -c Release --no-restore -p:Platform=x64
```

Expected: zero failures in targeted and full runs.

- [ ] **Step 7: Commit orchestration and navigation**

```powershell
git add -- 'FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs' 'FlairX-Mod-Manager.Tests/GameBananaBrowserXamlTests.cs'
git commit -m 'feat: page through GameBanana update windows'
```

### Task 6: Full verification, same-build deployment, and final commit

**Files:**
- Modify: `openspec/changes/gamebanana-updated-time-window/tasks.md`
- Generate: `FlairX-Mod-Manager/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/FlairX Mod Manager.dll`
- Generate: `FlairX-Mod-Manager/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/FlairX Mod Manager.pdb`
- Generate: `FlairX-Mod-Manager/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/FlairX Mod Manager.pri`
- Back up and replace: `E:\flairx\app\FlairX Mod Manager.dll`, `.pdb`, `.pri`

- [ ] **Step 1: Run repository and governance checks**

```powershell
openspec validate gamebanana-updated-time-window --strict
git diff --check
git status --short
```

Expected: OpenSpec valid, no whitespace errors, only intentional source/OpenSpec changes plus the protected untracked `artifacts/` directory.

- [ ] **Step 2: Run a fresh full x64 Release test suite**

```powershell
& 'C:\Users\Administrator\Documents\Codex\2026-08-18\wo-d\work\dotnet-sdk\dotnet.exe' test 'FlairX-Mod-Manager.Tests\FlairX-Mod-Manager.Tests.csproj' -c Release --no-restore -p:Platform=x64
```

Expected: all discovered tests pass with zero failures.

- [ ] **Step 3: Produce a fresh x64 Release build and verify PRI generation**

```powershell
& 'C:\Users\Administrator\Documents\Codex\2026-08-18\wo-d\work\dotnet-sdk\dotnet.exe' build 'FlairX-Mod-Manager\FlairX-Mod-Manager.csproj' -c Release --no-restore -p:Platform=x64
```

Resolve the output directory and require all three files to exist. Record their length, UTC last-write time, and SHA-256 hash before deployment.

- [ ] **Step 4: Stop only FlairX processes and create a recoverable backup**

Resolve `E:\flairx\app` and `E:\flairx\backups` to absolute paths and verify both remain under `E:\flairx`. Stop only processes whose executable path resolves under `E:\flairx`. Create the backup path with `$backupPath = Join-Path 'E:\flairx\backups' ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-gamebanana-updated-time-window')` and copy the currently installed DLL/PDB/PRI into it.

- [ ] **Step 5: Deploy the same-build artifact set and verify hashes**

Copy the three recorded source files into `E:\flairx\app`, then compare each target SHA-256 with its recorded source hash. Do not restart when any file is missing or any hash differs; restore the backup set instead.

- [ ] **Step 6: Restart and perform focused acceptance**

Start `E:\flairx\FlairX Mod Manager Launcher.exe` hidden only if the launcher does not need user interaction. Verify the process remains running and inspect current FlairX logs for new XAML/load exceptions. Exercise or statically confirm:

1. Character Skins + Latest Updated + empty search exposes separate Time Range and Sort Within Range controls.
2. Default is Last 90 Days + Latest Updated.
3. 30/90/180/Unlimited selections reload safely and window modes hide Load More.
4. Likes/Downloads/Comments reorder the whole retained pool.
5. Search/category/primary-sort changes hide/reset window controls.
6. Details → Back restores range/sort state.

- [ ] **Step 7: Complete OpenSpec, validate, and commit**

Mark every finished checkbox in `openspec/changes/gamebanana-updated-time-window/tasks.md`, run strict validation, archive the change so its capability becomes the durable current spec, and validate the archived/main spec state. Then run `git diff --check`, `git status --short`, and create the final implementation/verification commit without adding `artifacts/`.

Record the final commit hash, test count, build result, PRI path, backup path, deployment hashes, and restart evidence for the user report.
