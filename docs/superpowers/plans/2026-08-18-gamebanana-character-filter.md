# GameBanana Character Filter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a GameBanana character selector that appears for Character Skins, filters initial and paginated results by the chosen category, composes with Most Liked/Most Downloaded sorting, and restores through detail navigation.

**Architecture:** Introduce one small, UI-independent selector model/helper that converts GameBanana's nested category records into stable character options and resolves invalid selections to the game's parent character category. The WinUI browser owns asynchronous option loading and selection state, while all category queries consume a single resolved category ID. Navigation records persist that ID so back navigation restores the same role and sort.

**Tech Stack:** C# 14 / .NET 10, WinUI 3 XAML, xUnit, existing `GameBananaService` category and mod-list APIs.

---

## File structure

- Create `FlairX-Mod-Manager/Services/GameBananaCharacterFilter.cs`: pure option construction and category-ID fallback logic, independent of WinUI.
- Create `FlairX-Mod-Manager.Tests/GameBananaCharacterFilterTests.cs`: public-behavior tests for nested leaves, deduplication, parent/default selection, and invalid saved selections.
- Modify `FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml`: insert the conditional character ComboBox between the filter and sort controls.
- Modify `FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs`: load options, reload on selection, route initial/retry/pagination queries through the selected ID, and persist/restore navigation state.
- Modify `FlairX-Mod-Manager/Language/GameBananaBrowser/en.json`, `zh-CN.json`, and `zh-TW.json`: add the `Filter_AllCharacters` label.

### Task 1: Character option domain helper

**Files:**
- Create: `FlairX-Mod-Manager/Services/GameBananaCharacterFilter.cs`
- Create: `FlairX-Mod-Manager.Tests/GameBananaCharacterFilterTests.cs`

- [ ] **Step 1: Write a failing test for nested character leaves**

```csharp
[Fact]
public void BuildOptions_PutsAllCharactersFirstAndUsesNestedLeaves()
{
    var parent = new GameBananaService.CategoryRecord
    {
        Id = 30305,
        Name = "Characters",
        Children =
        [
            new() { Id = 47714, Name = "Remielle Dan" },
            new()
            {
                Id = 48000,
                Name = "Group",
                Children = [new() { Id = 48001, Name = "Jane Doe" }]
            }
        ]
    };

    var options = GameBananaCharacterFilter.BuildOptions([parent], 30305, "All Characters");

    Assert.Equal([30305, 48001, 47714], options.Select(option => option.CategoryId));
    Assert.Equal("All Characters", options[0].DisplayName);
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
& 'C:\Users\Administrator\Documents\Codex\2026-08-18\wo-d\work\dotnet-sdk\dotnet.exe' test 'FlairX-Mod-Manager.Tests\FlairX-Mod-Manager.Tests.csproj' -c Release -p:Platform=x64 --no-restore --filter FullyQualifiedName~GameBananaCharacterFilterTests
```

Expected: FAIL because `GameBananaCharacterFilter` does not exist.

- [ ] **Step 3: Implement the minimal pure helper**

```csharp
namespace FlairX_Mod_Manager.Services;

internal sealed record GameBananaCharacterOption(int CategoryId, string DisplayName)
{
    public override string ToString() => DisplayName;
}

internal static class GameBananaCharacterFilter
{
    internal static IReadOnlyList<GameBananaCharacterOption> BuildOptions(
        IEnumerable<GameBananaService.CategoryRecord> categories,
        int parentCategoryId,
        string allCharactersLabel)
    {
        var categoryList = categories.ToList();
        var parent = categoryList.FirstOrDefault(category => category.Id == parentCategoryId);
        var candidates = parent?.Children.Count > 0
            ? GetLeaves(parent.Children)
            : categoryList.Where(category => category.Id != parentCategoryId);

        return new[] { new GameBananaCharacterOption(parentCategoryId, allCharactersLabel) }
            .Concat(candidates
                .Where(category => category.Id > 0 && !string.IsNullOrWhiteSpace(category.Name))
                .GroupBy(category => category.Id)
                .Select(group => group.First())
                .OrderBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(category => new GameBananaCharacterOption(category.Id, category.Name)))
            .ToList();
    }

    internal static int ResolveCategoryId(
        int parentCategoryId,
        int? selectedCategoryId,
        IEnumerable<GameBananaCharacterOption> options) =>
        selectedCategoryId.HasValue && options.Any(option => option.CategoryId == selectedCategoryId.Value)
            ? selectedCategoryId.Value
            : parentCategoryId;

    private static IEnumerable<GameBananaService.CategoryRecord> GetLeaves(
        IEnumerable<GameBananaService.CategoryRecord> categories)
    {
        foreach (var category in categories)
        {
            if (category.Children.Count == 0)
                yield return category;
            else
                foreach (var child in GetLeaves(category.Children))
                    yield return child;
        }
    }
}
```

- [ ] **Step 4: Run the focused test and verify GREEN**

