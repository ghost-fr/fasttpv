using FastTPV.Core.Models;

namespace FastTPV.Core.Interfaces;

/// <summary>
/// Interface for database operations
/// </summary>
public interface IDatabase
{
    Task<bool> InitializeAsync();
    Task<bool> ConnectAsync(string connectionString);
    Task<bool> IsConnectedAsync();
    void Disconnect();
}

/// <summary>
/// Repository pattern for Article data access
/// </summary>
public interface IArticleRepository
{
    Task<Article?> GetByIdAsync(int id);
    Task<Article?> GetByCodeAsync(string code);
    Task<List<Article>> GetAllAsync();
    Task<List<Article>> GetByCategoryAsync(string category);
    Task<int> AddAsync(Article article);
    Task<bool> UpdateAsync(Article article);
    Task<bool> DeleteAsync(int id);
}

/// <summary>
/// Repository pattern for Sale data access
/// </summary>
public interface ISaleRepository
{
    Task<Sale?> GetByIdAsync(int id);
    Task<Sale?> GetByTicketNumberAsync(string ticketNumber);
    Task<List<Sale>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<int> AddAsync(Sale sale);
    Task<bool> UpdateAsync(Sale sale);
    Task<bool> CancelAsync(int saleId);
}

/// <summary>
/// Repository pattern for Customer data access
/// </summary>
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(int id);
    Task<Customer?> GetByCodeAsync(string code);
    Task<List<Customer>> GetAllAsync();
    Task<int> AddAsync(Customer customer);
    Task<bool> UpdateAsync(Customer customer);
    Task<bool> DeleteAsync(int id);
}
