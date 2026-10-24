using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using FlairX_Mod_Manager;

namespace FlairX_Mod_Manager.Services
{
    public partial class GameBananaService
    {

        // Category response models
        public class CategoryListResponse
        {
            [JsonPropertyName("_aRecords")]
            public List<CategoryRecord>? Records { get; set; }
            
            [JsonPropertyName("_aMetadata")]
            public ResponseMetadata? Metadata { get; set; }
            
            [JsonPropertyName("_nRecordCount")]
            public int RecordCount { get; set; }
        }

        public class CategoryRecord
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }
            
            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("_sProfileUrl")]
            public string ProfileUrl { get; set; } = "";
            
            [JsonPropertyName("_sIconUrl")]
            public string? IconUrl { get; set; }
            
            [JsonPropertyName("_aPreviewMedia")]
            public PreviewMediaCategory? PreviewMedia { get; set; }
            
            [JsonPropertyName("_aGame")]
            public Game? Game { get; set; }
            
            [JsonPropertyName("_bHasFiles")]
            public bool HasFiles { get; set; }
            
            [JsonPropertyName("_tsDateAdded")]
            public long? DateAdded { get; set; }

            /// <summary>Children (subcategories) returned by the nested category API</summary>
            [JsonPropertyName("_aChildren")]
            public List<CategoryRecord> Children { get; set; } = new();
            
            /// <summary>
            /// Get icon URL from either _sIconUrl or _aPreviewMedia
            /// </summary>
            public string? GetIconUrl()
            {
                // Try _sIconUrl first
                if (!string.IsNullOrEmpty(IconUrl))
                    return IconUrl;
                
                // Try _aPreviewMedia._aImages[0]._sUrl
                if (PreviewMedia?.Images != null && PreviewMedia.Images.Count > 0)
                {
                    var iconImage = PreviewMedia.Images.FirstOrDefault(i => i.Type == "icon");
                    if (iconImage != null)
                        return iconImage.Url;
                }
                
                return null;
            }
        }

        public class PreviewMediaCategory
        {
            [JsonPropertyName("_aImages")]
            public List<CategoryImage>? Images { get; set; }
        }

        public class CategoryImage
        {
            [JsonPropertyName("_sType")]
            public string Type { get; set; } = "";
            
            [JsonPropertyName("_sUrl")]
            public string Url { get; set; } = "";
        }
    }
}
