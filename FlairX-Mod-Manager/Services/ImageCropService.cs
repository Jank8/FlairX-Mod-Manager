using System;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FlairX_Mod_Manager.Services
{
    public enum CropType
    {
        Center,
        Smart,
        Entropy,
        Attention
    }

    public static class ImageCropService
    {
        /// <summary>
        /// Calculate crop rectangle based on the specified crop type (ImageSharp version)
        /// </summary>
        public static Rectangle CalculateCropRectangle(Image<Rgba32> image, int targetWidth, int targetHeight, CropType cropType)
        {
            double targetRatio = (double)targetWidth / targetHeight;
            double sourceRatio = (double)image.Width / image.Height;
            
            int cropWidth, cropHeight;
            
            // Calculate crop dimensions to match target aspect ratio
            if (sourceRatio > targetRatio)
            {
                // Source is wider - crop width
                cropHeight = image.Height;
                cropWidth = (int)(cropHeight * targetRatio);
            }
            else
            {
                // Source is taller - crop height
                cropWidth = image.Width;
                cropHeight = (int)(cropWidth / targetRatio);
            }
            
            // Ensure crop dimensions don't exceed image dimensions
            cropWidth = Math.Min(cropWidth, image.Width);
            cropHeight = Math.Min(cropHeight, image.Height);
            
            int x, y;
            
            switch (cropType)
            {
                case CropType.Center:
                    (x, y) = CalculateCenterCrop(image.Width, image.Height, cropWidth, cropHeight);
                    break;
                    
                case CropType.Smart:
                    (x, y) = CalculateSmartCrop(image, cropWidth, cropHeight);
                    break;
                    
                case CropType.Entropy:
                    (x, y) = CalculateEntropyCrop(image, cropWidth, cropHeight);
                    break;
                    
                case CropType.Attention:
                    (x, y) = CalculateAttentionCrop(image, cropWidth, cropHeight);
                    break;
                    
                default:
                    // Default to center crop
                    (x, y) = CalculateCenterCrop(image.Width, image.Height, cropWidth, cropHeight);
                    break;
            }
            
            return new Rectangle(x, y, cropWidth, cropHeight);
        }
        
        /// <summary>
        /// Center crop - crops from the center of the image
        /// </summary>
        private static (int x, int y) CalculateCenterCrop(int imageWidth, int imageHeight, int cropWidth, int cropHeight)
        {
            int x = (imageWidth - cropWidth) / 2;
            int y = (imageHeight - cropHeight) / 2;
            return (x, y);
        }
        
        /// <summary>
        /// Smart crop - uses edge detection to find the most interesting area
        /// </summary>
        private static (int x, int y) CalculateSmartCrop(Image<Rgba32> image, int cropWidth, int cropHeight)
        {
            try
            {
                // Create a low-resolution version for faster processing
                int sampleWidth = Math.Min(image.Width, 400);
                int sampleHeight = Math.Min(image.Height, 400);
                
                using (var sample = image.Clone(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = new Size(sampleWidth, sampleHeight),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.Bicubic  // = HighQualityBicubic
                })))
                {
                    // Calculate edge intensity map
                    double[,] edgeMap = CalculateEdgeMap(sample);
                    
                    // Find the best crop position by sliding window
                    double scaleX = (double)image.Width / sampleWidth;
                    double scaleY = (double)image.Height / sampleHeight;
                    
                    int sampleCropWidth = (int)(cropWidth / scaleX);
                    int sampleCropHeight = (int)(cropHeight / scaleY);
                    
                    var (bestX, bestY) = FindBestCropPosition(edgeMap, sampleCropWidth, sampleCropHeight);
                    
                    // Scale back to original image coordinates
                    int x = (int)(bestX * scaleX);
                    int y = (int)(bestY * scaleY);
                    
                    // Ensure within bounds
                    x = Math.Max(0, Math.Min(x, image.Width - cropWidth));
                    y = Math.Max(0, Math.Min(y, image.Height - cropHeight));
                    
                    return (x, y);
                }
            }
            catch
            {
                // Fallback to center crop on error
                return CalculateCenterCrop(image.Width, image.Height, cropWidth, cropHeight);
            }
        }
        
        /// <summary>
        /// Entropy crop - finds the area with the most detail/information
        /// </summary>
        private static (int x, int y) CalculateEntropyCrop(Image<Rgba32> image, int cropWidth, int cropHeight)
        {
            try
            {
                // Create a low-resolution version for faster processing
                int sampleWidth = Math.Min(image.Width, 400);
                int sampleHeight = Math.Min(image.Height, 400);
                
                using (var sample = image.Clone(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = new Size(sampleWidth, sampleHeight),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.Bicubic  // = HighQualityBicubic
                })))
                {
                    // Calculate entropy map
                    double[,] entropyMap = CalculateEntropyMap(sample);
                    
                    // Find the best crop position
                    double scaleX = (double)image.Width / sampleWidth;
                    double scaleY = (double)image.Height / sampleHeight;
                    
                    int sampleCropWidth = (int)(cropWidth / scaleX);
                    int sampleCropHeight = (int)(cropHeight / scaleY);
                    
                    var (bestX, bestY) = FindBestCropPosition(entropyMap, sampleCropWidth, sampleCropHeight);
                    
                    // Scale back to original image coordinates
                    int x = (int)(bestX * scaleX);
                    int y = (int)(bestY * scaleY);
                    
                    // Ensure within bounds
                    x = Math.Max(0, Math.Min(x, image.Width - cropWidth));
                    y = Math.Max(0, Math.Min(y, image.Height - cropHeight));
                    
                    return (x, y);
                }
            }
            catch
            {
                // Fallback to center crop on error
                return CalculateCenterCrop(image.Width, image.Height, cropWidth, cropHeight);
            }
        }
        
        /// <summary>
        /// Attention crop - focuses on bright areas and potential focal points
        /// </summary>
        private static (int x, int y) CalculateAttentionCrop(Image<Rgba32> image, int cropWidth, int cropHeight)
        {
            try
            {
                // Create a low-resolution version for faster processing
                int sampleWidth = Math.Min(image.Width, 400);
                int sampleHeight = Math.Min(image.Height, 400);
                
                using (var sample = image.Clone(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = new Size(sampleWidth, sampleHeight),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.Bicubic  // = HighQualityBicubic
                })))
                {
                    // Calculate attention map (brightness + saturation)
                    double[,] attentionMap = CalculateAttentionMap(sample);
                    
                    // Find the best crop position
                    double scaleX = (double)image.Width / sampleWidth;
                    double scaleY = (double)image.Height / sampleHeight;
                    
                    int sampleCropWidth = (int)(cropWidth / scaleX);
                    int sampleCropHeight = (int)(cropHeight / scaleY);
                    
                    var (bestX, bestY) = FindBestCropPosition(attentionMap, sampleCropWidth, sampleCropHeight);
                    
                    // Scale back to original image coordinates
                    int x = (int)(bestX * scaleX);
                    int y = (int)(bestY * scaleY);
                    
                    // Ensure within bounds
                    x = Math.Max(0, Math.Min(x, image.Width - cropWidth));
                    y = Math.Max(0, Math.Min(y, image.Height - cropHeight));
                    
                    return (x, y);
                }
            }
            catch
            {
                // Fallback to center crop on error
                return CalculateCenterCrop(image.Width, image.Height, cropWidth, cropHeight);
            }
        }
        
        /// <summary>
        /// Calculate edge detection map using Sobel operator
        /// </summary>
        private static double[,] CalculateEdgeMap(Image<Rgba32> image)
        {
            int width = image.Width;
            int height = image.Height;
            double[,] edgeMap = new double[width, height];
            
            // Process pixels safely with ImageSharp (no unsafe code)
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 1; y < height - 1; y++)
                {
                    Span<Rgba32> prevRow = accessor.GetRowSpan(y - 1);
                    Span<Rgba32> currRow = accessor.GetRowSpan(y);
                    Span<Rgba32> nextRow = accessor.GetRowSpan(y + 1);
                    
                    for (int x = 1; x < width - 1; x++)
                    {
                        // Sobel operator with proper kernels
                        // Gx = [-1  0  1]    Gy = [-1 -2 -1]
                        //      [-2  0  2]         [ 0  0  0]
                        //      [-1  0  1]         [ 1  2  1]
                        int gx = 0, gy = 0;
                        
                        // Top row
                        int gray = (prevRow[x - 1].R + prevRow[x - 1].G + prevRow[x - 1].B) / 3;
                        gx += -1 * gray; gy += -1 * gray;
                        
                        gray = (prevRow[x].R + prevRow[x].G + prevRow[x].B) / 3;
                        gy += -2 * gray;
                        
                        gray = (prevRow[x + 1].R + prevRow[x + 1].G + prevRow[x + 1].B) / 3;
                        gx += 1 * gray; gy += -1 * gray;
                        
                        // Middle row
                        gray = (currRow[x - 1].R + currRow[x - 1].G + currRow[x - 1].B) / 3;
                        gx += -2 * gray;
                        
                        gray = (currRow[x + 1].R + currRow[x + 1].G + currRow[x + 1].B) / 3;
                        gx += 2 * gray;
                        
                        // Bottom row
                        gray = (nextRow[x - 1].R + nextRow[x - 1].G + nextRow[x - 1].B) / 3;
                        gx += -1 * gray; gy += 1 * gray;
                        
                        gray = (nextRow[x].R + nextRow[x].G + nextRow[x].B) / 3;
                        gy += 2 * gray;
                        
                        gray = (nextRow[x + 1].R + nextRow[x + 1].G + nextRow[x + 1].B) / 3;
                        gx += 1 * gray; gy += 1 * gray;
                        
                        edgeMap[x, y] = Math.Sqrt(gx * gx + gy * gy);
                    }
                }
            });
            
            return edgeMap;
        }
        
        /// <summary>
        /// Calculate entropy map (local variance)
        /// </summary>
        private static double[,] CalculateEntropyMap(Image<Rgba32> image)
        {
            int width = image.Width;
            int height = image.Height;
            double[,] entropyMap = new double[width, height];
            int windowSize = 5;
            
            // Process pixels safely with ImageSharp
            image.ProcessPixelRows(accessor =>
            {
                for (int y = windowSize; y < height - windowSize; y++)
                {
                    for (int x = windowSize; x < width - windowSize; x++)
                    {
                        double sum = 0;
                        double sumSq = 0;
                        int count = 0;
                        
                        // Calculate variance in local window
                        for (int dy = -windowSize; dy <= windowSize; dy++)
                        {
                            Span<Rgba32> row = accessor.GetRowSpan(y + dy);
                            for (int dx = -windowSize; dx <= windowSize; dx++)
                            {
                                ref Rgba32 pixel = ref row[x + dx];
                                int gray = (pixel.R + pixel.G + pixel.B) / 3;
                                
                                sum += gray;
                                sumSq += gray * gray;
                                count++;
                            }
                        }
                        
                        double mean = sum / count;
                        double variance = (sumSq / count) - (mean * mean);
                        entropyMap[x, y] = variance;
                    }
                }
            });
            
            return entropyMap;
        }
        
        /// <summary>
        /// Calculate attention map based on brightness and saturation
        /// </summary>
        private static double[,] CalculateAttentionMap(Image<Rgba32> image)
        {
            int width = image.Width;
            int height = image.Height;
            double[,] attentionMap = new double[width, height];
            
            // Process pixels safely with ImageSharp
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < height; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);
                    
                    for (int x = 0; x < width; x++)
                    {
                        ref Rgba32 pixel = ref row[x];
                        int r = pixel.R;
                        int g = pixel.G;
                        int b = pixel.B;
                        
                        // Calculate brightness (0-255)
                        double brightness = (r + g + b) / 3.0;
                        
                        // Calculate saturation (0-1)
                        int max = Math.Max(r, Math.Max(g, b));
                        int min = Math.Min(r, Math.Min(g, b));
                        double saturation = max == 0 ? 0 : (max - min) / (double)max;
                        
                        // Combine brightness and saturation with bias towards center
                        double centerX = width / 2.0;
                        double centerY = height / 2.0;
                        double distanceFromCenter = Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
                        double maxDistance = Math.Sqrt(centerX * centerX + centerY * centerY);
                        double centerBias = 1.0 - (distanceFromCenter / maxDistance) * 0.3; // 30% bias towards center
                        
                        // Normalize: brightness (0-255) * 0.7 + saturation (0-1) * 255 * 0.3
                        // Both components now in same scale (0-255)
                        attentionMap[x, y] = (brightness * 0.7 + saturation * 255.0 * 0.3) * centerBias;
                    }
                }
            });
            
            return attentionMap;
        }
        
        /// <summary>
        /// Find the best crop position by sliding window over the map
        /// </summary>
        private static (int x, int y) FindBestCropPosition(double[,] map, int cropWidth, int cropHeight)
        {
            int width = map.GetLength(0);
            int height = map.GetLength(1);
            
            double bestScore = double.MinValue;
            int bestX = 0;
            int bestY = 0;
            
            // Slide window with step size for performance
            int step = Math.Max(1, Math.Min(cropWidth, cropHeight) / 20);
            
            for (int y = 0; y <= height - cropHeight; y += step)
            {
                for (int x = 0; x <= width - cropWidth; x += step)
                {
                    double score = 0;
                    int count = 0;
                    
                    // Sample points in the crop area (not every pixel for performance)
                    int sampleStep = Math.Max(1, Math.Min(cropWidth, cropHeight) / 20);
                    
                    for (int dy = 0; dy < cropHeight; dy += sampleStep)
                    {
                        for (int dx = 0; dx < cropWidth; dx += sampleStep)
                        {
                            if (x + dx < width && y + dy < height)
                            {
                                score += map[x + dx, y + dy];
                                count++;
                            }
                        }
                    }
                    
                    score /= count; // Average score
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestX = x;
                        bestY = y;
                    }
                }
            }
            
            return (bestX, bestY);
        }
    }
}
