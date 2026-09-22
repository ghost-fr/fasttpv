using FastTPV.Core.Data;
using FastTPV.Core.Models;
using MySql.Data.MySqlClient;

namespace FastTPV.Core.Services;

/// <summary>
/// CRUD for the Categories table, and the piece that actually links it to
/// Article.Category: EnsureExistsAsync is called by InventoryWindowViewModel
/// every time a product is saved, so any category name a user types is
/// automatically registered here too. Article.Category itself stays a plain
/// string (unchanged schema — see the note on the Category model), but the
/// Categories table becomes the real source of truth for "which category names
/// exist," and InventoryWindow's autocomplete reads from here rather than just
/// scraping whatever strings happen to already be on Articles.
/// </summary>
public class CategoryService
{
    private readonly DatabaseContext _db;
    public CategoryService(DatabaseContext db) => _db = db;

    public async Task<List<Category>> GetAllAsync()
    {
        var rows = await _db.ExecuteQueryAsync("SELECT * FROM Categories ORDER BY Name");
        return rows.Select(MapRow).ToList();
    }

    public async Task<List<string>> GetActiveNamesAsync()
    {
        var rows = await _db.ExecuteQueryAsync(
            "SELECT Name FROM Categories WHERE IsActive = 1 ORDER BY Name");
        return rows.Select(r => r["Name"].ToString()!).ToList();
    }

    /// <summary>
    /// Registers a category name if it isn't already present (case-insensitive),
    /// re-activating it first if it had been deactivated. Safe to call on every
    /// article save — a no-op after the first time a given name is seen.
    /// </summary>
    public async Task EnsureExistsAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var trimmed = name.Trim();

        var existing = await _db.ExecuteQueryAsync(
            "SELECT Id, IsActive FROM Categories WHERE Name = @Name LIMIT 1",
            new MySqlParameter("@Name", trimmed));

        if (existing.Count == 0)
        {
            await _db.ExecuteInsertAsync(
                "INSERT INTO Categories (Name, Description, IsActive, CreatedAt) " +
                "VALUES (@Name, '', 1, @CreatedAt)",
                new MySqlParameter("@Name", trimmed),
                new MySqlParameter("@CreatedAt", DateTime.UtcNow));
        }
        else if (!Convert.ToBoolean(existing[0]["IsActive"]))
        {
            await _db.ExecuteNonQueryAsync(
                "UPDATE Categories SET IsActive = 1 WHERE Id = @Id",
                new MySqlParameter("@Id", Convert.ToInt32(existing[0]["Id"])));
        }
    }

    public async Task<int> AddAsync(Category category) =>
        (int)await _db.ExecuteInsertAsync(
            "INSERT INTO Categories (Name, Description, IsActive, CreatedAt) " +
            "VALUES (@Name, @Description, @IsActive, @CreatedAt)",
            new MySqlParameter("@Name", category.Name.Trim()),
            new MySqlParameter("@Description", category.Description ?? string.Empty),
            new MySqlParameter("@IsActive", category.IsActive),
            new MySqlParameter("@CreatedAt", DateTime.UtcNow));

    public Task<int> UpdateAsync(Category category) =>
        _db.ExecuteNonQueryAsync(
            "UPDATE Categories SET Name = @Name, Description = @Description, IsActive = @IsActive WHERE Id = @Id",
            new MySqlParameter("@Name", category.Name.Trim()),
            new MySqlParameter("@Description", category.Description ?? string.Empty),
            new MySqlParameter("@IsActive", category.IsActive),
            new MySqlParameter("@Id", category.Id));

    /// <summary>
    /// Soft-delete only (IsActive = 0) — Articles.Category is a free string with no
    /// FK, so there's nothing to block a delete on the DB side, but hard-deleting a
    /// category name that existing products still carry would just orphan that text
    /// silently. Deactivating keeps it out of the autocomplete list for new entries
    /// while leaving already-tagged products untouched.
    /// </summary>
    public Task<int> DeactivateAsync(int id) =>
        _db.ExecuteNonQueryAsync("UPDATE Categories SET IsActive = 0 WHERE Id = @Id",
            new MySqlParameter("@Id", id));

    private static Category MapRow(Dictionary<string, object> r) => new()
    {
        Id = Convert.ToInt32(r["Id"]),
        Name = r["Name"].ToString()!,
        Description = r["Description"]?.ToString() ?? string.Empty,
        IsActive = Convert.ToBoolean(r["IsActive"]),
        CreatedAt = Convert.ToDateTime(r["CreatedAt"])
    };
}
