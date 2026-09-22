using FastTPV.Core.Models;
using FastTPV.Core.Services;

namespace FastTPV.Desktop.Features;

/// <summary>
/// Thin read-only facade over ArticleService/SaleService for the Reports screen.
/// Nothing here is new business logic — it just gives ReportsWindow a single,
/// narrowly-scoped dependency instead of reaching into the full sale/article services.
/// </summary>
public sealed class ReportsService
{
    private readonly SaleService _sales;
    private readonly ArticleService _articles;

    public ReportsService(SaleService sales, ArticleService articles)
    {
        _sales = sales;
        _articles = articles;
    }

    public Task<List<Sale>> GetSalesAsync(DateTime from, DateTime to) => _sales.GetByDateRangeAsync(from, to);

    public Task<List<Article>> GetLowStockAsync() => _articles.GetLowStockAsync();

    /// <summary>
    /// Aggregate sales/tax/discount totals for a date range — the numbers an X-Report
    /// (mid-shift, non-resetting check) or a Z-Report (end-of-day close-out) needs.
    /// Cancelled sales are excluded, same as the totals ReportsWindow already showed
    /// before this method existed (Status != "Cancelled").
    /// </summary>
    public async Task<SalesSummary> GetSalesSummaryAsync(DateTime from, DateTime to)
    {
        var sales = (await _sales.GetByDateRangeAsync(from, to))
            .Where(s => s.Status != "Cancelled")
            .ToList();

        return new SalesSummary
        {
            From = from,
            To = to,
            TransactionCount = sales.Count,
            GrossSubtotal = sales.Sum(s => s.SubTotal),
            TotalDiscount = sales.Sum(s => s.TicketDiscountAmount) + sales.Sum(s => s.LineItems.Sum(li => li.Discount)),
            TotalTax = sales.Sum(s => s.Tax),
            NetTotal = sales.Sum(s => s.TotalAmount)
        };
    }

    /// <summary>
    /// How the total for a date range breaks down by tender type (Cash / Card / Digital /
    /// Mixed) — the other half of an X/Z-Report, alongside GetSalesSummaryAsync, and
    /// useful on its own for reconciling the cash drawer against Cash-tendered sales.
    /// </summary>
    public async Task<List<PaymentMethodTotal>> GetPaymentMethodBreakdownAsync(DateTime from, DateTime to)
    {
        var sales = (await _sales.GetByDateRangeAsync(from, to))
            .Where(s => s.Status != "Cancelled")
            .ToList();

        return sales
            .GroupBy(s => string.IsNullOrWhiteSpace(s.PaymentMethod) ? "Unknown" : s.PaymentMethod)
            .Select(g => new PaymentMethodTotal(g.Key, g.Sum(s => s.TotalAmount), g.Count()))
            .OrderByDescending(t => t.Amount)
            .ToList();
    }
}

/// <summary>Aggregate totals for a date range — see ReportsService.GetSalesSummaryAsync.</summary>
public sealed class SalesSummary
{
    public DateTime From { get; init; }
    public DateTime To { get; init; }
    public int TransactionCount { get; init; }
    public decimal GrossSubtotal { get; init; }
    public decimal TotalDiscount { get; init; }
    public decimal TotalTax { get; init; }
    public decimal NetTotal { get; init; }
}

/// <summary>One tender method's share of a date range's totals — see
/// ReportsService.GetPaymentMethodBreakdownAsync.</summary>
public sealed record PaymentMethodTotal(string Method, decimal Amount, int Count);
