using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FlairX_Mod_Manager
{
    public static partial class ModListManager
    {
        /// <summary>
        /// Complete mod information stored in master list
        /// </summary>
        public class ModInfo
        {
            public string Name { get; set; } = "";
            public string Directory { get; set; } = "";
            public string Category { get; set; } = "";
            public string Character { get; set; } = "";
            public string Author { get; set; } = "";
            public string Url { get; set; } = "";
            public string ImagePath { get; set; } = "";
            public DateTime LastChecked { get; set; } = DateTime.MinValue;
            public DateTime LastUpdated { get; set; } = DateTime.MinValue;
            public bool IsActive { get; set; }
            public bool IsNSFW { get; set; }
            public bool IsBroken { get; set; }
            public bool HasUpdate { get; set; }
            public bool IsFavorite { get; set; }
            public bool StatusKeeperSync { get; set; }
            public string SyncMethod { get; set; } = "";
            public List<NamespaceInfo> Namespaces { get; set; } = new List<NamespaceInfo>();
        }

        /// <summary>
        /// Namespace information for StatusKeeper sync
        /// </summary>
        public class NamespaceInfo
        {
            public string Namespace { get; set; } = "";
            public List<string> IniFiles { get; set; } = new List<string>();
        }

        public class ModListData
        {
            public DateTime LastUpdated { get; set; }
            // Format: "CategoryName|ModName" to support duplicate mod names across categories
            // Used for: NSFW, Broken, Outdated lists
            public List<string> Mods { get; set; } = new List<string>();
        }

        private class MasterModListData
        {
            public DateTime LastUpdated { get; set; }
            public List<ModInfo> Mods { get; set; } = new List<ModInfo>();
        }
    }
}
