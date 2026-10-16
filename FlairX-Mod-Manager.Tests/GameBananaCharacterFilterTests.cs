using FlairX_Mod_Manager.Services;
using Xunit;

namespace FlairX_Mod_Manager.Tests;

public class GameBananaCharacterFilterTests
{
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
}
