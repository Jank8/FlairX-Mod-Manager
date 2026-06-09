using System;

namespace Magazynier.Models
{
    /// <summary>
    /// Represents a physical asset/device in the inventory
    /// </summary>
    public class Asset
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string? InventoryNumber { get; set; }
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; } // denormalized for display
        public string? Manufacturer { get; set; }
        public string? Model { get; set; }
        public string? Description { get; set; }
        public AssetStatus Status { get; set; } = AssetStatus.Available;
        public DateTime? PurchaseDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Current assignment info (populated via JOIN)
        public int? AssignedUserId { get; set; }
        public string? AssignedUserName { get; set; }
        public string? AssignmentDecisionNumber { get; set; }
        public DateTime? AssignmentDate { get; set; }

        public override string ToString() => Name;
    }

    public enum AssetStatus
    {
        Available = 0,
        Assigned = 1,
        InRepair = 2,
        Retired = 3
    }
}
