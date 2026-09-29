using MySql.Data.MySqlClient;
using Serilog;

namespace FastTPV.Core.Data;

/// <summary>
/// Creates the FastTPV schema on first run so the app is usable against an empty
/// database — you only need to CREATE DATABASE FastTPV; the tables are created here.
/// </summary>
public static class DatabaseInitializer
{
    private static readonly ILogger Logger = Log.ForContext(typeof(DatabaseInitializer));

    public static async Task EnsureSchemaAsync(DatabaseContext db)
    {
        var statements = new[]
        {
            @"CREATE TABLE IF NOT EXISTS Articles (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                Code VARCHAR(50) UNIQUE NOT NULL,
                Name VARCHAR(255) NOT NULL,
                Description TEXT,
                Price DECIMAL(10, 2) NOT NULL DEFAULT 0,
                CostPrice DECIMAL(10, 2) NOT NULL DEFAULT 0,
                StockLevel INT NOT NULL DEFAULT 0,
                MinimumStock INT NOT NULL DEFAULT 0,
                Category VARCHAR(100),
                PricingMode VARCHAR(20) NOT NULL DEFAULT 'Fixed',
                IsActive BOOLEAN NOT NULL DEFAULT TRUE,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                INDEX(Code), INDEX(Category)
            )",
            @"CREATE TABLE IF NOT EXISTS Customers (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                Code VARCHAR(50) UNIQUE NOT NULL,
                Name VARCHAR(255) NOT NULL,
                Email VARCHAR(255),
                Phone VARCHAR(20),
                Address VARCHAR(255),
                City VARCHAR(100),
                ZipCode VARCHAR(20),
                TaxId VARCHAR(50),
                CreditLimit DECIMAL(10, 2) NOT NULL DEFAULT 0,
                CurrentDebt DECIMAL(10, 2) NOT NULL DEFAULT 0,
                IsActive BOOLEAN NOT NULL DEFAULT TRUE,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
            )",
            @"CREATE TABLE IF NOT EXISTS Sales (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                TicketNumber VARCHAR(50) UNIQUE NOT NULL,
                CustomerId INT NULL,
                SaleDate DATETIME NOT NULL,
                SubTotal DECIMAL(10, 2) NOT NULL DEFAULT 0,
                Tax DECIMAL(10, 2) NOT NULL DEFAULT 0,
                TotalAmount DECIMAL(10, 2) NOT NULL DEFAULT 0,
                TicketDiscountAmount DECIMAL(10, 2) NOT NULL DEFAULT 0,
                PaymentMethod VARCHAR(50),
                Status VARCHAR(20) NOT NULL DEFAULT 'Completed',
                Notes TEXT,
                SalesmanId INT NULL,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            )",
            @"CREATE TABLE IF NOT EXISTS SaleLineItems (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                SaleId INT NOT NULL,
                ArticleId INT NOT NULL,
                ArticleName VARCHAR(255),
                Quantity INT NOT NULL DEFAULT 1,
                UnitPrice DECIMAL(10, 2) NOT NULL DEFAULT 0,
                LineTotal DECIMAL(10, 2) NOT NULL DEFAULT 0,
                Discount DECIMAL(10, 2) NOT NULL DEFAULT 0,
                INDEX(SaleId)
            )",
            @"CREATE TABLE IF NOT EXISTS Users (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                UserName VARCHAR(50) UNIQUE NOT NULL,
                DisplayName VARCHAR(100) NOT NULL,
                PasswordHash VARCHAR(255) NOT NULL,
                PasswordSalt VARCHAR(255) NOT NULL,
                Role VARCHAR(20) NOT NULL DEFAULT 'Cashier',
                IsActive BOOLEAN NOT NULL DEFAULT TRUE,
                FailedLoginAttempts INT NOT NULL DEFAULT 0,
                LockedUntil DATETIME NULL,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            )",
            @"CREATE TABLE IF NOT EXISTS AuditLog (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                UserId INT NULL,
                UserName VARCHAR(50),
                Action VARCHAR(100) NOT NULL,
                Entity VARCHAR(100),
                Details TEXT,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            )",
            @"CREATE TABLE IF NOT EXISTS CashSessions (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                OpenedByUserId INT NULL,
                OpenedAt DATETIME NOT NULL,
                OpeningFloat DECIMAL(10, 2) NOT NULL DEFAULT 0,
                ClosedAt DATETIME NULL,
                ClosingCounted DECIMAL(10, 2) NULL,
                ExpectedCash DECIMAL(10, 2) NULL,
                Variance DECIMAL(10, 2) NULL,
                Status VARCHAR(20) NOT NULL DEFAULT 'Open'
            )",
            @"CREATE TABLE IF NOT EXISTS CashMovements (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                SessionId INT NOT NULL,
                Type VARCHAR(20) NOT NULL,
                Amount DECIMAL(10, 2) NOT NULL,
                Reason VARCHAR(255),
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            )",
            @"CREATE TABLE IF NOT EXISTS Suppliers (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                Code VARCHAR(50) UNIQUE NOT NULL,
                Name VARCHAR(255) NOT NULL,
                ContactName VARCHAR(100),
                Email VARCHAR(255),
                Phone VARCHAR(20),
                TaxId VARCHAR(50),
                Address VARCHAR(255),
                IsActive BOOLEAN NOT NULL DEFAULT TRUE,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            )",
            @"CREATE TABLE IF NOT EXISTS StockMovements (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                ArticleId INT NOT NULL,
                QuantityDelta INT NOT NULL,
                Reason VARCHAR(255),
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                CreatedByUserId INT NULL
            )",
            @"CREATE TABLE IF NOT EXISTS Payments (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                SaleId INT NOT NULL,
                Method VARCHAR(50) NOT NULL,
                Amount DECIMAL(10, 2) NOT NULL,
                ChangeAmount DECIMAL(10, 2) NOT NULL DEFAULT 0,
                Reference VARCHAR(100),
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
            )",
            @"CREATE TABLE IF NOT EXISTS Categories (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                Name VARCHAR(100) UNIQUE NOT NULL
            )"
        };

        foreach (var statement in statements)
        {
            await db.ExecuteNonQueryAsync(statement);
        }

        // Columns added after the tables they belong to already existed on some
        // installs — CREATE TABLE IF NOT EXISTS above is a no-op against those.
        // Error 1060 (duplicate column) means it's already there; safe to ignore.
        await TryAlterAsync(db, "ALTER TABLE Users ADD COLUMN Role VARCHAR(20) NOT NULL DEFAULT 'Cashier'");
        await TryAlterAsync(db, "ALTER TABLE Sales ADD COLUMN TicketDiscountAmount DECIMAL(10, 2) NOT NULL DEFAULT 0");
        await TryAlterAsync(db, "ALTER TABLE Users ADD COLUMN FailedLoginAttempts INT NOT NULL DEFAULT 0");
        await TryAlterAsync(db, "ALTER TABLE Users ADD COLUMN LockedUntil DATETIME NULL");
        await TryAlterAsync(db, "ALTER TABLE Articles ADD COLUMN PricingMode VARCHAR(20) NOT NULL DEFAULT 'Fixed'");

        Logger.Information("Database schema verified/created");
    }

    private static async Task TryAlterAsync(DatabaseContext db, string statement)
    {
        try
        {
            await db.ExecuteNonQueryAsync(statement);
        }
        catch (MySqlException ex) when (ex.Number == 1060) // Duplicate column name
        {
            // Column already exists — nothing to do.
        }
    }
}
