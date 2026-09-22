using System.Security.Cryptography;
using FastTPV.Core.Data;
using MySql.Data.MySqlClient;

namespace FastTPV.Desktop.Features;

public sealed record UserSession(int UserId, string UserName, string DisplayName, string Role);

/// <summary>
/// Read-only view of a user row for listing in UsersWindow — deliberately excludes
/// the password hash/salt, which never need to leave UserAccountService.
/// </summary>
public sealed record UserAccount(int Id, string UserName, string DisplayName, string Role, bool IsActive);

public static class Roles
{
    public const string Admin = "Admin";
    public const string Cashier = "Cashier";
}

public sealed class UserAccountService
{
    private readonly DatabaseContext _db;
    public UserAccountService(DatabaseContext db) => _db = db;

    /// <summary>
    /// Verifies credentials, enforcing a temporary lockout after too many wrong
    /// passwords in a row (RuntimeSettings.MaxFailedLoginAttempts /
    /// LockoutMinutes). A wrong password increments the counter and, on hitting
    /// the threshold, sets LockedUntil; a correct one resets both. While locked,
    /// this returns null without even checking the password, same as any other
    /// failure — call GetLockoutRemainingAsync separately if the caller wants to
    /// show a more specific "try again in N minutes" message.
    /// </summary>
    public async Task<UserSession?> AuthenticateAsync(string userName, string password)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password)) return null;

        const string sql = "SELECT Id, UserName, DisplayName, Role, PasswordHash, PasswordSalt, IsActive, " +
            "FailedLoginAttempts, LockedUntil FROM Users WHERE UserName = @UserName LIMIT 1";
        var rows = await _db.ExecuteQueryAsync(sql, new MySqlParameter("@UserName", userName.Trim()));

        if (rows.Count == 0 || !Convert.ToBoolean(rows[0]["IsActive"])) return null;

        var userId = Convert.ToInt32(rows[0]["Id"]);
        var lockedUntil = rows[0]["LockedUntil"] is DBNull ? (DateTime?)null : Convert.ToDateTime(rows[0]["LockedUntil"]);
        if (lockedUntil.HasValue && lockedUntil.Value > DateTime.UtcNow)
        {
            return null;
        }

        if (!VerifyPassword(password, rows[0]["PasswordHash"].ToString()!, rows[0]["PasswordSalt"].ToString()!))
        {
            await RecordFailedAttemptAsync(userId, Convert.ToInt32(rows[0]["FailedLoginAttempts"]));
            return null;
        }

        await ResetLoginAttemptsAsync(userId);

        return new UserSession(
            userId,
            rows[0]["UserName"].ToString()!,
            rows[0]["DisplayName"].ToString()!,
            rows[0]["Role"].ToString() ?? Roles.Cashier);
    }

    /// <summary>
    /// For LoginWindow to check after a null AuthenticateAsync result, to show a
    /// specific "locked for N more minutes" message instead of the generic
    /// invalid-credentials one. Returns null if the account isn't currently locked
    /// (wrong username, inactive account, or just a wrong password that hasn't hit
    /// the threshold yet).
    /// </summary>
    public async Task<TimeSpan?> GetLockoutRemainingAsync(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return null;

        var rows = await _db.ExecuteQueryAsync(
            "SELECT LockedUntil FROM Users WHERE UserName = @UserName LIMIT 1",
            new MySqlParameter("@UserName", userName.Trim()));

        if (rows.Count == 0 || rows[0]["LockedUntil"] is DBNull) return null;

        var lockedUntil = Convert.ToDateTime(rows[0]["LockedUntil"]);
        var remaining = lockedUntil - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    private async Task RecordFailedAttemptAsync(int userId, int currentFailedAttempts)
    {
        var maxAttempts = AppRuntime.Settings.MaxFailedLoginAttempts;
        var newCount = currentFailedAttempts + 1;

        if (newCount >= maxAttempts)
        {
            var lockedUntil = DateTime.UtcNow.AddMinutes(AppRuntime.Settings.LockoutMinutes);
            await _db.ExecuteNonQueryAsync(
                "UPDATE Users SET FailedLoginAttempts = 0, LockedUntil = @LockedUntil WHERE Id = @Id",
                new MySqlParameter("@LockedUntil", lockedUntil), new MySqlParameter("@Id", userId));
        }
        else
        {
            await _db.ExecuteNonQueryAsync(
                "UPDATE Users SET FailedLoginAttempts = @Count WHERE Id = @Id",
                new MySqlParameter("@Count", newCount), new MySqlParameter("@Id", userId));
        }
    }

    private Task<int> ResetLoginAttemptsAsync(int userId) =>
        _db.ExecuteNonQueryAsync(
            "UPDATE Users SET FailedLoginAttempts = 0, LockedUntil = NULL WHERE Id = @Id",
            new MySqlParameter("@Id", userId));

    public async Task<List<UserAccount>> GetAllAsync()
    {
        var rows = await _db.ExecuteQueryAsync(
            "SELECT Id, UserName, DisplayName, Role, IsActive FROM Users ORDER BY UserName");

        return rows.Select(r => new UserAccount(
            Convert.ToInt32(r["Id"]),
            r["UserName"].ToString()!,
            r["DisplayName"].ToString()!,
            r["Role"].ToString() ?? Roles.Cashier,
            Convert.ToBoolean(r["IsActive"]))).ToList();
    }

    public async Task<bool> AddAsync(string userName, string displayName, string password, string role)
    {
        var (hash, salt) = HashPassword(password);
        var newId = await _db.ExecuteInsertAsync(
            "INSERT INTO Users (UserName, DisplayName, PasswordHash, PasswordSalt, Role, IsActive, CreatedAt) " +
            "VALUES (@UserName, @DisplayName, @Hash, @Salt, @Role, 1, @CreatedAt)",
            new MySqlParameter("@UserName", userName.Trim()),
            new MySqlParameter("@DisplayName", displayName.Trim()),
            new MySqlParameter("@Hash", hash),
            new MySqlParameter("@Salt", salt),
            new MySqlParameter("@Role", role),
            new MySqlParameter("@CreatedAt", DateTime.UtcNow));

        return newId > 0;
    }

    public Task<int> SetActiveAsync(int userId, bool isActive) =>
        _db.ExecuteNonQueryAsync("UPDATE Users SET IsActive = @IsActive WHERE Id = @Id",
            new MySqlParameter("@IsActive", isActive), new MySqlParameter("@Id", userId));

    public Task<int> SetRoleAsync(int userId, string role) =>
        _db.ExecuteNonQueryAsync("UPDATE Users SET Role = @Role WHERE Id = @Id",
            new MySqlParameter("@Role", role), new MySqlParameter("@Id", userId));

    /// <summary>
    /// Admin-initiated reset — no current-password check. Also clears any active
    /// lockout, so an Admin resetting a locked-out user's password immediately
    /// un-blocks them too, rather than leaving LockedUntil in the future.
    /// </summary>
    public Task<int> SetPasswordAsync(int userId, string newPassword)
    {
        var (hash, salt) = HashPassword(newPassword);
        return _db.ExecuteNonQueryAsync(
            "UPDATE Users SET PasswordHash = @Hash, PasswordSalt = @Salt, FailedLoginAttempts = 0, LockedUntil = NULL WHERE Id = @Id",
            new MySqlParameter("@Hash", hash), new MySqlParameter("@Salt", salt), new MySqlParameter("@Id", userId));
    }

    /// <summary>Self-service change — requires proving the current password first.</summary>
    public async Task<bool> ChangeOwnPasswordAsync(int userId, string currentPassword, string newPassword)
    {
        var rows = await _db.ExecuteQueryAsync(
            "SELECT PasswordHash, PasswordSalt FROM Users WHERE Id = @Id LIMIT 1",
            new MySqlParameter("@Id", userId));

        if (rows.Count == 0) return false;
        if (!VerifyPassword(currentPassword, rows[0]["PasswordHash"].ToString()!, rows[0]["PasswordSalt"].ToString()!))
            return false;

        await SetPasswordAsync(userId, newPassword);
        return true;
    }

    public static bool IsValidPassword(string password, int minimumLength)
        => password.Length >= minimumLength && password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit);

    public static (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120_000, HashAlgorithmName.SHA512, 64);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    private static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        var salt = Convert.FromBase64String(storedSalt);
        var expected = Convert.FromBase64String(storedHash);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120_000, HashAlgorithmName.SHA512, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
