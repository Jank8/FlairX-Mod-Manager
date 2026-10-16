using System.Text.Json;
using FlairX_Mod_Manager.Services;
using Xunit;

namespace FlairX_Mod_Manager.Tests;

public class GameBananaCategoryTreeTests
{
    private const string SingleCategoryJson = """
        [
          {
            "_idRow": 47714,
            "_sName": "Remielle Dan",
            "_sProfileUrl": "https://gamebanana.com/mods/cats/47714",
            "_sIconUrl": "https://images.gamebanana.com/remielle.png",
            "_aChildren": []
          }
        ]
        """;

    [Fact]
    public void ParseCategoryTreeFromJson_FlattensNestedCategoriesAndPreservesFields()
    {
        const string json = """
            [
              {
                "_idRow": 30305,
                "_sName": "Characters",
                "_sProfileUrl": "https://gamebanana.com/mods/cats/30305",
                "_sIconUrl": "https://images.gamebanana.com/characters.png",
                "_aChildren": [
                  {
                    "_idRow": 47714,
                    "_sName": "Remielle Dan",
                    "_sProfileUrl": "https://gamebanana.com/mods/cats/47714",
                    "_sIconUrl": "https://images.gamebanana.com/remielle.png",
                    "_aChildren": []
                  },
                  {
                    "_idRow": 47714,
                    "_sName": "Duplicate Remielle",
                    "_aChildren": []
                  }
                ]
              },
              {
                "_idRow": 50000,
                "_sName": "Weapons",
                "_aChildren": []
              }
            ]
            """;

        var categories = GameBananaService.ParseCategoryTreeFromJson(json);

        Assert.Equal([30305, 47714, 50000], categories.Select(category => category.Id));
        var remielle = Assert.Single(categories, category => category.Id == 47714);
        Assert.Equal("Remielle Dan", remielle.Name);
        Assert.Equal("https://gamebanana.com/mods/cats/47714", remielle.ProfileUrl);
        Assert.Equal("https://images.gamebanana.com/remielle.png", remielle.GetIconUrl());
    }

    [Fact]
    public void ParseCategoryTreeFromJson_EmptyArrayReturnsEmptyList()
    {
        var categories = GameBananaService.ParseCategoryTreeFromJson("[]");

        Assert.Empty(categories);
    }

    [Fact]
    public void ParseCategoryTreeFromJson_MalformedJsonThrowsJsonException()
    {
        Assert.Throws<JsonException>(() => GameBananaService.ParseCategoryTreeFromJson("not json"));
    }

    [Fact]
    public async Task FetchCategoryTreeWithFallbackAsync_JsonSuccessDoesNotUseFallback()
    {
        var fallbackCalls = 0;
        string? requestedUrl = null;

        var categories = await GameBananaService.FetchCategoryTreeWithFallbackAsync(
            19567,
            url =>
            {
                requestedUrl = url;
                return Task.FromResult(SingleCategoryJson);
            },
            () =>
            {
                fallbackCalls++;
                return Task.FromResult<List<GameBananaService.CategoryRecord>?>([]);
            });

        var category = Assert.Single(Assert.IsType<List<GameBananaService.CategoryRecord>>(categories));
        Assert.Equal(47714, category.Id);
        Assert.Equal(0, fallbackCalls);
        Assert.Equal(
            "https://gamebanana.com/apiv13/Util/ModCategory/NestedStructure?_idGameRow=19567",
            requestedUrl);
    }

    [Fact]
    public async Task FetchCategoryTreeWithFallbackAsync_EmptyJsonUsesFallback()
    {
        var fallbackCalls = 0;
        var fallbackCategories = new List<GameBananaService.CategoryRecord>
        {
            new() { Id = 47714, Name = "Fallback Remielle" }
        };

        var categories = await GameBananaService.FetchCategoryTreeWithFallbackAsync(
            19567,
            _ => Task.FromResult("[]"),
            () =>
            {
                fallbackCalls++;
                return Task.FromResult<List<GameBananaService.CategoryRecord>?>(fallbackCategories);
            });

        Assert.Same(fallbackCategories, categories);
        Assert.Equal(1, fallbackCalls);
    }

    [Fact]
    public async Task FetchCategoryTreeWithFallbackAsync_JsonFailureUsesFallback()
    {
        var fallbackCalls = 0;
        var fallbackCategories = new List<GameBananaService.CategoryRecord>
        {
            new() { Id = 47714, Name = "Fallback Remielle" }
        };

        var categories = await GameBananaService.FetchCategoryTreeWithFallbackAsync(
            19567,
            _ => Task.FromException<string>(new HttpRequestException("network unavailable")),
            () =>
            {
                fallbackCalls++;
                return Task.FromResult<List<GameBananaService.CategoryRecord>?>(fallbackCategories);
            });

        Assert.Same(fallbackCategories, categories);
        Assert.Equal(1, fallbackCalls);
    }
}
