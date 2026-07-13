using BatoBuzz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace BatoBuzz.Api.Infrastructure;

/// <summary>
/// Applies the small ordered upgrade set needed by API PostgreSQL deployments
/// that were originally created with EnsureCreated.
/// </summary>
public static class PostgresSchemaUpgrader
{
    private const long AdvisoryLockId = 2_026_071_301;

    private static readonly IReadOnlyList<PostgresMigration> Migrations =
    [
        new(
            "20260710_001_period_lock_and_bank_reconciliation",
            [
                """ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "PeriodLockDate" date NULL""",
                """ALTER TABLE "Companies" ALTER COLUMN "PeriodLockDate" TYPE date USING "PeriodLockDate"::date""",
                """ALTER TABLE "JournalLines" ADD COLUMN IF NOT EXISTS "IsCleared" boolean NOT NULL DEFAULT FALSE""",
                """ALTER TABLE "JournalLines" ADD COLUMN IF NOT EXISTS "ClearedDate" date NULL""",
                """ALTER TABLE "JournalLines" ALTER COLUMN "ClearedDate" TYPE date USING "ClearedDate"::date"""
            ]),
        new(
            "20260713_002_normalize_weighted_average_costing",
            [
                """ALTER TABLE "Items" ADD COLUMN IF NOT EXISTS "CostingMethod" integer NOT NULL DEFAULT 2""",
                """UPDATE "Items" SET "CostingMethod" = 2 WHERE "CostingMethod" = 1"""
            ]),
        new(
            "20260713_003_operational_document_journal_links",
            [
                """ALTER TABLE "SalesInvoices" ADD COLUMN IF NOT EXISTS "PostedJournalEntryId" uuid NULL""",
                """ALTER TABLE "Receipts" ADD COLUMN IF NOT EXISTS "PostedJournalEntryId" uuid NULL""",
                """ALTER TABLE "PurchaseBills" ADD COLUMN IF NOT EXISTS "PostedJournalEntryId" uuid NULL""",
                """ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "PostedJournalEntryId" uuid NULL""",
                """
                UPDATE "SalesInvoices" d SET "PostedJournalEntryId" = (
                    SELECT x."Id" FROM "JournalEntries" x
                    WHERE x."CompanyId" = d."CompanyId" AND x."VoucherType" = 1
                      AND x."ReferenceNumber" = d."InvoiceNumber"
                    ORDER BY x."CreatedAt" LIMIT 1)
                WHERE d."PostedJournalEntryId" IS NULL
                """,
                """
                UPDATE "Receipts" d SET "PostedJournalEntryId" = (
                    SELECT x."Id" FROM "JournalEntries" x
                    WHERE x."CompanyId" = d."CompanyId" AND x."VoucherType" = 3
                      AND x."ReferenceNumber" = d."ReceiptNumber"
                    ORDER BY x."CreatedAt" LIMIT 1)
                WHERE d."PostedJournalEntryId" IS NULL
                """,
                """
                UPDATE "PurchaseBills" d SET "PostedJournalEntryId" = (
                    SELECT x."Id" FROM "JournalEntries" x
                    WHERE x."CompanyId" = d."CompanyId" AND x."VoucherType" = 2
                      AND (x."ReferenceNumber" = d."BillNumber"
                           OR x."ReferenceNumber" = d."SupplierInvoiceNumber")
                    ORDER BY x."CreatedAt" LIMIT 1)
                WHERE d."PostedJournalEntryId" IS NULL
                """,
                """
                UPDATE "Payments" d SET "PostedJournalEntryId" = (
                    SELECT x."Id" FROM "JournalEntries" x
                    WHERE x."CompanyId" = d."CompanyId" AND x."VoucherType" = 4
                      AND x."ReferenceNumber" = d."PaymentNumber"
                    ORDER BY x."CreatedAt" LIMIT 1)
                WHERE d."PostedJournalEntryId" IS NULL
                """
            ])
    ];

    public static async Task ApplyAllAsync(
        BatoBuzzDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var provider = dbContext.Database.ProviderName ?? string.Empty;
        if (!provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            return;

        var connection = dbContext.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        try
        {
            if (!wasOpen)
                await connection.OpenAsync(cancellationToken);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await ExecuteAsync(
                connection,
                transaction,
                """
                CREATE TABLE IF NOT EXISTS "__BatoBuzzSchemaMigrations" (
                    "MigrationId" text NOT NULL PRIMARY KEY,
                    "AppliedAtUtc" timestamp with time zone NOT NULL
                )
                """,
                cancellationToken);

            await ExecuteAsync(
                connection,
                transaction,
                $"SELECT pg_advisory_xact_lock({AdvisoryLockId})",
                cancellationToken);

            foreach (var migration in Migrations)
            {
                if (await IsAppliedAsync(
                        connection, transaction, migration.Id, cancellationToken))
                {
                    continue;
                }

                foreach (var statement in migration.Statements)
                    await ExecuteAsync(connection, transaction, statement, cancellationToken);

                await using var record = connection.CreateCommand();
                record.Transaction = transaction;
                record.CommandText =
                    """
                    INSERT INTO "__BatoBuzzSchemaMigrations" ("MigrationId", "AppliedAtUtc")
                    VALUES (@id, @appliedAtUtc)
                    ON CONFLICT ("MigrationId") DO NOTHING
                    """;
                AddParameter(record, "id", migration.Id);
                AddParameter(record, "appliedAtUtc", DateTime.UtcNow);
                await record.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            if (!wasOpen)
                await connection.CloseAsync();
        }
    }

    private static async Task<bool> IsAppliedAsync(
        DbConnection connection,
        DbTransaction transaction,
        string migrationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT 1 FROM "__BatoBuzzSchemaMigrations"
            WHERE "MigrationId" = @id
            LIMIT 1
            """;
        AddParameter(command, "id", migrationId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task ExecuteAsync(
        DbConnection connection,
        DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record PostgresMigration(
        string Id,
        IReadOnlyList<string> Statements);
}
