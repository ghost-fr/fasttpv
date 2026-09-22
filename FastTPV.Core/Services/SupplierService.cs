using FastTPV.Core.Data;
using FastTPV.Core.Models;
using MySql.Data.MySqlClient;
using Serilog;

namespace FastTPV.Core.Services;

public sealed class SupplierService
{
    private readonly DatabaseContext _db;
    private static readonly ILogger Logger = Log.ForContext<SupplierService>();

    public SupplierService(DatabaseContext db) => _db = db;

    public async Task<List<Supplier>> GetAllAsync()
    {
        try
        {
            var rows = await _db.ExecuteQueryAsync("SELECT * FROM Suppliers WHERE IsActive = 1 ORDER BY Name");
            return rows.Select(MapToSupplier).ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading suppliers");
            return new List<Supplier>();
        }
    }

    public async Task<int> AddAsync(Supplier supplier)
    {
        try
        {
            var query = @"INSERT INTO Suppliers (Code, Name, ContactName, Email, Phone, TaxId, Address, IsActive, CreatedAt, UpdatedAt)
                VALUES (@Code, @Name, @ContactName, @Email, @Phone, @TaxId, @Address, @IsActive, @CreatedAt, @UpdatedAt)";

            var newId = await _db.ExecuteInsertAsync(query,
                new MySqlParameter("@Code", supplier.Code),
                new MySqlParameter("@Name", supplier.Name),
                new MySqlParameter("@ContactName", supplier.ContactName),
                new MySqlParameter("@Email", supplier.Email),
                new MySqlParameter("@Phone", supplier.Phone),
                new MySqlParameter("@TaxId", supplier.TaxId),
                new MySqlParameter("@Address", supplier.Address),
                new MySqlParameter("@IsActive", supplier.IsActive),
                new MySqlParameter("@CreatedAt", DateTime.Now),
                new MySqlParameter("@UpdatedAt", DateTime.Now));

            supplier.Id = (int)newId;
            return supplier.Id;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error adding supplier {Name}", supplier.Name);
            return 0;
        }
    }

    public async Task<bool> UpdateAsync(Supplier supplier)
    {
        try
        {
            var query = @"UPDATE Suppliers SET Code = @Code, Name = @Name, ContactName = @ContactName, Email = @Email, Phone = @Phone,
                TaxId = @TaxId, Address = @Address, IsActive = @IsActive, UpdatedAt = @UpdatedAt WHERE Id = @Id";

            var affected = await _db.ExecuteNonQueryAsync(query,
                new MySqlParameter("@Code", supplier.Code),
                new MySqlParameter("@Name", supplier.Name),
                new MySqlParameter("@ContactName", supplier.ContactName),
                new MySqlParameter("@Email", supplier.Email),
                new MySqlParameter("@Phone", supplier.Phone),
                new MySqlParameter("@TaxId", supplier.TaxId),
                new MySqlParameter("@Address", supplier.Address),
                new MySqlParameter("@IsActive", supplier.IsActive),
                new MySqlParameter("@UpdatedAt", DateTime.Now),
                new MySqlParameter("@Id", supplier.Id));

            return affected > 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating supplier {Id}", supplier.Id);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            var affected = await _db.ExecuteNonQueryAsync(
                "UPDATE Suppliers SET IsActive = 0 WHERE Id = @Id", new MySqlParameter("@Id", id));
            return affected > 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deleting supplier {Id}", id);
            return false;
        }
    }

    private static Supplier MapToSupplier(Dictionary<string, object> row)
    {
        return new Supplier
        {
            Id = Convert.ToInt32(row["Id"]),
            Code = row["Code"]?.ToString() ?? string.Empty,
            Name = row["Name"]?.ToString() ?? string.Empty,
            ContactName = row["ContactName"]?.ToString() ?? string.Empty,
            Email = row["Email"]?.ToString() ?? string.Empty,
            Phone = row["Phone"]?.ToString() ?? string.Empty,
            TaxId = row["TaxId"]?.ToString() ?? string.Empty,
            Address = row["Address"]?.ToString() ?? string.Empty,
            IsActive = Convert.ToBoolean(row["IsActive"]),
            CreatedAt = Convert.ToDateTime(row["CreatedAt"]),
            UpdatedAt = Convert.ToDateTime(row["UpdatedAt"])
        };
    }
}
