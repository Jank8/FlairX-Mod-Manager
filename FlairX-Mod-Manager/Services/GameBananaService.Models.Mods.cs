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

        // Mod list response models
        public class ModListResponse
        {
            [JsonPropertyName("_aRecords")]
            public List<ModRecord>? Records { get; set; }
            
            [JsonPropertyName("_aMetadata")]
            public ResponseMetadata? Metadata { get; set; }
            
            [JsonPropertyName("_nRecordCount")]
            public int RecordCount { get; set; }
            
            [JsonPropertyName("_nPerpage")]
            public int PerPage { get; set; }
        }

        public class ResponseMetadata
        {
            [JsonPropertyName("_nRecordCount")]
            public int RecordCount { get; set; }
            
            [JsonPropertyName("_nPerpage")]
            public int PerPage { get; set; }
            
            [JsonPropertyName("_bIsComplete")]
            public bool IsComplete { get; set; }
        }

        public class ModRecord
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }
            
            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("_sProfileUrl")]
            public string ProfileUrl { get; set; } = "";
            
            [JsonPropertyName("_tsDateAdded")]
            public long DateAdded { get; set; }
            
            [JsonPropertyName("_tsDateModified")]
            public long DateModified { get; set; }
            
            [JsonPropertyName("_tsDateUpdated")]
            public long DateUpdated { get; set; }
            
            [JsonPropertyName("_nViewCount")]
            public int ViewCount { get; set; }
            
            [JsonPropertyName("_nLikeCount")]
            public int LikeCount { get; set; }
            
            [JsonPropertyName("_nDownloadCount")]
            public int DownloadCount { get; set; }
            
            [JsonPropertyName("_nPostCount")]
            public int PostCount { get; set; }
            
            [JsonPropertyName("_aPreviewMedia")]
            public PreviewMedia? PreviewMedia { get; set; }
            
            [JsonPropertyName("_aSubmitter")]
            public Submitter? Submitter { get; set; }
            
            [JsonPropertyName("_sText")]
            public string? Description { get; set; }
            
            [JsonPropertyName("_aMetadata")]
            public Dictionary<string, object>? Metadata { get; set; }
            
            [JsonPropertyName("_aRootCategory")]
            public RootCategory? RootCategory { get; set; }
            
            [JsonPropertyName("_bHasContentRatings")]
            public bool HasContentRatings { get; set; }
            
            [JsonPropertyName("_bIsNsfw")]
            public bool IsNsfw { get; set; }
            
            /// <summary>
            /// Returns true if the mod has content ratings (apiv11) OR is marked NSFW (apiv6)
            /// </summary>
            public bool HasAnyContentWarning => HasContentRatings || IsNsfw;
            
            [JsonPropertyName("_bHasRipe")]
            public bool HasRipe { get; set; }
            
            // Helper properties to get stats from metadata if main properties are 0
            public int GetDownloadCount()
            {
                if (DownloadCount > 0) return DownloadCount;
                if (Metadata != null && Metadata.TryGetValue("_nDownloadCount", out var value))
                {
                    if (value is JsonElement element && element.ValueKind == JsonValueKind.Number)
                        return element.GetInt32();
                }
                return 0;
            }
            
            public int GetViewCount()
            {
                if (ViewCount > 0) return ViewCount;
                if (Metadata != null && Metadata.TryGetValue("_nViewCount", out var value))
                {
                    if (value is JsonElement element && element.ValueKind == JsonValueKind.Number)
                        return element.GetInt32();
                }
                return 0;
            }
            
            public int GetLikeCount()
            {
                if (LikeCount > 0) return LikeCount;
                if (Metadata != null && Metadata.TryGetValue("_nLikeCount", out var value))
                {
                    if (value is JsonElement element && element.ValueKind == JsonValueKind.Number)
                        return element.GetInt32();
                }
                return 0;
            }

            public int GetPostCount()
            {
                if (PostCount > 0) return PostCount;
                if (Metadata != null && Metadata.TryGetValue("_nPostCount", out var value) &&
                    value is JsonElement element && element.ValueKind == JsonValueKind.Number)
                {
                    return element.GetInt32();
                }

                return 0;
            }
        }

        public class PreviewMedia
        {
            [JsonPropertyName("_aImages")]
            public List<ImageInfo>? Images { get; set; }
        }

        public class ImageInfo
        {
            [JsonPropertyName("_sType")]
            public string Type { get; set; } = "";
            
            [JsonPropertyName("_sBaseUrl")]
            public string BaseUrl { get; set; } = "";
            
            [JsonPropertyName("_sFile")]
            public string File { get; set; } = "";
            
            [JsonPropertyName("_sFile530")]
            public string? File530 { get; set; }
            
            [JsonPropertyName("_sFile220")]
            public string? File220 { get; set; }
            
            [JsonPropertyName("_sFile100")]
            public string? File100 { get; set; }
        }

        public class ModCategory
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }
            
            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("_sModelName")]
            public string? ModelName { get; set; }
            
            [JsonPropertyName("_sProfileUrl")]
            public string? ProfileUrl { get; set; }
            
            [JsonPropertyName("_sIconUrl")]
            public string? IconUrl { get; set; }
        }

        public class Submitter
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }
            
            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("_sProfileUrl")]
            public string ProfileUrl { get; set; } = "";
            
            [JsonPropertyName("_sAvatarUrl")]
            public string? AvatarUrl { get; set; }
        }

        public class RootCategory
        {
            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("_sProfileUrl")]
            public string ProfileUrl { get; set; } = "";
            
            [JsonPropertyName("_sIconUrl")]
            public string? IconUrl { get; set; }
        }

        public class Game
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }
            
            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("_sAbbreviation")]
            public string Abbreviation { get; set; } = "";
        }

        public class AuthorMatchResponse
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }
            
            [JsonPropertyName("name")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("avatar")]
            public string? Avatar { get; set; }
        }

        /// <summary>
        /// Internal model for apiv6 ByCategory response items — PreviewMedia is a flat list, not a nested object.
        /// </summary>
        private class ModRecordV6
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }

            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";

            [JsonPropertyName("_sProfileUrl")]
            public string ProfileUrl { get; set; } = "";

            [JsonPropertyName("_tsDateAdded")]
            public long DateAdded { get; set; }

            [JsonPropertyName("_tsDateUpdated")]
            public long DateUpdated { get; set; }

            [JsonPropertyName("_bIsNsfw")]
            public bool IsNsfw { get; set; }

            [JsonPropertyName("_nLikeCount")]
            public int LikeCount { get; set; }

            [JsonPropertyName("_nViewCount")]
            public int ViewCount { get; set; }

            [JsonPropertyName("_nDownloadCount")]
            public int DownloadCount { get; set; }

            [JsonPropertyName("_nPostCount")]
            public int PostCount { get; set; }

            [JsonPropertyName("_aPreviewMedia")]
            public List<ImageInfo>? PreviewImages { get; set; }

            [JsonPropertyName("_aSubmitter")]
            public Submitter? Submitter { get; set; }
        }

        /// <summary>
        /// Valid sort orders for GetModsByCategoryAsync (used by apiv6 ByCategory).
        /// When searching by name, sorting is not supported by the API and this value is ignored.
        /// </summary>
        public enum CategorySortOrder
        {
            LatestUpdated,   // _tsDateUpdated,DESC  (default)
            Newest,          // _tsDateAdded,DESC
            Oldest,          // _tsDateAdded,ASC
            MostLiked,       // _nLikeCount,DESC
            MostViewed,      // _nViewCount,DESC
            MostDownloaded,  // _nDownloadCount,DESC
            MostCommented,   // _nPostCount,DESC
        }
    }
}
