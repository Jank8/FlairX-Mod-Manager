using System;
using System.Collections.Generic;
using System.Linq;

namespace FlairX_Mod_Manager.Services;

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

internal static class GameBananaRecentSecondarySorter
{
    internal static bool IsEligible(
        bool isCharacterSkins,
        GameBananaService.CategorySortOrder primarySort,
        string? search) =>
        isCharacterSkins &&
        primarySort == GameBananaService.CategorySortOrder.LatestUpdated &&
        string.IsNullOrWhiteSpace(search);

    internal static GameBananaRecentSelection Normalize(
        bool isCharacterSkins,
        GameBananaService.CategorySortOrder primarySort,
        string? search,
        GameBananaRecentSelection requested) =>
        IsEligible(isCharacterSkins, primarySort, search)
            ? requested
            : GameBananaRecentSelection.Default;

    internal static long? GetCutoffUnixSeconds(
        GameBananaUpdatedTimeRange range,
        DateTimeOffset nowUtc) => range switch
    {
        GameBananaUpdatedTimeRange.Last30Days => nowUtc.AddDays(-30).ToUnixTimeSeconds(),
        GameBananaUpdatedTimeRange.Last90Days => nowUtc.AddDays(-90).ToUnixTimeSeconds(),
        GameBananaUpdatedTimeRange.Last180Days => nowUtc.AddDays(-180).ToUnixTimeSeconds(),
        _ => null
    };

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
