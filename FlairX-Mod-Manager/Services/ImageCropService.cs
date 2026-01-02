using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

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
        /// Calculate crop rectangle based on the specified crop type
        /// </summary>
        /// <param name="image">Source image</param>
        /// <param name="targetWidth">Target width</param>
        /// <param name="targetHeight">Target height</param>
        /// <param name="cropType">Type of cropping algorithm</param>
        /// <returns>Rectangle with crop coordinates</returns>
        public static Rectangle CalculateCropRectangle(Image image, int targetWidth, int targetHeight, CropType cropType)
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
        private static (int x, int y) CalculateSmartCrop(Image image, int cropWidth, int cropHeight)
        {
            try
            {
                using (var bmp = new Bitmap(image))
                {
                    // Create a low-resolution version for faster processing
                    int sampleWidth = Math.Min(image.Width, 400);
                    int sampleHeight = Math.Min(image.Height, 400);
                    
                    using (var sample = new Bitmap(sampleWidth, sampleHeight))
                    using (var g = Graphics.FromImage(sample))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(bmp, 0, 0, sampleWidth, sampleHeight);
                        
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
        private static (int x, int y) CalculateEntropyCrop(Image image, int cropWidth, int cropHeight)
        {
            try
            {
                using (var bmp = new Bitmap(image))
                {
                    // Create a low-resolution version for faster processing
                    int sampleWidth = Math.Min(image.Width, 400);
                    int sampleHeight = Math.Min(image.Height, 400);
                    
                    using (var sample = new Bitmap(sampleWidth, sampleHeight))
                    using (var g = Graphics.FromImage(sample))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(bmp, 0, 0, sampleWidth, sampleHeight);
                        
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
        private static (int x, int y) CalculateAttentionCrop(Image image, int cropWidth, int cropHeight)
        {
            try
            {
                using (var bmp = new Bitmap(image))
                {
                    // Create a low-resolution version for faster processing
                    int sampleWidth = Math.Min(image.Width, 400);
                    int sampleHeight = Math.Min(image.Height, 400);
                    
                    using (var sample = new Bitmap(sampleWidth, sampleHeight))
                    using (var g = Graphics.FromImage(sample))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(bmp, 0, 0, sampleWidth, sampleHeight);
                        
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
        private static double[,] CalculateEdgeMap(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            double[,] edgeMap = new double[width, height];
            
            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            
            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0;
                int stride = bmpData.Stride;
                
                for (int y = 1; y < height - 1; y++)
                {
                    for (int x = 1; x < width - 1; x++)
                    {
                        // Sobel operator with proper kernels
                        // Gx = [-1  0  1]    Gy = [-1 -2 -1]
                        //      [-2  0  2]         [ 0  0  0]
                        //      [-1  0  1]         [ 1  2  1]
                        int gx = 0, gy = 0;
                        
                        // Top row
                        int offset = (y - 1) * stride + (x - 1) * 3;
                        int gray = (ptr[offset] + ptr[offset + 1] + ptr[offset + 2]) / 3;
                        gx += -1 * gray; gy += -1 * gray;
                        
                        offset = (y - 1) * stride + x * 3;
                        gray = (ptr[offset] + ptr[offset + 1] + ptr[offset + 2]) / 3;
                        gy += -2 * gray;
                        
                        offset = (y - 1) * stride + (x + 1) * 3;
                        gray = (ptr[offset] + ptr[offset + 1] + ptr[offset + 2]) / 3;
                        gx += 1 * gray; gy += -1 * gray;
                        
                        // Middle row
                        offset = y * stride + (x - 1) * 3;
                        gray = (ptr[offset] + ptr[offset + 1] + ptr[offset + 2]) / 3;
                        gx += -2 * gray;
                        
                        offset = y * stride + (x + 1) * 3;
                        gray = (ptr[offset] + ptr[offset + 1] + ptr[offset + 2]) / 3;
                        gx += 2 * gray;
                        
                        // Bottom row
                        offset = (y + 1) * stride + (x - 1) * 3;
                        gray = (ptr[offset] + ptr[offset + 1] + ptr[offset + 2]) / 3;
                        gx += -1 * gray; gy += 1 * gray;
                        
                        offset = (y + 1) * stride + x * 3;
                        gray = (ptr[offset] + ptr[offset + 1] + ptr[offset + 2]) / 3;
                        gy += 2 * gray;
                        
                        offset = (y + 1) * stride + (x + 1) * 3;
                        gray = (ptr[offset] + ptr[offset + 1] + ptr[offset + 2]) / 3;
                        gx += 1 * gray; gy += 1 * gray;
                        
                        edgeMap[x, y] = Math.Sqrt(gx * gx + gy * gy);
                    }
                }
            }
            
            bitmap.UnlockBits(bmpData);
            return edgeMap;
        }
        
        /// <summary>
        /// Calculate entropy map (local variance)
        /// </summary>
        private static double[,] CalculateEntropyMap(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            double[,] entropyMap = new double[width, height];
            
            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            
            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0;
                int stride = bmpData.Stride;
                int windowSize = 5;
                
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
                            for (int dx = -windowSize; dx <= windowSize; dx++)
                            {
                                int offset = (y + dy) * stride + (x + dx) * 3;
                                int gray = (ptr[offset] + ptr[offset + 1] + ptr[offset + 2]) / 3;
                                
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
            }
            
            bitmap.UnlockBits(bmpData);
            return entropyMap;
        }
        
        /// <summary>
        /// Calculate attention map based on brightness and saturation
        /// </summary>
        private static double[,] CalculateAttentionMap(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            double[,] attentionMap = new double[width, height];
            
            BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            
            unsafe
            {
                byte* ptr = (byte*)bmpData.Scan0;
                int stride = bmpData.Stride;
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int offset = y * stride + x * 3;
                        int b = ptr[offset];
                        int g = ptr[offset + 1];
                        int r = ptr[offset + 2];
                        
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
            }
            
            bitmap.UnlockBits(bmpData);
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
