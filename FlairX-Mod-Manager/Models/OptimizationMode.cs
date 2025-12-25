namespace FlairX_Mod_Manager.Models
{
    public enum OptimizationMode
    {
        Standard,       // Standard optimization: optimize quality, generate thumbnails (catmini, catprev, minitile), auto crop
        CategoryFull    // Category drag&drop: full optimization with manual crop inspection for catprev/catmini
    }
}
