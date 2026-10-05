using FastTPV.Core.Models;

namespace FastTPV.Core.Services;

public enum PriceChangeMode
{
    /// <summary>NewPrice = OldPrice x (1 + value/100). Negative value lowers prices.</summary>
    Percent,
    /// <summary>NewPrice = OldPrice + value (currency units). Negative value lowers prices.</summary>
    Amount
}

/// <summary>One article's proposed price change, shown in the preview and then applied.</summary>
public sealed record PriceChange(int ArticleId, string Code, string Name, decimal OldPrice, decimal NewPrice)
{
    public decimal Difference => NewPrice - OldPrice;
}

/// <summary>Result of planning a bulk price change (pure, database-free).</summary>
public sealed class PriceChangePlan
{
    public List<PriceChange> Changes { get; } = new();
    /// <summary>Articles left alone, with the reason (open price, would go below zero, unchanged...).</summary>
    public List<string> Skipped { get; } = new();
    public string? Error { get; set; }
    public bool IsValid => Error is null;
}

public static class PriceChangePlanner
{
    /// <summary>
    /// Computes new prices for <paramref name="articles"/>. Rules: open-price products are skipped (their
    /// price is typed at the till); results are rounded to 2 decimals (away from zero); a result that
    /// would be below 0.00 is skipped rather than clamped (so nothing is silently given away); unchanged
    /// prices are not listed; a change of more than <paramref name="maxPercentSwing"/> percent is
    /// rejected as a probable typo (e.g. 150 instead of 15).
    /// </summary>
    public static PriceChangePlan Plan(IEnumerable<Article> articles, PriceChangeMode mode, decimal value, decimal maxPercentSwing = 100m)
    {
        var plan = new PriceChangePlan();
        if (value == 0m) { plan.Error = "Enter a change other than zero."; return plan; }
        if (mode == PriceChangeMode.Percent && Math.Abs(value) > maxPercentSwing)
        {
            plan.Error = $"A change of {value:0.##}% is larger than the allowed {maxPercentSwing:0}%. Check the value.";
            return plan;
        }

        foreach (var a in articles)
        {
            if (a.IsOpenPrice) { plan.Skipped.Add($"{a.Code} {a.Name}: open-price product"); continue; }

            var raw = mode == PriceChangeMode.Percent ? a.Price * (1m + value / 100m) : a.Price + value;
            var rounded = Math.Round(raw, 2, MidpointRounding.AwayFromZero);

            if (rounded < 0m) { plan.Skipped.Add($"{a.Code} {a.Name}: would become negative"); continue; }
            if (rounded > 99_999_999.99m) { plan.Skipped.Add($"{a.Code} {a.Name}: exceeds the maximum price"); continue; }
            if (rounded == a.Price) continue;

            plan.Changes.Add(new PriceChange(a.Id, a.Code, a.Name, a.Price, rounded));
        }

        if (plan.Changes.Count == 0 && plan.Error is null)
            plan.Error = "No prices would change.";
        return plan;
    }
}
