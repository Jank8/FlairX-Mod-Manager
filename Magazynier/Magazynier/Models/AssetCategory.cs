namespace Magazynier.Models
{
    /// <summary>
    /// Represents a device type/category (e.g. Monitor, Keyboard, Computer)
    /// </summary>
    public class AssetCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string IconGlyph { get; set; } = "\uE7F4"; // default: device icon

        public override string ToString() => Name;
    }
}
