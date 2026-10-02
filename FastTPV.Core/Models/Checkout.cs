namespace FastTPV.Core.Models;

/// <summary>One tender line of a sale (e.g. Cash 20.00, then Card 15.50).</summary>
public sealed record PaymentLine(string Method, decimal Amount);

/// <summary>One article that cannot be sold in the requested quantity.</summary>
public sealed record StockShortage(int ArticleId, string ArticleName, int Requested, int Available);

public enum CheckoutStatus
{
    /// <summary>Sale, lines, stock, movements and payments were all committed.</summary>
    Completed,
    /// <summary>This RequestId was already committed earlier (retry/double-click/network blip). Nothing was written again.</summary>
    AlreadyCompleted,
    /// <summary>The cashier has no open cash session; open one before selling.</summary>
    NoOpenSession,
    /// <summary>At least one article lacks stock. Nothing was written. Retry with AllowNegativeStock after manager approval.</summary>
    InsufficientStock,
    /// <summary>Tendered amount does not cover the total, or change cannot be given from the cash tendered.</summary>
    PaymentInvalid,
    /// <summary>The ticket itself is invalid (empty, zero open price, inactive article...). See Message.</summary>
    Invalid,
    /// <summary>Unexpected database failure. Nothing was written. Safe to retry with the same RequestId.</summary>
    Failed
}

/// <summary>Everything needed to finalize a sale atomically.</summary>
public sealed class CheckoutRequest
{
    /// <summary>
    /// Idempotency key: generate ONE per ticket attempt and reuse it on retries. If a previous attempt
    /// actually committed, the retry returns that sale instead of creating a second ticket.
    /// </summary>
    public Guid RequestId { get; init; } = Guid.NewGuid();

    /// <summary>The ticket. TicketNumber/Id are assigned by the checkout; LineItems must be filled.</summary>
    public Sale Sale { get; init; } = new();

    public IReadOnlyList<PaymentLine> Payments { get; init; } = Array.Empty<PaymentLine>();

    /// <summary>The signed-in cashier. Their open cash session receives the sale.</summary>
    public int UserId { get; init; }

    /// <summary>Set ONLY after a manager approved selling below zero stock.</summary>
    public bool AllowNegativeStock { get; init; }
}

public sealed class CheckoutResult
{
    public CheckoutStatus Status { get; init; }
    public Sale? Sale { get; init; }
    public decimal Change { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<StockShortage> Shortages { get; init; } = Array.Empty<StockShortage>();

    /// <summary>True when a ticket exists for this request (new or previously committed).</summary>
    public bool Succeeded => Status is CheckoutStatus.Completed or CheckoutStatus.AlreadyCompleted;
}
