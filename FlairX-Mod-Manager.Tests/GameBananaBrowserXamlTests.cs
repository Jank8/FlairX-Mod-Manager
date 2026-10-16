using System.Xml.Linq;
using System.Text.Json;
using Xunit;

namespace FlairX_Mod_Manager.Tests;

public class GameBananaBrowserXamlTests
{
    [Fact]
    public void CharacterFilter_IsCreatedAfterXamlInitialization()
    {
        var xamlPath = FindRepositoryFile(
            "FlairX-Mod-Manager",
            "Pages",
            "GameBananaBrowserUserControl.xaml");
        var document = XDocument.Load(xamlPath);
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        Assert.DoesNotContain(
            document.Descendants(),
            element => (string?)element.Attribute(xaml + "Name") == "CharacterFilterComboBox");
        Assert.DoesNotContain(
            document.Descendants(),
            element => (string?)element.Attribute(xaml + "Name") == "SecondarySortComboBox");
        Assert.DoesNotContain(
            document.Descendants(),
            element => (string?)element.Attribute(xaml + "Name") == "TimeRangeComboBox");
        Assert.Contains(
            document.Descendants(),
            element => (string?)element.Attribute(xaml + "Name") == "FiltersToolbarGrid");

        var codeBehindPath = FindRepositoryFile(
            "FlairX-Mod-Manager",
            "Pages",
            "GameBananaBrowserUserControl.xaml.cs");
        var codeBehind = File.ReadAllText(codeBehindPath);

        Assert.Contains("CreateCharacterFilterComboBox();", codeBehind);
        Assert.Contains("private void CreateCharacterFilterComboBox()", codeBehind);
        Assert.Contains("CreateRecentWindowComboBoxes();", codeBehind);
        Assert.Contains("private void CreateRecentWindowComboBoxes()", codeBehind);
        Assert.DoesNotContain("CategoryFilterComboBox.Parent", codeBehind);
    }

