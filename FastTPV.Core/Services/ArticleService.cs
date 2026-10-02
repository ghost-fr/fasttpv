using FastTPV.Core.Data;
using FastTPV.Core.Interfaces;
using FastTPV.Core.Models;
using MySql.Data.MySqlClient;
using Serilog;

namespace FastTPV.Core.Services;

public class ArticleService : IArticleRepository
{
    private readonly DatabaseContext _db;
    private static readonly ILogger Logger = Log.ForContext<ArticleService>();

    public ArticleService(DatabaseContext db)
    {
        _db = db;
    }

    public async Task<Article?> GetByIdAsync(int id)
    {
        try
        {
            var query = "SELECT * FROM Articles WHERE Id = @Id";
            var results = await _db.ExecuteQueryAsync(query, new MySqlParameter("@Id", id));
            if (results.Count == 0) return null;
            return MapToArticle(results[0]);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting article by id: {Id}", id);
            return null;
        }
    }

    public async Task<Article?> GetByCodeAsync(string code)
    {
        try
        {
            var query = "SELECT * FROM Articles WHERE Code = @Code";
            var results = await _db.ExecuteQueryAsync(query, new MySqlParameter("@Code", code));
            if (results.Count == 0) return null;
            return MapToArticle(results[0]);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting article by code: {Code}", code);
            return null;
        }
    }

    public async Task<List<Article>> GetAllAsync()
    {
        try
        {
            var query = "SELECT * FROM Articles WHERE IsActive = 1 ORDER BY Name";
            var results = await _db.ExecuteQueryAsync(query);
            return results.Select(MapToArticle).ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting all articles");
            return new List<Article>();
        }
    }

    /// <summary>
    /// All articles, active and inactive, ordered by name. Used by the Inventory screen
    /// (so deactivated products can be found and reactivated) and by the Excel export.
    /// </summary>
    public async Task<List<Article>> GetAllIncludingInactiveAsync()
    {
        try
        {
            var results = await _db.ExecuteQueryAsync("SELECT * FROM Articles ORDER BY Name, Code");
            return results.Select(MapToArticle).ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting all articles (including inactive)");
            return new List<Article>();
        }
    }

    /// <summary>
    /// Business-rule validation shared by the editor and the Excel import.
    /// Returns a user-facing message, or null when the article is valid. Name need not be
    /// unique: the same Name with different Codes (and prices) is fully supported.
    /// </summary>
    public static string? Validate(Article a)
    {
        if (string.IsNullOrWhiteSpace(a.Code)) return "Code (barcode) is required.";
        if (a.Code.Trim().Length > 50) return "Code is longer than 50 characters.";
        if (string.IsNullOrWhiteSpace(a.Name)) return "Name is required.";
        if (a.Name.Trim().Length > 255) return "Name is longer than 255 characters.";
        const decimal MaxMoney = 99_999_999.99m; // DECIMAL(10,2)
        if (a.Price > MaxMoney || a.CostPrice > MaxMoney) return "Price/cost is larger than the supported maximum (99,999,999.99).";
        if (a.Price < 0) return "Price cannot be negative.";
        if (a.CostPrice < 0) return "Cost price cannot be negative.";
        if (a.MinimumStock < 0) return "Minimum stock cannot be negative.";
        if (a.StockLevel < 0) return "Stock cannot be negative.";
        if (!string.Equals(a.PricingMode, "Fixed", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(a.PricingMode, "OpenPrice", StringComparison.OrdinalIgnoreCase))
            return "Pricing mode must be Fixed or OpenPrice.";
        return null;
    }

    /// <summary>Builds the clear "code already used" error, naming the article that owns it.</summary>
    private async Task<DuplicateCodeException> BuildDuplicateAsync(string code, int? exceptId)
    {
        var holder = await GetByCodeAsync(code);
        string message;
        if (holder is not null && holder.Id != exceptId)
        {
            message = $"Code '{code}' is already used by \"{holder.Name}\"" +
                      (holder.IsActive ? "." : " (an inactive product - reactivate it or choose another code).");
        }
        else
        {
            message = $"Code '{code}' is already in use.";
        }
        return new DuplicateCodeException(code, message);
    }

    public async Task<List<Article>> GetByCategoryAsync(string category)
    {
        try
        {
            var query = "SELECT * FROM Articles WHERE Category = @Category AND IsActive = 1 ORDER BY Name";
            var results = await _db.ExecuteQueryAsync(query, new MySqlParameter("@Category", category));
            return results.Select(MapToArticle).ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting articles by category: {Category}", category);
            return new List<Article>();
        }
    }

    public async Task<List<Article>> GetLowStockAsync()
    {
        try
        {
            var query = "SELECT * FROM Articles WHERE IsActive = 1 AND StockLevel <= MinimumStock ORDER BY Name";
            var results = await _db.ExecuteQueryAsync(query);
            return results.Select(MapToArticle).ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting low stock articles");
            return new List<Article>();
        }
    }

    /// <summary>
    /// Inserts a new article. Returns the new Id, or 0 on an unexpected failure (logged).
    /// Throws <see cref="DuplicateCodeException"/> when the Code is already taken - the
    /// database UNIQUE constraint is the authority, so this is also safe under concurrent adds.
    /// </summary>
    public async Task<int> AddAsync(Article article)
    {
        try
        {
            var query = @"INSERT INTO Articles (Code, Name, Description, Price, CostPrice, StockLevel,
                MinimumStock, Category, PricingMode, IsActive, CreatedAt, UpdatedAt)
                VALUES (@Code, @Name, @Description, @Price, @CostPrice, @StockLevel,
                @MinimumStock, @Category, @PricingMode, @IsActive, @CreatedAt, @UpdatedAt)";

            article.Code = article.Code.Trim();
            article.Name = article.Name.Trim();

            var newId = await _db.ExecuteInsertAsync(query,
                new MySqlParameter("@Code", article.Code),
                new MySqlParameter("@Name", article.Name),
                new MySqlParameter("@Description", article.Description),
                new MySqlParameter("@Price", article.Price),
                new MySqlParameter("@CostPrice", article.CostPrice),
                new MySqlParameter("@StockLevel", article.StockLevel),
                new MySqlParameter("@MinimumStock", article.MinimumStock),
                new MySqlParameter("@Category", article.Category),
                new MySqlParameter("@PricingMode", string.IsNullOrWhiteSpace(article.PricingMode) ? "Fixed" : article.PricingMode),
                new MySqlParameter("@IsActive", article.IsActive),
                new MySqlParameter("@CreatedAt", DateTime.Now),
                new MySqlParameter("@UpdatedAt", DateTime.Now));

            article.Id = (int)newId;
            Logger.Information("Article added successfully: {ArticleCode} (Id {Id})", article.Code, article.Id);
            return article.Id;
        }
        catch (MySqlException ex) when (ex.Number == 1062) // unique key violation on Code
        {
            Logger.Warning("Add rejected: duplicate code {ArticleCode}", article.Code);
            throw await BuildDuplicateAsync(article.Code, exceptId: null);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error adding article");
            return 0;
        }
    }