Run the Step 2 command. Expected: PASS.

- [ ] **Step 5: Add one-at-a-time tests for deduplication and selection fallback**

Add and run each test separately before adding the next:

```csharp
[Fact]
public void BuildOptions_DeduplicatesFlatFallbackCategories()
{
    var categories = new[]
    {
        new GameBananaService.CategoryRecord { Id = 47714, Name = "Remielle Dan" },
        new GameBananaService.CategoryRecord { Id = 47714, Name = "Duplicate" }
    };

    var options = GameBananaCharacterFilter.BuildOptions(categories, 30305, "All Characters");

    Assert.Equal([30305, 47714], options.Select(option => option.CategoryId));
}

[Theory]
[InlineData(null, 30305)]
[InlineData(99999, 30305)]
[InlineData(47714, 47714)]
public void ResolveCategoryId_UsesSelectionOnlyWhenItExists(int? selected, int expected)
{
    var options = new[]
    {
        new GameBananaCharacterOption(30305, "All Characters"),
        new GameBananaCharacterOption(47714, "Remielle Dan")
    };

    Assert.Equal(expected, GameBananaCharacterFilter.ResolveCategoryId(30305, selected, options));
}
```

Expected after every RED→GREEN cycle: focused suite PASS.

- [ ] **Step 6: Commit the helper and tests**

```powershell
git add FlairX-Mod-Manager/Services/GameBananaCharacterFilter.cs FlairX-Mod-Manager.Tests/GameBananaCharacterFilterTests.cs
git commit -m "feat: model GameBanana character selections"
```

### Task 2: Conditional character selector UI

**Files:**
- Modify: `FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml`
- Modify: `FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs`
- Modify: `FlairX-Mod-Manager/Language/GameBananaBrowser/en.json`
- Modify: `FlairX-Mod-Manager/Language/GameBananaBrowser/zh-CN.json`
- Modify: `FlairX-Mod-Manager/Language/GameBananaBrowser/zh-TW.json`

- [ ] **Step 1: Add a character ComboBox between category and sort**

Change the toolbar to five columns and add:

```xml
<ComboBox x:Name="CharacterFilterComboBox"
          Grid.Column="1"
          MinWidth="180"
          MaxWidth="260"
          Margin="0,0,12,0"
          VerticalAlignment="Center"
          Visibility="Collapsed"
          DisplayMemberPath="DisplayName"
          SelectionChanged="CharacterFilterComboBox_SelectionChanged"/>
```

Move sort/search/starter-pack to columns 2/3/4.

- [ ] **Step 2: Add localized labels with explicit fallback**

Add these JSON entries:

```json
// en.json
"Filter_AllCharacters": "All Characters"

// zh-CN.json
"Filter_AllCharacters": "全部角色"

// zh-TW.json
"Filter_AllCharacters": "全部角色"
```

Add a browser helper that rejects the repository's `[MISSING:key]` sentinel:

```csharp
private string GetTranslationOrDefault(string key, string fallback)
{
    var value = SharedUtilities.GetTranslation(_lang, key);
    return string.IsNullOrWhiteSpace(value) || value.StartsWith("[MISSING:", StringComparison.Ordinal)
        ? fallback
        : value;
}
```

- [ ] **Step 3: Add selector state and asynchronous option loading**

Add fields:

```csharp
private IReadOnlyList<GameBananaCharacterOption> _characterOptions = [];
private int? _selectedCharacterCategoryId;
private bool _isUpdatingCharacterSelection;
```

Implement `LoadCharacterOptionsAsync(int? preferredCategoryId = null)` so it immediately displays a disabled parent/all option, calls `GetCharacterCategoriesAsync(_gameTag)`, rebuilds the options with `GameBananaCharacterFilter.BuildOptions`, selects `preferredCategoryId` only if valid, and enables the ComboBox only when a concrete character option exists. Catch and log failures while retaining the parent option.

- [ ] **Step 4: Show and load the selector only for Character Skins**

In `CategoryFilterComboBox_SelectionChanged`, set both `CharacterFilterComboBox.Visibility` and `SortOrderComboBox.Visibility`. On Character Skins, reset state to the parent ID, await `LoadCharacterOptionsAsync()`, and reload page 1. On All Mods, hide the selector and leave the existing all-mod query unchanged.

- [ ] **Step 5: Reload when a concrete character changes**

Implement:

```csharp
private void CharacterFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    if (_isUpdatingCharacterSelection ||
        _currentCategoryFilter != CategoryFilter.CharacterSkins ||
        sender is not ComboBox { SelectedItem: GameBananaCharacterOption option })
        return;

    _selectedCharacterCategoryId = option.CategoryId;
    _currentPage = 1;
    _ = LoadModsAsync();
}
```

- [ ] **Step 6: Build to catch XAML/code-behind errors**

Run:

