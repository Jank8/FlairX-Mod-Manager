using System;

namespace Magazynier.Models
{
    /// <summary>
    /// Represents an assignment of an asset to a user, with a decision number
    /// </summary>
    public class Assignment
    {
        public int Id { get; set; }
        public int AssetId { get; set; }
        public string? AssetName { get; set; }       // denormalized for display
        public string? AssetSerial { get; set; }     // denormalized for display
        public int UserId { get; set; }
        public string? UserName { get; set; }        // denormalized for display
        public string DecisionNumber { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; } = DateTime.Now;
        public DateTime? ReturnedAt { get; set; }
        public string? Notes { get; set; }

        public bool IsActive => ReturnedAt == null;
    }
}
