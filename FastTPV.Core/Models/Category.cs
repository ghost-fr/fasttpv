namespace FastTPV.Core.Models;

/// <summary>
/// A named product category. NOTE: Article.Category is still a plain string, not a
/// foreign key to this table — this model/table exists for future use (a managed
/// category list) but nothing currently enforces Articles to reference it. See
/// README-CHANGES.md for what a full migration to this would involve.
/// </summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
