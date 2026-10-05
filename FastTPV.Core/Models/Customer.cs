namespace FastTPV.Core.Models;

/// <summary>
/// Represents a customer
/// </summary>
public class Customer
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public decimal CurrentDebt { get; set; }

    /// <summary>Optional loyalty module: points earned on completed sales (0 when the module is off).</summary>
    public int LoyaltyPoints { get; set; }

    /// <summary>Credit still available on account (never negative). 0 when no credit limit is set.</summary>
    public decimal AvailableCredit => Math.Max(0m, CreditLimit - CurrentDebt);
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
