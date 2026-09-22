using FastTPV.Core.Data;
using FastTPV.Core.Interfaces;
using FastTPV.Core.Models;
using MySql.Data.MySqlClient;
using Serilog;

namespace FastTPV.Core.Services;

/// <summary>
/// Service for managing articles/products
/// </summary>
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

            if (results.Count == 0)
                return null;

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

            if (results.Count == 0)
                return null;

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
            var query = "SELECT * FROM Articles WHERE IsActive = 1 AND StockLevel <= MinimumStock ORDER BY StockLevel";
            var results = await _db.ExecuteQueryAsync(query);
            return results.Select(MapToArticle).ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting low stock articles");
            return new List<Article>();
        }
    }

    public async Task<int> AddAsync(Article article)
    {
        try
        {
            var query = @"INSERT INTO Articles (Code, Name, Description, Price, CostPrice, StockLevel,
                MinimumStock, Category, IsActive, CreatedAt, UpdatedAt)
                VALUES (@Code, @Name, @Description, @Price, @CostPrice, @StockLevel,
                @MinimumStock, @Category, @IsActive, @CreatedAt, @UpdatedAt)";

            var newId = await _db.ExecuteInsertAsync(query,
                new MySqlParameter("@Code", article.Code),
                new MySqlParameter("@Name", article.Name),
                new MySqlParameter("@Description", article.Description),
                new MySqlParameter("@Price", article.Price),
                new MySqlParameter("@CostPrice", article.CostPrice),
                new MySqlParameter("@StockLevel", article.StockLevel),
                new MySqlParameter("@MinimumStock", article.MinimumStock),
                new MySqlParameter("@Category", article.Category),
                new MySqlParameter("@IsActive", article.IsActive),
                new MySqlParameter("@CreatedAt", DateTime.Now),
                new MySqlParameter("@UpdatedAt", DateTime.Now));

            article.Id = (int)newId;
            Logger.Information("Article added successfully: {ArticleCode} (Id {Id})", article.Code, article.Id);
            return article.Id;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error adding article");
            return 0;
        }
    }

    public async Task<bool> UpdateAsync(Article article)
    {
        try
        {
            var query = @"UPDATE Articles SET Name = @Name, Description = @Description,
                Price = @Price, CostPrice = @CostPrice, StockLevel = @StockLevel,
                MinimumStock = @MinimumStock, Category = @Category, IsActive = @IsActive,
                UpdatedAt = @UpdatedAt WHERE Id = @Id";

            var result = await _db.ExecuteNonQueryAsync(query,
                new MySqlParameter("@Name", article.Name),
                new MySqlParameter("@Description", article.Description),
                new MySqlParameter("@Price", article.Price),
                new MySqlParameter("@CostPrice", article.CostPrice),
                new MySqlParameter("@StockLevel", article.StockLevel),
                new MySqlParameter("@MinimumStock", article.MinimumStock),
                new MySqlParameter("@Category", article.Category),
                new MySqlParameter("@IsActive", article.IsActive),
                new MySqlParameter("@UpdatedAt", DateTime.Now),
                new MySqlParameter("@Id", article.Id));

            Logger.Information("Article updated successfully: {ArticleId}", article.Id);
            return result > 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating article: {ArticleId}", article.Id);
            return false;
        }
    }

    /// <summary>
    /// Atomically adjusts stock by a delta (negative to decrement on sale, positive to
    /// restock). Guards against selling below zero.
    /// </summary>
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
            IsActive = Convert.ToBoolean(row["IsActive"]),
            CreatedAt = Convert.ToDateTime(row["CreatedAt"]),
            UpdatedAt = Convert.ToDateTime(row["UpdatedAt"])
        };
    }
}
