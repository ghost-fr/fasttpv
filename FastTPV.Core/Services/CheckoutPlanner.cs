using FastTPV.Core.Models;

namespace FastTPV.Core.Services;

/// <summary>
/// Pure (database-free) validation of a ticket and its tender, so the rules can be unit-tested
/// and so the cashier gets a precise message before any lock is taken.
/// </summary>
public static class CheckoutPlanner
{
    public sealed record Plan(string? Error, CheckoutStatus ErrorStatus, decimal Change, IReadOnlyList<PaymentLine> Payments, string PaymentMethod)
    {
        public bool IsValid => Error is null;
    }

    private static Plan Fail(CheckoutStatus status, string message) =>
        new(message, status, 0m, Array.Empty<PaymentLine>(), string.Empty);

    /// <summary>
    /// Rules: at least one line; positive whole quantities; no negative prices/discounts; a discount
    /// never exceeds its line; each line total equals price x qty - discount; payments cover the total;
    /// change is only given from cash and never exceeds the cash tendered.
    /// A zero-total ticket (e.g. 100% discount) needs no payment.
    /// </summary>
    public static Plan Validate(Sale sale, IReadOnlyList<PaymentLine> payments)
    {
        if (sale.LineItems.Count == 0) return Fail(CheckoutStatus.Invalid, "The ticket has no lines.");

        foreach (var l in sale.LineItems)
        {
            var name = string.IsNullOrWhiteSpace(l.ArticleName) ? $"article {l.ArticleId}" : l.ArticleName;
            if (l.Quantity <= 0) return Fail(CheckoutStatus.Invalid, $"{name}: quantity must be at least 1.");
            if (l.UnitPrice < 0) return Fail(CheckoutStatus.Invalid, $"{name}: price cannot be negative.");
            if (l.Discount < 0) return Fail(CheckoutStatus.Invalid, $"{name}: discount cannot be negative.");
            var gross = Math.Round(l.UnitPrice * l.Quantity, 2);
            if (l.Discount > gross) return Fail(CheckoutStatus.Invalid, $"{name}: discount is larger than the line amount.");
            if (Math.Abs(l.LineTotal - (gross - l.Discount)) > 0.01m)
                return Fail(CheckoutStatus.Invalid, $"{name}: line total does not match price x quantity - discount.");
        }

        if (sale.TotalAmount < 0) return Fail(CheckoutStatus.Invalid, "The ticket total cannot be negative.");
        if (sale.TicketDiscountAmount < 0) return Fail(CheckoutStatus.Invalid, "The ticket discount cannot be negative.");

        // Normalize tender: trim names, merge duplicates of the same method, drop zero lines.
        var merged = payments
            .Where(p => p.Amount != 0)
            .GroupBy(p => (p.Method ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new PaymentLine(g.Key, Math.Round(g.Sum(p => p.Amount), 2)))
            .ToList();

        if (merged.Any(p => p.Method.Length == 0)) return Fail(CheckoutStatus.PaymentInvalid, "A payment has no method.");
        if (merged.Any(p => p.Amount < 0)) return Fail(CheckoutStatus.PaymentInvalid, "A payment amount cannot be negative.");

        var total = Math.Round(sale.TotalAmount, 2);
        var tendered = merged.Sum(p => p.Amount);

        if (total == 0m)
            return new Plan(null, CheckoutStatus.Completed, tendered, merged, merged.Count == 0 ? "None" : MethodName(merged));

        if (tendered < total)
            return Fail(CheckoutStatus.PaymentInvalid, $"Tendered {tendered:0.00} is less than the total {total:0.00}.");

        var change = tendered - total;
        if (change > 0m)
        {
            var cash = merged.Where(p => p.Method.Equals("Cash", StringComparison.OrdinalIgnoreCase)).Sum(p => p.Amount);
            if (change > cash)
                return Fail(CheckoutStatus.PaymentInvalid,
                    $"Change of {change:0.00} cannot be given: only {cash:0.00} was tendered in cash. Reduce the card amount.");
        }

        return new Plan(null, CheckoutStatus.Completed, change, merged, MethodName(merged));
    }

    private static string MethodName(IReadOnlyList<PaymentLine> p) => p.Count == 1 ? p[0].Method : "Mixed";
}
