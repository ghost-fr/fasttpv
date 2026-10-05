namespace FastTPV.Core.Models;

/// <summary>
/// Represents a product/article in the inventory
/// </summary>
public class Article
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal CostPrice { get; set; }
    public int StockLevel { get; set; }
    public int MinimumStock { get; set; }
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Fixed = catalogue price is used as-is on the POS.
    /// OpenPrice = cashier enters the amount on the keypad at sale time (fruit, bulk, services...).
    /// </summary>
    public string PricingMode { get; set; } = "Fixed";

    public bool IsActive { get; set; } = true;

    /// <summary>Shown as a large "quick key" on the sales screen.</summary>
    public bool IsFavorite { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsLowStock => StockLevel <= MinimumStock;
}
