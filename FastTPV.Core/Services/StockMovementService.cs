using FastTPV.Core.Data;
using FastTPV.Core.Models;
using MySql.Data.MySqlClient;
using Serilog;

namespace FastTPV.Core.Services;

/// <summary>
/// Records an audit trail of stock changes. The actual stock number is always
/// changed via ArticleService.AdjustStockAsync (an atomic single-column UPDATE with
/// a WHERE guard against going negative) rather than a fetch-then-full-row-update —
/// this service just logs why the change happened alongside it.
/// </summary>
public sealed class StockMovementService
{
    private readonly DatabaseContext _db;
    private readonly ArticleService _articles;
    private static readonly ILogger Logger = Log.ForContext<StockMovementService>();

    public StockMovementService(DatabaseContext db, ArticleService articles)
    {
        _db = db;
        _articles = articles;
    }

    public async Task<List<StockMovement>> GetRecentAsync(int articleId, int count = 20)
    {
        try
        {
            var query = "SELECT * FROM StockMovements WHERE ArticleId = @ArticleId ORDER BY CreatedAt DESC LIMIT @Count";
            var rows = await _db.ExecuteQueryAsync(query,
                new MySqlParameter("@ArticleId", articleId),
                new MySqlParameter("@Count", count));
            return rows.Select(MapToMovement).ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting stock movements for article {ArticleId}", articleId);
            return new List<StockMovement>();
        }
    }

    /// <summary>Logs a movement without changing stock — use when the stock change
    /// itself was already made elsewhere (e.g. a sale's own AdjustStockAsync call)
    /// and this is purely the audit-trail entry for it.</summary>
    public async Task<bool> RecordAsync(StockMovement movement)
    {
        try
        {
            var query = @"INSERT INTO StockMovements (ArticleId, ArticleCode, ArticleName, Quantity, MovementType, Reason, UserId, Reference, CreatedAt)
                VALUES (@ArticleId, @ArticleCode, @ArticleName, @Quantity, @MovementType, @Reason, @UserId, @Reference, @CreatedAt)";

            var affected = await _db.ExecuteNonQueryAsync(query,
                new MySqlParameter("@ArticleId", movement.ArticleId),
                new MySqlParameter("@ArticleCode", movement.ArticleCode),
                new MySqlParameter("@ArticleName", movement.ArticleName),
                new MySqlParameter("@Quantity", movement.Quantity),
                new MySqlParameter("@MovementType", movement.MovementType),
                new MySqlParameter("@Reason", movement.Reason),
                new MySqlParameter("@UserId", movement.UserId),
                new MySqlParameter("@Reference", movement.Reference),
                new MySqlParameter("@CreatedAt", movement.CreatedAt));

            return affected > 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error recording stock movement for article {ArticleId}", movement.ArticleId);
            return false;
        }
    }

    /// <summary>
    /// The one call site for a manual/reasoned stock change (e.g. from the Inventory
    /// screen's "Adjust stock" panel): changes the real stock level atomically via
    /// ArticleService, then logs why. Sales and voids call ArticleService directly
    /// and log their own movement separately, since they already have the article's
    /// code/name in hand and don't need the extra fetch this convenience wrapper does.
    /// </summary>
    public async Task<bool> AdjustStockWithReasonAsync(int articleId, int quantityDelta, string reason, int userId, string reference = "")
    {
        try
        {
            var article = await _articles.GetByIdAsync(articleId);
            if (article is null) return false;

            if (!await _articles.AdjustStockAsync(articleId, quantityDelta)) return false;

            return await RecordAsync(new StockMovement
            {
                ArticleId = article.Id,
                ArticleCode = article.Code,
                ArticleName = article.Name,
                Quantity = quantityDelta,
                MovementType = quantityDelta >= 0 ? "In" : "Out",
                Reason = reason,
                UserId = userId,
                Reference = reference,
                CreatedAt = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error adjusting stock with reason for article {ArticleId}", articleId);
            return false;
        }
    }

    private static StockMovement MapToMovement(Dictionary<string, object> row)
    {
        return new StockMovement
        {
            Id = Convert.ToInt32(row["Id"]),
            ArticleId = Convert.ToInt32(row["ArticleId"]),
            ArticleCode = row["ArticleCode"]?.ToString() ?? string.Empty,
            ArticleName = row["ArticleName"]?.ToString() ?? string.Empty,
            Quantity = Convert.ToInt32(row["Quantity"]),
            MovementType = row["MovementType"]?.ToString() ?? "Adjustment",
            Reason = row["Reason"]?.ToString() ?? string.Empty,
            UserId = Convert.ToInt32(row["UserId"] ?? 0),
            Reference = row["Reference"]?.ToString() ?? string.Empty,
            CreatedAt = Convert.ToDateTime(row["CreatedAt"])
        };
    }
}
