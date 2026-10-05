namespace FastTPV.Core.Models;

/// <summary>One line of a parked ticket. Prices are kept exactly as the cashier left them (open prices, discounts).</summary>
public sealed class HeldLine
{
    public int ArticleId { get; set; }
    public string ArticleName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal DiscountAmount { get; set; }
}

/// <summary>A ticket parked ("held") so the cashier can serve someone else, recalled later from any terminal.</summary>
public sealed class HeldSale
{
    public int Id { get; set; }
    /// <summary>Short text the cashier sees in the list (defaults to time + item count).</summary>
    public string Label { get; set; } = string.Empty;
    public int UserId { get; set; }
    public int CustomerId { get; set; }
    public decimal TicketDiscountAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "Held"; // Held, Recalled, Discarded
    public List<HeldLine> Lines { get; set; } = new();

    public int ItemCount => Lines.Sum(l => l.Quantity);
}
