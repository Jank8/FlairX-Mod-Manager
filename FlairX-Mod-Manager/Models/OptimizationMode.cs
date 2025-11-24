namespace FlairX_Mod_Manager.Models
{
    public enum OptimizationMode
    {
        Full,           // Full optimization with resize and crop
        NoResize,       // Optimize quality only, no resize/crop
        Miniatures,     // Generate miniatures only (minitile.jpg, catmini.jpg)
        Rename,         // Rename files to standard names only
        Disabled        // No optimization
    }
}