```powershell
& 'C:\Users\Administrator\Documents\Codex\2026-08-18\wo-d\work\dotnet-sdk\dotnet.exe' build 'FlairX-Mod-Manager\FlairX-Mod-Manager.csproj' -c Release -p:Platform=x64 --no-restore --no-incremental
```

Expected: exit code 0.

- [ ] **Step 7: Commit the visible selector**

```powershell
git add FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs FlairX-Mod-Manager/Language/GameBananaBrowser/en.json FlairX-Mod-Manager/Language/GameBananaBrowser/zh-CN.json FlairX-Mod-Manager/Language/GameBananaBrowser/zh-TW.json
git commit -m "feat: add GameBanana character selector"
```

### Task 3: Route queries and navigation through the selected character

**Files:**
- Modify: `FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs`
- Test: `FlairX-Mod-Manager.Tests/GameBananaCharacterFilterTests.cs`

- [ ] **Step 1: Add a failing query-resolution test for parent and concrete selections**

Use the existing `ResolveCategoryId_UsesSelectionOnlyWhenItExists` theory as the public contract, run it alone, and temporarily make the concrete-selection inline data expect `30305` to confirm the assertion detects routing changes; restore `47714` and verify it fails until the call sites are updated.

- [ ] **Step 2: Centralize the active category ID**

Add:

```csharp
private int GetActiveCharacterCategoryId()
{
    var parentId = GameBananaService.GetCharacterCategoryId(_gameTag);
    return GameBananaCharacterFilter.ResolveCategoryId(
        parentId,
        _selectedCharacterCategoryId,
        _characterOptions);
}
```

Replace every `GetCharacterCategoryId(_gameTag)` used by `LoadModsAsync` initial request, its Cloudflare retry, and `LoadMoreModsAsync` with `GetActiveCharacterCategoryId()`. Keep `_currentSortOrder` unchanged so Most Liked and Most Downloaded compose with the selected category.

- [ ] **Step 3: Persist the selected category in navigation entries**

Extend `NavigationEntry` with:

```csharp
int? CharacterCategoryId = null
```

Add `CharacterCategoryId: _selectedCharacterCategoryId` to every `NavigationState.ModsList` push. Include it in the reload comparison in `RestoreNavigationEntryAsync`.

- [ ] **Step 4: Restore character options and selection before reloading**

During ModsList restoration, show/hide the character selector with the filter. If restoring Character Skins, call `await LoadCharacterOptionsAsync(entry.CharacterCategoryId)` before `LoadModsAsync`; otherwise clear the selected character ID. Suppress ComboBox events while setting restored selections so only one request runs.

- [ ] **Step 5: Run the full test suite and production build**

Run:

```powershell
& 'C:\Users\Administrator\Documents\Codex\2026-08-18\wo-d\work\dotnet-sdk\dotnet.exe' test 'FlairX-Mod-Manager.Tests\FlairX-Mod-Manager.Tests.csproj' -c Release -p:Platform=x64 --no-restore
& 'C:\Users\Administrator\Documents\Codex\2026-08-18\wo-d\work\dotnet-sdk\dotnet.exe' build 'FlairX-Mod-Manager\FlairX-Mod-Manager.csproj' -c Release -p:Platform=x64 --no-restore --no-incremental
```

Expected: all tests PASS; build exits 0 with no errors.

- [ ] **Step 6: Commit query and navigation behavior**

```powershell
git add FlairX-Mod-Manager/Pages/GameBananaBrowserUserControl.xaml.cs FlairX-Mod-Manager.Tests/GameBananaCharacterFilterTests.cs
git commit -m "feat: filter GameBanana results by character"
```

### Task 4: Package, deploy, and smoke-test

**Files:**
- Deploy from: `FlairX-Mod-Manager/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/`
- Deploy to: `E:\flairx`

- [ ] **Step 1: Stop the installed FlairX process only if it is running**

Resolve the executable path and stop only processes whose path is under `E:\flairx`; do not terminate unrelated processes.

- [ ] **Step 2: Back up the installed files being replaced**

Create a timestamped directory under `E:\flairx\backups` and copy the current executable, DLLs, PRI/resources, and the three modified language JSON files into it before replacement.

- [ ] **Step 3: Deploy the fresh Release output**

Copy the complete Release output into `E:\flairx`, excluding build-only metadata and preserving the `backups` directory.

- [ ] **Step 4: Launch and manually verify the acceptance path**

Verify in Zenless Zone Zero:

1. All Mods shows no character selector.
2. Character Skins shows `全部角色` plus `Remielle Dan`.
3. Selecting Remielle requests category `47714` and changes the card set.
4. Most Liked and Most Downloaded both retain the Remielle filter.
5. Loading more stays on Remielle.
6. Opening a mod and going back restores Remielle and the chosen sort.

- [ ] **Step 5: Re-run verification after deployment and commit any final corrections**

Run the full test and build commands from Task 3 again. Inspect `git status --short` and `git diff --check`; expected: tests pass, build exits 0, no whitespace errors, and only intentional changes remain.

