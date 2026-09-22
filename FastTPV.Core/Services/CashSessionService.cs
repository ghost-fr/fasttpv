using FastTPV.Core.Data;
using FastTPV.Core.Models;
using MySql.Data.MySqlClient;
using Serilog;

namespace FastTPV.Core.Services;

/// <summary>
/// Service for managing cash-drawer (till) sessions and their cash movements.
/// </summary>
public class CashSessionService
{
    private readonly DatabaseContext _db;
    private static readonly ILogger Logger = Log.ForContext<CashSessionService>();

    public CashSessionService(DatabaseContext db)
    {
        _db = db;
    }

    public async Task<CashSession?> GetOpenSessionAsync(int userId)
    {
        try
        {
            var rows = await _db.ExecuteQueryAsync(
                "SELECT * FROM CashSessions WHERE UserId = @UserId AND Status = 'Open' ORDER BY OpenedAt DESC LIMIT 1",
                new MySqlParameter("@UserId", userId));

            return rows.Count == 0 ? null : MapToSession(rows[0]);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting open cash session for user {UserId}", userId);
            return null;
        }
    }

    public async Task<int> OpenAsync(int userId, decimal openingFloat)
    {
        try
        {
            var newId = await _db.ExecuteInsertAsync(
                "INSERT INTO CashSessions (UserId, OpenedAt, OpeningFloat, Status) VALUES (@UserId, @OpenedAt, @Float, 'Open')",
                new MySqlParameter("@UserId", userId),
                new MySqlParameter("@OpenedAt", DateTime.Now),
                new MySqlParameter("@Float", openingFloat));

            Logger.Information("Cash session opened for user {UserId} with float {Float:C}", userId, openingFloat);
            return (int)newId;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error opening cash session for user {UserId}", userId);
            return 0;
        }
    }

    public async Task<bool> AddMovementAsync(int sessionId, string type, decimal amount, string reason)
    {
        try
        {
            var newId = await _db.ExecuteInsertAsync(
                "INSERT INTO CashMovements (CashSessionId, Type, Amount, Reason, CreatedAt) " +
                "VALUES (@SessionId, @Type, @Amount, @Reason, @CreatedAt)",
                new MySqlParameter("@SessionId", sessionId),
                new MySqlParameter("@Type", type),
                new MySqlParameter("@Amount", amount),
                new MySqlParameter("@Reason", reason),
                new MySqlParameter("@CreatedAt", DateTime.Now));

            return newId > 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error adding cash movement to session {SessionId}", sessionId);
            return false;
        }
    }

    public async Task<List<CashMovement>> GetMovementsAsync(int sessionId)
    {
        try
        {
            var rows = await _db.ExecuteQueryAsync(
                "SELECT * FROM CashMovements WHERE CashSessionId = @SessionId ORDER BY CreatedAt",
                new MySqlParameter("@SessionId", sessionId));

            return rows.Select(MapToMovement).ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting cash movements for session {SessionId}", sessionId);
            return new List<CashMovement>();
        }
    }

    public async Task<bool> CloseAsync(int sessionId, decimal countedCash, decimal expectedCash)
    {
        try
        {
            var variance = countedCash - expectedCash;

            var result = await _db.ExecuteNonQueryAsync(
                "UPDATE CashSessions SET ClosedAt = @ClosedAt, CountedCash = @CountedCash, " +
                "ExpectedCash = @ExpectedCash, Variance = @Variance, Status = 'Closed' WHERE Id = @Id",
                new MySqlParameter("@ClosedAt", DateTime.Now),
                new MySqlParameter("@CountedCash", countedCash),
                new MySqlParameter("@ExpectedCash", expectedCash),
                new MySqlParameter("@Variance", variance),
                new MySqlParameter("@Id", sessionId));

            Logger.Information("Cash session {SessionId} closed. Expected {Expected:C}, counted {Counted:C}, variance {Variance:C}",
                sessionId, expectedCash, countedCash, variance);
            return result > 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error closing cash session {SessionId}", sessionId);
            return false;
        }
    }

    private static CashSession MapToSession(Dictionary<string, object> row)
    {
        return new CashSession
        {
            Id = Convert.ToInt32(row["Id"]),
            UserId = Convert.ToInt32(row["UserId"]),
            OpenedAt = Convert.ToDateTime(row["OpenedAt"]),
            OpeningFloat = Convert.ToDecimal(row["OpeningFloat"]),
            ClosedAt = row["ClosedAt"] == null ? null : Convert.ToDateTime(row["ClosedAt"]),
            CountedCash = row["CountedCash"] == null ? null : Convert.ToDecimal(row["CountedCash"]),
            ExpectedCash = row["ExpectedCash"] == null ? null : Convert.ToDecimal(row["ExpectedCash"]),
            Variance = row["Variance"] == null ? null : Convert.ToDecimal(row["Variance"]),
            Status = row["Status"]?.ToString() ?? "Open",
            Notes = row["Notes"]?.ToString() ?? string.Empty
        };
    }

    private static CashMovement MapToMovement(Dictionary<string, object> row)
    {
        return new CashMovement
        {
            Id = Convert.ToInt32(row["Id"]),
            CashSessionId = Convert.ToInt32(row["CashSessionId"]),
            Type = row["Type"]?.ToString() ?? "In",
            Amount = Convert.ToDecimal(row["Amount"]),
            Reason = row["Reason"]?.ToString() ?? string.Empty,
            CreatedAt = Convert.ToDateTime(row["CreatedAt"])
        };
    }
}
