using Microsoft.Data.Sqlite;
using System.IO;

namespace BatoBuzz.Desktop.Services;

public static class SqliteDatabaseGuard
{
    private static readonly string[] RequiredTables =
    [
        "Users",
        "Roles",
        "UserRole",
        "Permissions",
        "RolePermission",
        "Companies",
        "Branches",
        "FinancialYears",
        "AccountGroups",
        "Ledgers",
        "JournalEntries",
        "JournalLines",
        "Customers",
        "Suppliers",
        "SalesInvoices",
        "SalesInvoiceLines",
        "Receipts",
        "ReceiptAllocations",
        "PurchaseBills",
        "ItemCategories",
        "PurchaseBillLines",
        "Payments",
        "PaymentAllocations",
        "Items",
        "Units",
        "Warehouses",
        "AuditLogs",
        "StockBalances",
        "StockMovements"
    ];

    public static void ValidateBackup(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var fullPath = Path.GetFullPath(databasePath);
        if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
            throw new InvalidOperationException("The selected database backup is missing or empty.");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private
        };
        using var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();

        using (var integrityCheck = connection.CreateCommand())
        {
            integrityCheck.CommandText = "PRAGMA quick_check";
            using var reader = integrityCheck.ExecuteReader();
            var messages = new List<string>();
            while (reader.Read())
                messages.Add(reader.GetString(0));

            if (messages.Count != 1
                || !string.Equals(messages[0], "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The selected database failed its integrity check: {string.Join("; ", messages)}");
            }
        }

        foreach (var tableName in RequiredTables)
        {
            using var tableCheck = connection.CreateCommand();
            tableCheck.CommandText =
                "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name LIMIT 1";
            tableCheck.Parameters.AddWithValue("$name", tableName);
            if (tableCheck.ExecuteScalar() is null)
            {
                throw new InvalidOperationException(
                    $"The selected file is not a compatible BatoBuzz backup (missing table: {tableName}).");
            }
        }
    }

    public static void CreateValidatedBackup(string sourcePath, string destinationPath) =>
        CreateOnlineBackup(sourcePath, destinationPath);

    public static void CreateOnlineBackup(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var fullSourcePath = Path.GetFullPath(sourcePath);
        var fullDestinationPath = Path.GetFullPath(destinationPath);
        if (!File.Exists(fullSourcePath))
            throw new FileNotFoundException("The source database was not found.", fullSourcePath);
        if (string.Equals(fullSourcePath, fullDestinationPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The backup destination must differ from the source database.");
        if (File.Exists(fullDestinationPath))
            throw new IOException($"The backup destination already exists: {fullDestinationPath}");

        Directory.CreateDirectory(
            Path.GetDirectoryName(fullDestinationPath)
            ?? throw new InvalidOperationException("The backup directory could not be resolved."));

        var sourceBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = fullSourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private
        };
        var destinationBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = fullDestinationPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private
        };

        try
        {
            using var source = new SqliteConnection(sourceBuilder.ConnectionString);
            using var destination = new SqliteConnection(destinationBuilder.ConnectionString);
            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
            destination.Close();

            ValidateBackup(fullDestinationPath);
        }
        catch
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (File.Exists(fullDestinationPath))
                    File.Delete(fullDestinationPath);
            }
            catch
            {
                // Preserve the original backup error.
            }

            throw;
        }
    }
}
