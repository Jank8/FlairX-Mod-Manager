using System;
using System.Collections.Generic;
using System.Linq;

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
            {
                yield return category;
                continue;
            }

            foreach (var child in GetLeaves(category.Children))
                yield return child;
        }
    }
}
