using MySql.Data.MySqlClient;
using Serilog;

namespace FastTPV.Core.Data;

/// <summary>
/// Database connection management for FastTPV.
/// Opens a fresh connection per operation (MySqlConnection/MySqlCommand are not safe
/// to share across concurrent async calls), which is what makes this safe to call from
/// multiple ViewModels at once.
/// </summary>
public class DatabaseContext
{
    private readonly string _connectionString;
    private static readonly ILogger Logger = Log.ForContext<DatabaseContext>();

    public DatabaseContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Verifies that the database is reachable. Call once at startup.
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            Logger.Information("Database connection established successfully");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to connect to database");
            return false;
        }
    }

    /// <summary>
    /// Execute a SQL query and return rows as dictionaries keyed by column name.
    /// </summary>
    public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string query, params MySqlParameter[] parameters)
    {
        var result = new List<Dictionary<string, object>>();

        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var cmd = new MySqlCommand(query, connection);
            if (parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
                }
                result.Add(row);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error executing query: {Query}", query);
            throw;
        }

        return result;
    }

    /// <summary>
    /// Execute a non-query command (INSERT, UPDATE, DELETE, DDL). Returns affected row count.
    /// </summary>
    public async Task<int> ExecuteNonQueryAsync(string query, params MySqlParameter[] parameters)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var cmd = new MySqlCommand(query, connection);
            if (parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            return await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error executing non-query: {Query}", query);
            throw;
        }
    }

    /// <summary>
    /// Execute an INSERT and return the newly generated auto-increment id (LAST_INSERT_ID()).
    /// </summary>
    public async Task<long> ExecuteInsertAsync(string query, params MySqlParameter[] parameters)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var cmd = new MySqlCommand(query + "; SELECT LAST_INSERT_ID();", connection);
            if (parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            var result = await cmd.ExecuteScalarAsync();
            return result == null || result == DBNull.Value ? 0 : Convert.ToInt64(result);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error executing insert: {Query}", query);
            throw;
        }
    }

    /// <summary>
    /// Execute a query and return a single scalar value.
    /// </summary>
    public async Task<object?> ExecuteScalarAsync(string query, params MySqlParameter[] parameters)
    {
        try
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var cmd = new MySqlCommand(query, connection);
            if (parameters.Length > 0)
                cmd.Parameters.AddRange(parameters);

            return await cmd.ExecuteScalarAsync();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error executing scalar query: {Query}", query);
            throw;
        }
    }
}
