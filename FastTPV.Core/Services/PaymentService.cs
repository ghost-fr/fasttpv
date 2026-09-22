using FastTPV.Core.Data;
using FastTPV.Core.Models;
using MySql.Data.MySqlClient;
using Serilog;

namespace FastTPV.Core.Services;

/// <summary>
/// Persists the tender line(s) against a sale — one row for a normal single-method
/// sale, multiple rows for a split/mixed-tender sale (part cash, part card).
/// </summary>
public sealed class PaymentService
{
    private readonly DatabaseContext _db;
    private static readonly ILogger Logger = Log.ForContext<PaymentService>();

    public PaymentService(DatabaseContext db) => _db = db;

    public async Task<List<Payment>> GetBySaleAsync(int saleId)
    {
        try
        {
            var rows = await _db.ExecuteQueryAsync(
                "SELECT * FROM Payments WHERE SaleId = @SaleId ORDER BY CreatedAt ASC",
                new MySqlParameter("@SaleId", saleId));
            return rows.Select(MapToPayment).ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading payments for sale {SaleId}", saleId);
            return new List<Payment>();
        }
    }

    public async Task<int> AddAsync(Payment payment)
    {
        try
        {
            // Change is a reserved word in MySQL — must be backtick-quoted.
            var query = @"INSERT INTO Payments (SaleId, Method, Amount, `Change`, Reference, CreatedAt)
                VALUES (@SaleId, @Method, @Amount, @Change, @Reference, @CreatedAt)";

            var newId = await _db.ExecuteInsertAsync(query,
                new MySqlParameter("@SaleId", payment.SaleId),
                new MySqlParameter("@Method", payment.Method),
                new MySqlParameter("@Amount", payment.Amount),
                new MySqlParameter("@Change", payment.Change),
                new MySqlParameter("@Reference", payment.Reference),
                new MySqlParameter("@CreatedAt", payment.CreatedAt));

            payment.Id = (int)newId;
            return payment.Id;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error adding payment for sale {SaleId}", payment.SaleId);
            return 0;
        }
    }

    /// <summary>
    /// Records one Payment row per tender method and returns the change due (any
    /// excess beyond the sale total, conventionally handed back in cash). Does not
    /// validate that the total tendered covers the sale — the caller (checkout UI)
    /// is expected to have already confirmed that before calling this, since it's
    /// in a better position to show the shortfall to the cashier interactively.
    /// </summary>
    public async Task<decimal> RecordPaymentsAsync(int saleId, decimal saleTotal, IDictionary<string, decimal> tenderedByMethod)
    {
        var totalTendered = tenderedByMethod.Values.Sum();
        var change = Math.Max(0m, totalTendered - saleTotal);
        var changeAssigned = false;

        foreach (var (method, amount) in tenderedByMethod)
        {
            if (amount <= 0) continue;

            await AddAsync(new Payment
            {
                SaleId = saleId,
                Method = method,
                Amount = amount,
                // Change is conventionally given back in cash and against whichever
                // line the cashier tendered last; recorded once, on the first
                // nonzero line, so the sum of Payment.Amount - Change still reconciles.
                Change = !changeAssigned && change > 0 ? change : 0,
                Reference = method,
                CreatedAt = DateTime.Now
            });

            if (!changeAssigned && change > 0) changeAssigned = true;
        }

        return change;
    }

    private static Payment MapToPayment(Dictionary<string, object> row)
    {
        return new Payment
        {
            Id = Convert.ToInt32(row["Id"]),
            SaleId = Convert.ToInt32(row["SaleId"]),
            Method = row["Method"]?.ToString() ?? "Cash",
            Amount = Convert.ToDecimal(row["Amount"] ?? 0m),
            Change = Convert.ToDecimal(row["Change"] ?? 0m),
            Reference = row["Reference"]?.ToString() ?? string.Empty,
            CreatedAt = Convert.ToDateTime(row["CreatedAt"])
        };
    }
}
