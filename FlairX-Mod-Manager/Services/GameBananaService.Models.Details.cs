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

        // Mod details response
        public class ModDetailsResponse
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }
            
            [JsonPropertyName("_sName")]
            public string Name { get; set; } = "";
            
            [JsonPropertyName("_sText")]
            public string? Description { get; set; }
            
            [JsonPropertyName("_sProfileUrl")]
            public string ProfileUrl { get; set; } = "";
            
            [JsonPropertyName("_aSubmitter")]
            public Submitter? Submitter { get; set; }
            
            [JsonPropertyName("_aFiles")]
            public List<ModFile>? Files { get; set; }
            
            [JsonPropertyName("_aPreviewMedia")]
            public PreviewMedia? PreviewMedia { get; set; }
            
            [JsonPropertyName("_tsDateAdded")]
            public long? DateAdded { get; set; }
            
            [JsonPropertyName("_tsDateModified")]
            public long? DateModified { get; set; }
            
            [JsonPropertyName("_tsDateUpdated")]
            public long? DateUpdated { get; set; }
            
            [JsonPropertyName("_nViewCount")]
            public int? ViewCount { get; set; }
            
            [JsonPropertyName("_nLikeCount")]
            public int? LikeCount { get; set; }
            
            [JsonPropertyName("_nDownloadCount")]
            public int? DownloadCount { get; set; }
            
            [JsonPropertyName("_nPostCount")]
            public int? PostCount { get; set; }
            
            [JsonPropertyName("_aCategory")]
            public ModCategory? Category { get; set; }
            
            [JsonPropertyName("_aGame")]
            public Game? Game { get; set; }
            
            [JsonPropertyName("_sVersion")]
            public string? Version { get; set; }
            
            [JsonPropertyName("_bIsObsolete")]
            public bool? IsObsolete { get; set; }
            
            [JsonPropertyName("_aRootCategory")]
            public RootCategory? RootCategory { get; set; }

            [JsonPropertyName("_bIsNsfw")]
            public bool? IsNsfw { get; set; }

            [JsonPropertyName("_bHasContentRatings")]
            public bool? HasContentRatings { get; set; }
            
            /// <summary>
            /// Check if mod data is valid (not private/removed)
            /// </summary>
            public bool IsAvailable => Submitter != null && Files != null;
            
            /// <summary>
            /// Get download count with fallback to 0
            /// </summary>
            public int GetDownloadCount() => DownloadCount ?? 0;
            
            /// <summary>
            /// Get view count with fallback to 0
            /// </summary>
            public int GetViewCount() => ViewCount ?? 0;
            
            /// <summary>
            /// Get like count with fallback to 0
            /// </summary>
            public int GetLikeCount() => LikeCount ?? 0;
            
            /// <summary>
            /// Check if mod has NSFW content.
            /// The list's IsRated flag is the most reliable source; these detail flags are a
            /// fallback for mods opened directly by URL.
            /// </summary>
            public bool IsNSFW() => IsNsfw == true || HasContentRatings == true;
        }

        public class ModFile
        {
            [JsonPropertyName("_idRow")]
            public int Id { get; set; }
            
            [JsonPropertyName("_sFile")]
            public string FileName { get; set; } = "";
            
            [JsonPropertyName("_nFilesize")]
            public long FileSize { get; set; }
            
            [JsonPropertyName("_sDescription")]
            public string? Description { get; set; }
            
            [JsonPropertyName("_sDownloadUrl")]
            public string DownloadUrl { get; set; } = "";
            
            [JsonPropertyName("_tsDateAdded")]
            public long DateAdded { get; set; }
            
            [JsonPropertyName("_nDownloadCount")]
            public int DownloadCount { get; set; }
        }
    }
}
