namespace FastTPV.Core.Models;

/// <summary>
/// A single cashier's till session: opened with a starting float, optionally
/// adjusted with cash in/out during the shift, and closed by counting the actual
/// cash on hand — the standard "open/close till" pattern used by most POS systems.
/// </summary>
public class CashSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime OpenedAt { get; set; }
    public decimal OpeningFloat { get; set; }
    public DateTime? ClosedAt { get; set; }
    public decimal? CountedCash { get; set; }
    public decimal? ExpectedCash { get; set; }

    /// <summary>CountedCash minus ExpectedCash. Positive = over, negative = short.</summary>
    public decimal? Variance { get; set; }

    public string Status { get; set; } = "Open"; // Open, Closed
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// A manual cash movement during an open session — a paid-out, a banking drop, a
/// till top-up, etc. — that isn't a sale but still affects the cash physically in
/// the drawer, so it has to be logged for the close-out math to be correct.
/// </summary>
public class CashMovement
{
    public int Id { get; set; }
    public int CashSessionId { get; set; }
    public string Type { get; set; } = "In"; // In, Out
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
