namespace FlairX_Mod_Manager.Models
{
    public enum OptimizationMode
    {
        Full,           // Full optimization: resize, crop, create preview/minitile/catmini, delete originals
        Lite,           // Lite optimization: optimize quality without resize/crop, create minitile, delete originals
        Miniatures,     // Generate miniatures only (minitile.jpg, catmini.jpg), keep originals
        Rename,         // Rename files to standard names only (preview.jpg, preview-01.jpg, etc.)
        Disabled        // No optimization, just copy files with standard names (for drag&drop only)
    }
}
