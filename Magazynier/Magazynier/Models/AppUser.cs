namespace Magazynier.Models
{
    /// <summary>
    /// Represents a person/employee who can be assigned assets
    /// </summary>
    public class AppUser
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public bool IsActive { get; set; } = true;

        public string FullName => $"{FirstName} {LastName}".Trim();

        public override string ToString() => FullName;
    }
}
