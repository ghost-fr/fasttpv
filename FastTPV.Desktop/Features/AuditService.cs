using FastTPV.Core.Data;
using MySql.Data.MySqlClient;

namespace FastTPV.Desktop.Features;

public sealed class AuditService
{
    private readonly DatabaseContext _db;
    public AuditService(DatabaseContext db) => _db = db;

    public Task WriteAsync(UserSession? user, string action, string entity, string details = "")
        => _db.ExecuteNonQueryAsync(
            "INSERT INTO AuditLog (UserId, Action, Entity, Details, CreatedAt) VALUES (@UserId, @Action, @Entity, @Details, @CreatedAt)",
            new MySqlParameter("@UserId", user?.UserId ?? 0),
            new MySqlParameter("@Action", action),
            new MySqlParameter("@Entity", entity),
            new MySqlParameter("@Details", details),
            new MySqlParameter("@CreatedAt", DateTime.UtcNow));
}
