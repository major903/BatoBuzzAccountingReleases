using Microsoft.Data.Sqlite;

namespace BatoBuzz.Infrastructure.Persistence;

/// <summary>
/// Applies ordered, idempotent SQLite schema upgrades to databases originally
/// created with EnsureCreated. A consistent online backup is taken before any
/// pending upgrade and each migration is committed atomically.
/// </summary>
public static class SchemaUpgrader
{
    private const string HistoryTable = "__BatoBuzzSchemaMigrations";
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
        "PurchaseBillLines",
        "ItemCategories",
        "Payments",
        "PaymentAllocations",
        "Items",
        "Units",
        "Warehouses",
        "StockBalances",
        "AuditLogs",
        "StockMovements"
    ];

    private static readonly IReadOnlyList<SchemaMigration> Migrations =
    [
        new(
            "20260710_001_period_lock_and_bank_reconciliation",
            (connection, transaction) =>
            {
                EnsureColumn(connection, transaction, "Companies", "PeriodLockDate", "TEXT NULL");
                EnsureColumn(connection, transaction, "JournalLines", "IsCleared", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumn(connection, transaction, "JournalLines", "ClearedDate", "TEXT NULL");
            }),
        new(
            "20260713_002_normalize_weighted_average_costing",
            (connection, transaction) =>
            {
                if (!TableExists(connection, transaction, "Items"))
                {
                    throw new InvalidOperationException(
                        "The Items table is required before the costing migration can run.");
                }

                EnsureColumn(connection, transaction, "Items", "CostingMethod", "INTEGER NOT NULL DEFAULT 2");
                using var normalize = connection.CreateCommand();
                normalize.Transaction = transaction;
                normalize.CommandText = "UPDATE Items SET CostingMethod = 2 WHERE CostingMethod = 1";
                normalize.ExecuteNonQuery();
            }),
        new(
            "20260713_003_operational_document_journal_links",
            (connection, transaction) =>
            {
                EnsureColumn(connection, transaction, "SalesInvoices", "PostedJournalEntryId", "TEXT NULL");
                EnsureColumn(connection, transaction, "Receipts", "PostedJournalEntryId", "TEXT NULL");
                EnsureColumn(connection, transaction, "PurchaseBills", "PostedJournalEntryId", "TEXT NULL");
                EnsureColumn(connection, transaction, "Payments", "PostedJournalEntryId", "TEXT NULL");

                ExecuteNonQuery(connection, transaction, """
                    UPDATE SalesInvoices SET PostedJournalEntryId = (
                        SELECT j.Id FROM JournalEntries j
                        WHERE j.CompanyId = SalesInvoices.CompanyId AND j.VoucherType = 1
                          AND j.ReferenceNumber = SalesInvoices.InvoiceNumber
                        ORDER BY j.CreatedAt LIMIT 1)
                    WHERE PostedJournalEntryId IS NULL
                    """);
                ExecuteNonQuery(connection, transaction, """
                    UPDATE Receipts SET PostedJournalEntryId = (
                        SELECT j.Id FROM JournalEntries j
                        WHERE j.CompanyId = Receipts.CompanyId AND j.VoucherType = 3
                          AND j.ReferenceNumber = Receipts.ReceiptNumber
                        ORDER BY j.CreatedAt LIMIT 1)
                    WHERE PostedJournalEntryId IS NULL
                    """);
                ExecuteNonQuery(connection, transaction, """
                    UPDATE PurchaseBills SET PostedJournalEntryId = (
                        SELECT j.Id FROM JournalEntries j
                        WHERE j.CompanyId = PurchaseBills.CompanyId AND j.VoucherType = 2
                          AND (j.ReferenceNumber = PurchaseBills.BillNumber
                               OR j.ReferenceNumber = PurchaseBills.SupplierInvoiceNumber)
                        ORDER BY j.CreatedAt LIMIT 1)
                    WHERE PostedJournalEntryId IS NULL
                    """);
                ExecuteNonQuery(connection, transaction, """
                    UPDATE Payments SET PostedJournalEntryId = (
                        SELECT j.Id FROM JournalEntries j
                        WHERE j.CompanyId = Payments.CompanyId AND j.VoucherType = 4
                          AND j.ReferenceNumber = Payments.PaymentNumber
                        ORDER BY j.CreatedAt LIMIT 1)
                    WHERE PostedJournalEntryId IS NULL
                    """);
            }),
        new(
            "20260713_004_journal_reversal_link",
            (connection, transaction) =>
            {
                EnsureColumn(connection, transaction, "JournalEntries", "ReversedJournalEntryId", "TEXT NULL");
                EnsureColumn(connection, transaction, "JournalEntries", "ReversalReason", "TEXT NULL");

                if (ColumnExists(connection, transaction, "JournalEntries", "ReversalJournalEntryId"))
                {
                    ExecuteNonQuery(connection, transaction, """
                        UPDATE JournalEntries
                        SET ReversedJournalEntryId = ReversalJournalEntryId
                        WHERE ReversedJournalEntryId IS NULL
                          AND ReversalJournalEntryId IS NOT NULL
                        """);
                }
            }),
        new(
            "20260714_005_add_tds_rates",
            (connection, transaction) =>
            {
                ExecuteNonQuery(connection, transaction, """
                    CREATE TABLE IF NOT EXISTS "TdsRates" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_TdsRates" PRIMARY KEY,
                        "Name" TEXT NOT NULL,
                        "RatePercent" TEXT NOT NULL,
                        "Description" TEXT NULL,
                        "IsActive" INTEGER NOT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "CreatedByUserId" TEXT NULL,
                        "ModifiedAt" TEXT NULL,
                        "ModifiedByUserId" TEXT NULL,
                        "RowVersion" BLOB NOT NULL,
                        "CompanyId" TEXT NOT NULL
                    )
                    """);
            })
    ];

    public static void ApplyAll(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        EnsureDatabaseReadyForUpgrade(connection);

        var applied = HistoryTableExists(connection)
            ? GetAppliedMigrationIds(connection)
            : new HashSet<string>(StringComparer.Ordinal);
        var pending = Migrations.Where(migration => !applied.Contains(migration.Id)).ToList();
        if (pending.Count == 0)
            return;

        CreateSafetyBackup(connection);
        EnsureHistoryTable(connection);

        foreach (var migration in pending)
        {
            using var transaction = connection.BeginTransaction(deferred: false);
            migration.Apply(connection, transaction);

            using var record = connection.CreateCommand();
            record.Transaction = transaction;
            record.CommandText =
                $"INSERT OR IGNORE INTO {QuoteIdentifier(HistoryTable)} (MigrationId, AppliedAtUtc) VALUES ($id, $appliedAtUtc)";
            record.Parameters.AddWithValue("$id", migration.Id);
            record.Parameters.AddWithValue("$appliedAtUtc", DateTime.UtcNow.ToString("O"));
            record.ExecuteNonQuery();

            transaction.Commit();
        }
    }

    private static bool HistoryTableExists(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name LIMIT 1";
        command.Parameters.AddWithValue("$name", HistoryTable);
        return command.ExecuteScalar() is not null;
    }

    private static HashSet<string> GetAppliedMigrationIds(SqliteConnection connection)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT MigrationId FROM {QuoteIdentifier(HistoryTable)}";
        using var reader = command.ExecuteReader();
        while (reader.Read())
            result.Add(reader.GetString(0));

        return result;
    }

    private static void EnsureHistoryTable(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE IF NOT EXISTS {QuoteIdentifier(HistoryTable)} (
                MigrationId TEXT NOT NULL PRIMARY KEY,
                AppliedAtUtc TEXT NOT NULL
            )
            """;
        command.ExecuteNonQuery();
    }

    private static void EnsureColumn(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string columnName,
        string columnDefinitionSql)
    {
        if (!TableExists(connection, transaction, tableName)
            || ColumnExists(connection, transaction, tableName, columnName))
        {
            return;
        }

        using var alter = connection.CreateCommand();
        alter.Transaction = transaction;
        alter.CommandText =
            $"ALTER TABLE {QuoteIdentifier(tableName)} ADD COLUMN {QuoteIdentifier(columnName)} {columnDefinitionSql}";
        alter.ExecuteNonQuery();
    }

    private static void ExecuteNonQuery(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }

    private static bool TableExists(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name LIMIT 1";
        command.Parameters.AddWithValue("$name", tableName);
        return command.ExecuteScalar() is not null;
    }

    private static bool ColumnExists(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tableName,
        string columnName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info({QuoteIdentifier(tableName)})";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(reader.GetOrdinal("name")), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void EnsureDatabaseReadyForUpgrade(SqliteConnection connection)
    {
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
                    $"The SQLite database failed its integrity check: {string.Join("; ", messages)}");
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
                    $"The SQLite database is missing the required '{tableName}' table. " +
                    "The schema upgrade was stopped before making changes.");
            }
        }
    }

    private static void CreateSafetyBackup(SqliteConnection connection)
    {
        var builder = new SqliteConnectionStringBuilder(connection.ConnectionString);
        if (builder.Mode == SqliteOpenMode.Memory
            || string.IsNullOrWhiteSpace(builder.DataSource)
            || string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var databasePath = Path.GetFullPath(builder.DataSource);
        if (!File.Exists(databasePath) || new FileInfo(databasePath).Length == 0)
            return;

        var directory = Path.GetDirectoryName(databasePath)
            ?? throw new InvalidOperationException("The database directory could not be resolved.");
        var fileName = Path.GetFileNameWithoutExtension(databasePath);
        var extension = Path.GetExtension(databasePath);
        var backupPath = Path.Combine(
            directory,
            $"{fileName}.pre-upgrade-{DateTime.UtcNow:yyyyMMdd-HHmmssfff}{extension}");

        var backupBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = backupPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };
        using var backup = new SqliteConnection(backupBuilder.ConnectionString);
        backup.Open();
        connection.BackupDatabase(backup);
    }

    private static string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)
            || identifier.Any(character => !(char.IsLetterOrDigit(character) || character == '_')))
        {
            throw new ArgumentException("SQLite identifier contains unsupported characters.", nameof(identifier));
        }

        return $"\"{identifier}\"";
    }

    private sealed record SchemaMigration(
        string Id,
        Action<SqliteConnection, SqliteTransaction> Apply);
}
