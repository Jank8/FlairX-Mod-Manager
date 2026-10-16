using FlairX_Mod_Manager.Services;
using Xunit;

namespace FlairX_Mod_Manager.Tests;

public class GameBananaImageUrlTests
{
    [Fact]
    public void GetListPreviewImageUrl_Prefers530PixelThumbnail()
    {
        var image = new GameBananaService.ImageInfo
        {
            BaseUrl = "https://images.gamebanana.com/img/ss/mods",
            File = "original.jpg",
            File100 = "100.jpg",
            File220 = "220.jpg",
            File530 = "530.jpg"
        };

        var url = GameBananaService.GetListPreviewImageUrl(image);

        Assert.Equal("https://images.gamebanana.com/img/ss/mods/530.jpg", url);
    }

    [Fact]
    public void GetListPreviewImageUrl_FallsBackWithoutLoadingOriginalWhen220Exists()
    {
        var image = new GameBananaService.ImageInfo
        {
            BaseUrl = "https://images.gamebanana.com/img/ss/mods/",
            File = "original.jpg",
            File100 = "100.jpg",
            File220 = "220.jpg"
        };

        var url = GameBananaService.GetListPreviewImageUrl(image);

        Assert.Equal("https://images.gamebanana.com/img/ss/mods/220.jpg", url);
    }
}
