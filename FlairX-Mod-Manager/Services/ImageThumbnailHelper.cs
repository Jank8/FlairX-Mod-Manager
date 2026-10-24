using System;
using System.IO;
using Microsoft.UI.Xaml.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FlairX_Mod_Manager.Services
{
    /// <summary>
    /// Builds the 64x64 preview bitmap used by crop and minitile selection panels.
    /// </summary>
    public static class ImageThumbnailHelper
    {
        public static BitmapImage? CreateThumbnailFromImage(Image<Rgba32>? sourceImage)
        {
            if (sourceImage == null)
            {
                return null;
            }

            try
            {
                using var thumbnail = sourceImage.Clone(ctx => ctx.Resize(64, 64));
                using var ms = new MemoryStream();
                thumbnail.SaveAsPng(ms);
                ms.Position = 0;

                var bitmap = new BitmapImage();
                bitmap.SetSource(ms.AsRandomAccessStream());
                return bitmap;
            }
            catch (Exception ex)
            {
                Logger.LogError("[THUMBNAIL] Failed to create thumbnail", ex);
                return null;
            }
        }
    }
}
