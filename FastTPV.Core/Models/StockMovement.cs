namespace FastTPV.Core.Models;

/// <summary>
/// An audit-trail entry for any change to an article's stock level — sales
/// (MovementType "Out"), voided-sale restocks and manual adjustments ("In" or "Out"
/// depending on sign). Written alongside every real stock change made through
/// ArticleService.AdjustStockAsync so there's a "why did the count change" history.
/// </summary>
public class StockMovement
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public string ArticleCode { get; set; } = string.Empty;
    public string ArticleName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string MovementType { get; set; } = "Adjustment";
    public string Reason { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
