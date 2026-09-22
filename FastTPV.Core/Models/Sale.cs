namespace FastTPV.Core.Models;

/// <summary>
/// Represents a sales transaction/ticket
/// </summary>
public class Sale
{
    public int Id { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public DateTime SaleDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Tax { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Absolute money amount knocked off the ticket as a whole, applied before tax
    /// (in addition to any per-line SaleLineItem.Discount amounts, which are already
    /// reflected in SubTotal). Stored as an amount rather than a percent so historical
    /// tickets remain accurate even if prices change later.
    /// </summary>
    public decimal TicketDiscountAmount { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed"; // Completed, Cancelled, Pending
    public string Notes { get; set; } = string.Empty;
    public int SalesmanId { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<SaleLineItem> LineItems { get; set; } = new();
}

/// <summary>
/// Represents a line item in a sale
/// </summary>
public class SaleLineItem
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int ArticleId { get; set; }
    public string ArticleName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    /// <summary>Absolute money amount knocked off this line (not a percent).</summary>
    public decimal Discount { get; set; }
}