    [Fact]
    public void RecentWindowFilters_AlignAllComboBoxDropDownsAtBottom()
    {
        var xamlPath = FindRepositoryFile(
            "FlairX-Mod-Manager",
            "Pages",
            "GameBananaBrowserUserControl.xaml");
        var document = XDocument.Load(xamlPath);
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        foreach (var name in new[] { "CategoryFilterComboBox", "SortOrderComboBox" })
        {
            var comboBox = Assert.Single(
                document.Descendants(),
                element => (string?)element.Attribute(xaml + "Name") == name);
            Assert.Equal("Bottom", (string?)comboBox.Attribute("VerticalAlignment"));
        }

        var codeBehindPath = FindRepositoryFile(
            "FlairX-Mod-Manager",
            "Pages",
            "GameBananaBrowserUserControl.xaml.cs");
        var codeBehind = File.ReadAllText(codeBehindPath);
        var characterMethod = SliceBetween(
            codeBehind,
            "private void CreateCharacterFilterComboBox()",
            "private void CreateRecentWindowComboBoxes()");
        var recentWindowMethod = SliceBetween(
            codeBehind,
            "private void CreateRecentWindowComboBoxes()",
            "private void GameBananaBrowserUserControl_Loaded");

        Assert.Contains("VerticalAlignment = VerticalAlignment.Bottom", characterMethod);
        Assert.Equal(
            2,
            recentWindowMethod.Split(
                "VerticalAlignment = VerticalAlignment.Bottom",
                StringSplitOptions.None).Length - 1);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("zh-CN")]
    [InlineData("zh-TW")]
    public void RecentWindowTranslations_AreComplete(string language)
    {
        var path = FindRepositoryFile(
            "FlairX-Mod-Manager",
            "Language",
            "GameBananaBrowser",
            $"{language}.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var keys = new[]
        {
            "TimeRange_Header",
            "TimeRange_Last30Days",
            "TimeRange_Last90Days",
            "TimeRange_Last180Days",
            "TimeRange_Unlimited",
            "WindowSort_Header",
            "WindowSort_LatestUpdated",
            "WindowSort_MostLiked",
            "WindowSort_MostDownloaded",
            "WindowSort_MostCommented",
            "RecentWindow_PartialTitle",
            "RecentWindow_PartialWarning",
            "RecentWindow_CappedTitle",
            "RecentWindow_CappedWarning"
        };

        foreach (var key in keys)
        {
            Assert.True(root.TryGetProperty(key, out var value), $"Missing {key} in {language}.json");
            Assert.False(string.IsNullOrWhiteSpace(value.GetString()));
        }
    }

    [Fact]
    public void NavigationState_PreservesEligibleSecondarySort()
    {
        var codeBehindPath = FindRepositoryFile(
            "FlairX-Mod-Manager",
            "Pages",
            "GameBananaBrowserUserControl.xaml.cs");
        var codeBehind = File.ReadAllText(codeBehindPath);

        Assert.Contains("GameBananaRecentSecondarySort SecondarySort", codeBehind);
        Assert.Contains("SecondarySort: _currentSecondarySort", codeBehind);
        Assert.Contains("entry.SecondarySort", codeBehind);
        Assert.Contains("GameBananaUpdatedTimeRange TimeRange", codeBehind);
        Assert.Contains("TimeRange: _currentTimeRange", codeBehind);
        Assert.Contains("entry.TimeRange", codeBehind);
        Assert.Contains("while (decision.ShouldContinue)", codeBehind);
    }

    [Fact]
    public void RecentWindowNotice_IsAppliedBeforeEmptyResultReturn()
    {
        var codeBehindPath = FindRepositoryFile(
            "FlairX-Mod-Manager",
            "Pages",
            "GameBananaBrowserUserControl.xaml.cs");
        var codeBehind = File.ReadAllText(codeBehindPath);

        var noticeIndex = codeBehind.IndexOf("if (partialWindow)", StringComparison.Ordinal);
        var emptyResultIndex = codeBehind.IndexOf("if (records.Count == 0)", StringComparison.Ordinal);

        Assert.True(noticeIndex >= 0, "The recent-window notice handling is missing.");
        Assert.True(emptyResultIndex >= 0, "The empty-result handling is missing.");
        Assert.True(
            noticeIndex < emptyResultIndex,
            "Partial or capped window notices must be shown before an empty-result early return.");
    }

    [Fact]
    public void RecentWindowNotices_UseDedicatedLocalizedTitles()
    {
        var codeBehindPath = FindRepositoryFile(
            "FlairX-Mod-Manager",
            "Pages",
            "GameBananaBrowserUserControl.xaml.cs");
        var noticeHandling = SliceBetween(
            File.ReadAllText(codeBehindPath),
            "if (partialWindow)",
            "if (records.Count == 0)");

        Assert.Contains("\"RecentWindow_PartialTitle\"", noticeHandling);
        Assert.Contains("\"Partial results\"", noticeHandling);
        Assert.Contains("\"RecentWindow_CappedTitle\"", noticeHandling);
        Assert.Contains("\"Results limited\"", noticeHandling);
        Assert.DoesNotContain("GetTranslationOrDefault(\"Warning\"", noticeHandling);
    }

    [Fact]
    public void SuccessfulInstall_ReturnsThroughTheSameNavigationHistoryAsBackButton()
    {
        var codeBehindPath = FindRepositoryFile(
            "FlairX-Mod-Manager",
            "Pages",
            "GameBananaBrowserUserControl.xaml.cs");
        var codeBehind = File.ReadAllText(codeBehindPath);
        var backButtonHandler = SliceBetween(
            codeBehind,
            "private void BackButton_Click",
            "private async Task NavigateBackAsync");
        var downloadHandler = SliceBetween(
            codeBehind,
            "private async void DetailDownloadButton_Click",
            "private void OnModInstalled");

        Assert.Contains("_ = NavigateBackAsync();", backButtonHandler);
        Assert.Contains("installationCompleted = true", downloadHandler);
        Assert.Contains("await NavigateBackAsync();", downloadHandler);
        Assert.DoesNotContain("CloseDetailsPanel();", downloadHandler);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. segments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(segments)}.");
    }

    private static string SliceBetween(string text, string startMarker, string endMarker)
    {
        var start = text.IndexOf(startMarker, StringComparison.Ordinal);
        var end = text.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);

        Assert.True(start >= 0, $"Missing marker: {startMarker}");
        Assert.True(end > start, $"Missing marker after {startMarker}: {endMarker}");
        return text[start..end];
    }
}
