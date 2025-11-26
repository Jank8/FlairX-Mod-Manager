namespace FlairX_Mod_Manager.Models
{
    public enum OptimizationMode
    {
        Full,           // Full optimization: resize, crop, create preview/minitile/catmini, delete originals
        Lite,           // Lite optimization: optimize quality without resize/crop, create minitile, delete originals
        Rename,         // Rename files to standard names + generate thumbnails (preview.jpg, preview-01.jpg, minitile.jpg, etc.)
        RenameOnly      // Rename files to standard names only without generating thumbnails
    }
}
