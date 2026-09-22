using FastTPV.Core.Data;
using FastTPV.Core.Interfaces;
using FastTPV.Core.Models;
using MySql.Data.MySqlClient;
using Serilog;

namespace FastTPV.Core.Services;

/// <summary>
/// Service for managing customers
/// </summary>
public class CustomerService : ICustomerRepository
{
    private readonly DatabaseContext _db;
    private static readonly ILogger Logger = Log.ForContext<CustomerService>();

    public CustomerService(DatabaseContext db)
    {
        _db = db;
    }

    public async Task<Customer?> GetByIdAsync(int id)
    {
        try
        {
            var query = "SELECT * FROM Customers WHERE Id = @Id";
            var results = await _db.ExecuteQueryAsync(query, new MySqlParameter("@Id", id));

            if (results.Count == 0)
                return null;

            return MapToCustomer(results[0]);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting customer by id: {Id}", id);
            return null;
        }
    }

    public async Task<Customer?> GetByCodeAsync(string code)
    {
        try
        {
            var query = "SELECT * FROM Customers WHERE Code = @Code";
            var results = await _db.ExecuteQueryAsync(query, new MySqlParameter("@Code", code));

            if (results.Count == 0)
                return null;

            return MapToCustomer(results[0]);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting customer by code: {Code}", code);
            return null;
        }
    }

    public async Task<List<Customer>> GetAllAsync()
    {
        try
        {
            var query = "SELECT * FROM Customers WHERE IsActive = 1 ORDER BY Name";
            var results = await _db.ExecuteQueryAsync(query);
            return results.Select(MapToCustomer).ToList();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error getting all customers");
            return new List<Customer>();
        }
    }

    public async Task<int> AddAsync(Customer customer)
    {
        try
        {
            var query = @"INSERT INTO Customers (Code, Name, Email, Phone, Address, City, ZipCode,
                TaxId, CreditLimit, CurrentDebt, IsActive, CreatedAt, UpdatedAt)
                VALUES (@Code, @Name, @Email, @Phone, @Address, @City, @ZipCode,
                @TaxId, @CreditLimit, @CurrentDebt, @IsActive, @CreatedAt, @UpdatedAt)";

            var newId = await _db.ExecuteInsertAsync(query,
                new MySqlParameter("@Code", customer.Code),
                new MySqlParameter("@Name", customer.Name),
                new MySqlParameter("@Email", customer.Email),
                new MySqlParameter("@Phone", customer.Phone),
                new MySqlParameter("@Address", customer.Address),
                new MySqlParameter("@City", customer.City),
                new MySqlParameter("@ZipCode", customer.ZipCode),
                new MySqlParameter("@TaxId", customer.TaxId),
                new MySqlParameter("@CreditLimit", customer.CreditLimit),
                new MySqlParameter("@CurrentDebt", customer.CurrentDebt),
                new MySqlParameter("@IsActive", customer.IsActive),
                new MySqlParameter("@CreatedAt", DateTime.Now),
                new MySqlParameter("@UpdatedAt", DateTime.Now));

            customer.Id = (int)newId;
            Logger.Information("Customer added successfully: {CustomerCode} (Id {Id})", customer.Code, customer.Id);
            return customer.Id;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error adding customer");
            return 0;
        }
    }

    public async Task<bool> UpdateAsync(Customer customer)
    {
        try
        {
            var query = @"UPDATE Customers SET Name = @Name, Email = @Email, Phone = @Phone,
                Address = @Address, City = @City, ZipCode = @ZipCode, TaxId = @TaxId,
                CreditLimit = @CreditLimit, CurrentDebt = @CurrentDebt, IsActive = @IsActive,
                UpdatedAt = @UpdatedAt WHERE Id = @Id";

            var result = await _db.ExecuteNonQueryAsync(query,
                new MySqlParameter("@Name", customer.Name),
                new MySqlParameter("@Email", customer.Email),
                new MySqlParameter("@Phone", customer.Phone),
                new MySqlParameter("@Address", customer.Address),
                new MySqlParameter("@City", customer.City),
                new MySqlParameter("@ZipCode", customer.ZipCode),
                new MySqlParameter("@TaxId", customer.TaxId),
                new MySqlParameter("@CreditLimit", customer.CreditLimit),
                new MySqlParameter("@CurrentDebt", customer.CurrentDebt),
                new MySqlParameter("@IsActive", customer.IsActive),
                new MySqlParameter("@UpdatedAt", DateTime.Now),
                new MySqlParameter("@Id", customer.Id));

            Logger.Information("Customer updated successfully: {CustomerId}", customer.Id);
            return result > 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error updating customer: {CustomerId}", customer.Id);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            var query = "UPDATE Customers SET IsActive = 0 WHERE Id = @Id";
            var result = await _db.ExecuteNonQueryAsync(query, new MySqlParameter("@Id", id));
            Logger.Information("Customer deleted (soft delete): {CustomerId}", id);
            return result > 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error deleting customer: {CustomerId}", id);
            return false;
        }
    }

    private static Customer MapToCustomer(Dictionary<string, object> row)
    {
        return new Customer
        {
            Id = Convert.ToInt32(row["Id"]),
            Code = row["Code"]?.ToString() ?? string.Empty,
            Name = row["Name"]?.ToString() ?? string.Empty,
            Email = row["Email"]?.ToString() ?? string.Empty,
            Phone = row["Phone"]?.ToString() ?? string.Empty,
            Address = row["Address"]?.ToString() ?? string.Empty,
            City = row["City"]?.ToString() ?? string.Empty,
            ZipCode = row["ZipCode"]?.ToString() ?? string.Empty,
            TaxId = row["TaxId"]?.ToString() ?? string.Empty,
            CreditLimit = Convert.ToDecimal(row["CreditLimit"]),
            CurrentDebt = Convert.ToDecimal(row["CurrentDebt"]),
            IsActive = Convert.ToBoolean(row["IsActive"]),
            CreatedAt = Convert.ToDateTime(row["CreatedAt"]),
            UpdatedAt = Convert.ToDateTime(row["UpdatedAt"])
        };
    }
}
