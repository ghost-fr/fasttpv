namespace FastTPV.Core.Models;

/// <summary>
/// One tender line against a Sale. A single-method sale (the common case) still gets
/// exactly one Payment row for a consistent record; a split-tender sale (e.g. part
/// cash, part card) gets one row per method.
/// </summary>
public class Payment
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public string Method { get; set; } = "Cash";
    public decimal Amount { get; set; }
    public decimal Change { get; set; }
    public string Reference { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