    /// <summary>
    /// Updates every editable field INCLUDING Code (barcode). Deliberately does NOT write
    /// StockLevel: a read-modify-write of stock from a stale screen would overwrite sales
    /// made in the meantime. Change stock through <see cref="AdjustStockAsync"/> (atomic)
    /// via StockMovementService so the change is also audited.
    /// Returns false if the article no longer exists. Throws <see cref="DuplicateCodeException"/>
    /// if the new Code belongs to a different article.
    /// </summary>
    public async Task<bool> UpdateAsync(Article article)
    {
        try
        {
            article.Code = article.Code.Trim();
            article.Name = article.Name.Trim();

            var query = @"UPDATE Articles SET Code = @Code, Name = @Name, Description = @Description,
                Price = @Price, CostPrice = @CostPrice,
                MinimumStock = @MinimumStock, Category = @Category, PricingMode = @PricingMode,
                IsActive = @IsActive, UpdatedAt = @UpdatedAt WHERE Id = @Id";

            var result = await _db.ExecuteNonQueryAsync(query,
                new MySqlParameter("@Code", article.Code),
                new MySqlParameter("@Name", article.Name),
                new MySqlParameter("@Description", article.Description ?? string.Empty),
                new MySqlParameter("@Price", article.Price),
                new MySqlParameter("@CostPrice", article.CostPrice),
                new MySqlParameter("@MinimumStock", article.MinimumStock),
                new MySqlParameter("@Category", article.Category),
                new MySqlParameter("@PricingMode", string.IsNullOrWhiteSpace(article.PricingMode) ? "Fixed" : article.PricingMode),
                new MySqlParameter("@IsActive", article.IsActive),
                new MySqlParameter("@UpdatedAt", DateTime.Now),
                new MySqlParameter("@Id", article.Id));

            if (result > 0)
                Logger.Information("Article updated successfully: {ArticleId} ({ArticleCode})", article.Id, article.Code);
            else
                Logger.Warning("Update matched no rows (article {ArticleId} missing or unchanged)", article.Id);
            return result > 0;
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            Logger.Warning("Update rejected: code {ArticleCode} already used by another article", article.Code);
            throw await BuildDuplicateAsync(article.Code, exceptId: article.Id);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating article: {ArticleId}", article.Id);
            return false;
        }
    }

    public async Task<bool> AdjustStockAsync(int articleId, int delta)
    {
        try
        {
            var query = @"UPDATE Articles SET StockLevel = StockLevel + @Delta, UpdatedAt = @UpdatedAt
                WHERE Id = @Id AND StockLevel + @Delta >= 0";

            var result = await _db.ExecuteNonQueryAsync(query,
                new MySqlParameter("@Delta", delta),
                new MySqlParameter("@UpdatedAt", DateTime.Now),
                new MySqlParameter("@Id", articleId));

            return result > 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error adjusting stock for article: {ArticleId}", articleId);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            var query = "UPDATE Articles SET IsActive = 0 WHERE Id = @Id";
            var result = await _db.ExecuteNonQueryAsync(query, new MySqlParameter("@Id", id));
            Logger.Information("Article deleted (soft delete): {ArticleId}", id);
            return result > 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deleting article: {ArticleId}", id);
            return false;
        }
    }

    private static Article MapToArticle(Dictionary<string, object> row)
    {
        return new Article
        {
            Id = Convert.ToInt32(row["Id"]),
            Code = row["Code"]?.ToString() ?? string.Empty,
            Name = row["Name"]?.ToString() ?? string.Empty,
            Description = row["Description"]?.ToString() ?? string.Empty,
            Price = Convert.ToDecimal(row["Price"]),
            CostPrice = Convert.ToDecimal(row["CostPrice"]),
            StockLevel = Convert.ToInt32(row["StockLevel"]),
            MinimumStock = Convert.ToInt32(row["MinimumStock"]),
            Category = row["Category"]?.ToString() ?? string.Empty,
            PricingMode = row.ContainsKey("PricingMode") && row["PricingMode"] != null && row["PricingMode"] != DBNull.Value
                ? row["PricingMode"]?.ToString() ?? "Fixed"
                : "Fixed",
            IsActive = Convert.ToBoolean(row["IsActive"]),
            CreatedAt = Convert.ToDateTime(row["CreatedAt"]),
            UpdatedAt = Convert.ToDateTime(row["UpdatedAt"])
        };
    }
}
