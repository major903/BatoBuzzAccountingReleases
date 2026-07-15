using BatoBuzz.Application.Interfaces;
using BatoBuzz.Application.Services;
using BatoBuzz.Contracts.Common;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Desktop.Services;
using BatoBuzz.Desktop.ViewModels;
using BatoBuzz.Domain.Enums;
using BatoBuzz.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ClosedXML.Excel;
using System.Reflection;

var dbPath = Path.Combine(Path.GetTempPath(), $"batobuzz-smoke-{Guid.NewGuid():N}.db");
var backupPath = Path.Combine(Path.GetTempPath(), $"batobuzz-smoke-backup-{Guid.NewGuid():N}.db");
var invalidBackupPath = Path.Combine(Path.GetTempPath(), $"batobuzz-smoke-invalid-{Guid.NewGuid():N}.db");
var reportExcelPath = Path.Combine(Path.GetTempPath(), $"batobuzz-smoke-report-{Guid.NewGuid():N}.xlsx");
var reportPdfPath = Path.Combine(Path.GetTempPath(), $"batobuzz-smoke-report-{Guid.NewGuid():N}.pdf");
var automaticBackupDataDirectory = Path.Combine(Path.GetTempPath(), $"batobuzz-smoke-auto-{Guid.NewGuid():N}");

try
{
    var services = new ServiceCollection();
    services.AddDbContext<BatoBuzzDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath};Cache=Shared"));
    services.AddScoped<IUnitOfWork, UnitOfWork>();
    services.AddScoped<ITokenService, LocalTokenService>();
    services.AddScoped<IAuthService, AuthService>();
    services.AddScoped<ICompanyService, CompanyService>();
    services.AddScoped<IAccountingService, AccountingService>();
    services.AddScoped<ISalesService, SalesService>();
    services.AddScoped<IPurchaseService, PurchaseService>();
    services.AddScoped<IInventoryService, InventoryService>();
    services.AddScoped<IDashboardService, DashboardService>();
    services.AddScoped<DesktopDataService>();

    await using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();

    var db = scope.ServiceProvider.GetRequiredService<BatoBuzzDbContext>();
    await db.Database.EnsureCreatedAsync();

    // Simulate a database created before the TDS feature was introduced.
    await db.Database.ExecuteSqlRawAsync("DROP TABLE \"TdsRates\"");
    SchemaUpgrader.ApplyAll($"Data Source={dbPath};Cache=Shared");
    SchemaUpgrader.ApplyAll($"Data Source={dbPath};Cache=Shared");
    await using (var migrationCheck = new SqliteConnection($"Data Source={dbPath};Cache=Shared"))
    {
        await migrationCheck.OpenAsync();
        await using var command = migrationCheck.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM __BatoBuzzSchemaMigrations";
        Require(Convert.ToInt32(await command.ExecuteScalarAsync()) == 6,
            "SQLite schema upgrades were not applied exactly once.");
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'TdsRates'";
        Require(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1,
            "SQLite schema upgrade did not create the TDS rates table.");
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'DocumentSequences'";
        Require(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1,
            "SQLite schema upgrade did not create the document sequence table.");
    }

    var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
    var data = scope.ServiceProvider.GetRequiredService<DesktopDataService>();
    var sales = scope.ServiceProvider.GetRequiredService<ISalesService>();
    var purchases = scope.ServiceProvider.GetRequiredService<IPurchaseService>();
    var inventory = scope.ServiceProvider.GetRequiredService<IInventoryService>();
    var accounting = scope.ServiceProvider.GetRequiredService<IAccountingService>();
    var dashboard = scope.ServiceProvider.GetRequiredService<IDashboardService>();
    var companyService = scope.ServiceProvider.GetRequiredService<ICompanyService>();
    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    var authResult = await auth.RegisterAsync(new RegisterRequest
    {
        UserName = "smokeowner",
        Email = "smokeowner@local.batobuzz",
        Password = "SmokeTest123!",
        FullName = "Smoke Test Owner"
    });
    Require(authResult.Success && authResult.User != null, "Owner registration failed.");

    var caseInsensitiveLogin = await auth.LoginAsync(new LoginRequest
    {
        UserName = "SMOKEOWNER",
        Password = "SmokeTest123!"
    });
    Require(caseInsensitiveLogin.Success && caseInsensitiveLogin.User?.Id == authResult.User!.Id,
        "Username lookup was not case-insensitive.");

    Require(!await auth.ChangePasswordAsync(authResult.User!.Id, new ChangePasswordRequest
    {
        CurrentPassword = "IncorrectOldPassword!",
        NewPassword = "ChangedSmoke123!"
    }), "Password change accepted an incorrect current password.");
    Require(await auth.ChangePasswordAsync(authResult.User.Id, new ChangePasswordRequest
    {
        CurrentPassword = "SmokeTest123!",
        NewPassword = "ChangedSmoke123!"
    }), "Password change failed with valid credentials.");
    Require(!(await auth.LoginAsync(new LoginRequest
    {
        UserName = "smokeowner",
        Password = "SmokeTest123!"
    })).Success, "Old password remained usable after password change.");
    Require((await auth.LoginAsync(new LoginRequest
    {
        UserName = "smokeowner",
        Password = "ChangedSmoke123!"
    })).Success, "New password was not usable after password change.");

    var lockoutUser = await auth.RegisterAsync(new RegisterRequest
    {
        UserName = "smokelockout",
        Email = "smokelockout@local.batobuzz",
        Password = "LockoutTest123!"
    });
    Require(lockoutUser.Success, "Lockout test user registration failed.");
    for (var attempt = 0; attempt < 5; attempt++)
    {
        Require(!(await auth.LoginAsync(new LoginRequest
        {
            UserName = "smokelockout",
            Password = "WrongPassword123!"
        })).Success, "Invalid password unexpectedly authenticated.");
    }
    var lockedLogin = await auth.LoginAsync(new LoginRequest
    {
        UserName = "smokelockout",
        Password = "LockoutTest123!"
    });
    Require(!lockedLogin.Success && lockedLogin.Errors.Any(error =>
            error.Contains("locked", StringComparison.OrdinalIgnoreCase)),
        "Five failed logins did not lock the account.");

    var userId = authResult.User!.Id;
    var company = await data.EnsureCompanyAsync(userId);
    var customer = await data.GetOrCreateCustomerAsync(company.Id, "Smoke Customer", userId);
    var supplier = await data.GetOrCreateSupplierAsync(company.Id, "Smoke Supplier", userId);
    var unit = await data.GetOrCreateUnitAsync(company.Id, "Piece");
    var warehouse = await data.GetOrCreateWarehouseAsync(company.Id, "Main Warehouse");

    var item = await inventory.CreateItemAsync(new CreateItemRequest
    {
        CompanyId = company.Id,
        Name = "Smoke Inventory Item",
        Code = "SMK-ITEM",
        UnitId = unit.Id,
        ItemType = (int)ItemType.Goods,
        ReorderLevel = 5,
        StandardCost = 100,
        SalePrice = 150,
        CostingMethod = (int)CostingMethod.WeightedAverage
    }, userId);

    await inventory.RecordStockMovementAsync(new CreateStockMovementRequest
    {
        CompanyId = company.Id,
        ItemId = item.Id,
        WarehouseId = warehouse.Id,
        MovementDate = DateTime.Today,
        MovementType = (int)MovementType.OpeningStock,
        Quantity = 10,
        UnitCost = 100,
        Narration = "Smoke opening stock"
    }, userId);

    var limitedCustomer = await data.CreateCustomerAsync(
        company.Id, "Credit Limited Customer", null, null, null, null, null, 50m, userId);
    var overLimitInvoice = await sales.CreateInvoiceAsync(new CreateSalesInvoiceRequest
    {
        CompanyId = company.Id,
        CustomerId = limitedCustomer.Id,
        InvoiceDate = DateTime.Today,
        DueDate = DateTime.Today.AddDays(7),
        IsVatApplicable = false,
        VatRate = 0,
        Reference = "SMOKE-CREDIT-LIMIT",
        Lines =
        {
            new SalesInvoiceLineRequest
            {
                ItemId = item.Id,
                WarehouseId = warehouse.Id,
                Description = "Credit policy guard",
                Quantity = 1,
                Rate = 100,
                TaxPercent = 0
            }
        }
    }, userId);
    await ExpectThrowsAsync<InvalidOperationException>(
        () => sales.PostInvoiceAsync(overLimitInvoice.Id, userId),
        "Invoice posting exceeded the customer's credit limit.");
    var limitedCustomerAfter = await db.Customers.AsNoTracking()
        .SingleAsync(candidate => candidate.Id == limitedCustomer.Id);
    Require(limitedCustomerAfter.CurrentBalance == 0m,
        "Rejected credit-limit invoice changed the customer balance.");

    var invoice = await sales.CreateInvoiceAsync(new CreateSalesInvoiceRequest
    {
        CompanyId = company.Id,
        CustomerId = customer.Id,
        InvoiceDate = DateTime.Today,
        DueDate = DateTime.Today.AddDays(15),
        Reference = "SMOKE-SALE",
        Narration = "Smoke sales invoice",
        Lines =
        {
            new SalesInvoiceLineRequest
            {
                ItemId = item.Id,
                WarehouseId = warehouse.Id,
                Description = "Smoke sale line",
                Quantity = 2,
                Rate = 150,
                DiscountPercent = 0,
                TaxPercent = 13
            }
        }
    }, userId);
    invoice = await sales.PostInvoiceAsync(invoice.Id, userId);
    Require(invoice.TotalAmount == 339m, $"Unexpected sales invoice total: {invoice.TotalAmount}");

    await sales.RecordReceiptAsync(new CreateReceiptRequest
    {
        CompanyId = company.Id,
        CustomerId = customer.Id,
        ReceiptDate = DateTime.Today,
        Amount = 100,
        PaymentMethod = (int)PaymentMethod.Cash,
        Narration = "Smoke receipt",
        Allocations =
        {
            new ReceiptAllocationRequest { SalesInvoiceId = invoice.Id, AmountAllocated = 100 }
        }
    }, userId);

    var bill = await purchases.CreateBillAsync(new CreatePurchaseBillRequest
    {
        CompanyId = company.Id,
        SupplierId = supplier.Id,
        BillDate = DateTime.Today,
        DueDate = DateTime.Today.AddDays(20),
        SupplierInvoiceNumber = "SUP-SMOKE-1",
        Narration = "Smoke purchase bill",
        Lines =
        {
            new PurchaseBillLineRequest
            {
                ItemId = item.Id,
                WarehouseId = warehouse.Id,
                Description = "Smoke purchase line",
                Quantity = 3,
                Rate = 100,
                DiscountPercent = 0,
                TaxPercent = 13
            }
        }
    }, userId);
    bill = await purchases.PostBillAsync(bill.Id, userId);
    Require(bill.TotalAmount == 339m, $"Unexpected purchase bill total: {bill.TotalAmount}");

    await purchases.RecordPaymentAsync(new CreatePaymentRequest
    {
        CompanyId = company.Id,
        SupplierId = supplier.Id,
        PaymentDate = DateTime.Today,
        Amount = 75,
        PaymentMethod = (int)PaymentMethod.Cash,
        Narration = "Smoke payment",
        Allocations =
        {
            new PaymentAllocationRequest { PurchaseBillId = bill.Id, AmountAllocated = 75 }
        }
    }, userId);

    var cash = await data.GetLedgerByNameAsync(company.Id, "Cash Account");
    var bank = await data.GetLedgerByNameAsync(company.Id, "Bank Account");
    var journal = await accounting.CreateJournalAsync(new CreateJournalRequest
    {
        CompanyId = company.Id,
        EntryDate = DateTime.Today,
        VoucherType = (int)VoucherType.Contra,
        ReferenceNumber = "SMOKE-CONTRA",
        Narration = "Smoke cash to bank transfer",
        Lines =
        {
            new JournalLineRequest { LedgerId = bank.Id, DebitAmount = 25, Narration = "Deposit to bank" },
            new JournalLineRequest { LedgerId = cash.Id, CreditAmount = 25, Narration = "Cash deposit" }
        }
    }, userId);
    await accounting.PostJournalAsync(journal.Id, userId);

    var trialBalance = await accounting.GetTrialBalanceAsync(company.Id, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));
    Require(trialBalance.TotalDebit == trialBalance.TotalCredit,
        $"Trial balance mismatch: Dr {trialBalance.TotalDebit:N2}, Cr {trialBalance.TotalCredit:N2}");

    var dash = await dashboard.GetDashboardAsync(company.Id);
    Require(dash.TotalSalesMonth > 0, "Dashboard sales did not update.");
    Require(dash.TotalPurchasesMonth > 0, "Dashboard purchases did not update.");

    var stock = await inventory.GetStockBalancesAsync(company.Id, warehouse.Id);
    Require(stock.Any(s => s.ItemId == item.Id && s.Quantity == 11),
        "Inventory stock balance should be opening 10 + purchase 3 - sale 2 = 11.");

    var stockLedger = await data.GetLedgerByNameAsync(company.Id, "Stock-in-Hand");
    var costOfGoodsSoldLedger = await data.GetLedgerByNameAsync(company.Id, "Cost of Goods Sold");
    var stockLedgerReport = await accounting.GetGeneralLedgerAsync(
        company.Id, stockLedger.Id, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));
    var costOfGoodsSoldReport = await accounting.GetGeneralLedgerAsync(
        company.Id, costOfGoodsSoldLedger.Id, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));
    Require(stockLedgerReport.ClosingBalance == 1_100m,
        $"Stock-in-Hand should reconcile to the 1,100 inventory subledger; got {stockLedgerReport.ClosingBalance:N2}.");
    Require(costOfGoodsSoldReport.ClosingBalance == 200m,
        $"Cost of Goods Sold should equal the exact weighted-average sale cost of 200; got {costOfGoodsSoldReport.ClosingBalance:N2}.");

    await ExpectThrowsAsync<InvalidOperationException>(
        () => accounting.CreateJournalAsync(new CreateJournalRequest
        {
            CompanyId = company.Id, EntryDate = DateTime.Today, VoucherType = (int)VoucherType.Journal,
            Lines =
            {
                new JournalLineRequest { LedgerId = stockLedger.Id, DebitAmount = 1 },
                new JournalLineRequest { LedgerId = cash.Id, CreditAmount = 1 }
            }
        }, userId),
        "Manual journal accepted an inventory control ledger.");

    var roundingJournal = await accounting.CreateJournalAsync(new CreateJournalRequest
    {
        CompanyId = company.Id,
        EntryDate = DateTime.Today,
        VoucherType = (int)VoucherType.Journal,
        ReferenceNumber = "SMOKE-ROUNDING",
        Lines =
        {
            new JournalLineRequest { LedgerId = bank.Id, DebitAmount = 0.05m },
            new JournalLineRequest { LedgerId = cash.Id, CreditAmount = 0.045m },
            new JournalLineRequest { LedgerId = cash.Id, CreditAmount = 0.005m }
        }
    }, userId);
    await ExpectThrowsAsync<BatoBuzz.Domain.Exceptions.UnbalancedJournalException>(
        () => accounting.PostJournalAsync(roundingJournal.Id, userId),
        "Journal that became unbalanced at numeric(18,2) precision was posted.");

    await inventory.RecordStockMovementAsync(new CreateStockMovementRequest
    {
        CompanyId = company.Id,
        ItemId = item.Id,
        WarehouseId = warehouse.Id,
        MovementDate = DateTime.Today,
        MovementType = (int)MovementType.Damage,
        Quantity = 1,
        UnitCost = 999,
        Narration = "Smoke outbound cost guard"
    }, userId);
    var stockAfterDamage = await inventory.GetStockBalancesAsync(company.Id, warehouse.Id);
    Require(stockAfterDamage.Single(balance => balance.ItemId == item.Id).TotalValue == 1_000m,
        "Outbound movement did not use weighted-average value.");
    var damageMovement = (await unitOfWork.StockMovements.GetByItemAsync(item.Id, warehouse.Id))
        .First(movement => movement.MovementType == MovementType.Damage);
    Require(damageMovement.UnitCost == 100m && damageMovement.TotalCost == 100m,
        "Caller-supplied outbound unit cost was not ignored.");

    // Posting-effective reports: one prior-period posting contributes to opening,
    // while a draft and a legacy reversed entry contribute nothing.
    var priorJournal = await accounting.CreateJournalAsync(new CreateJournalRequest
    {
        CompanyId = company.Id,
        EntryDate = DateTime.Today.AddDays(-10),
        VoucherType = (int)VoucherType.Contra,
        ReferenceNumber = "SMOKE-PRIOR",
        Lines =
        {
            new JournalLineRequest { LedgerId = bank.Id, DebitAmount = 40 },
            new JournalLineRequest { LedgerId = cash.Id, CreditAmount = 40 }
        }
    }, userId);
    await accounting.PostJournalAsync(priorJournal.Id, userId);

    var reversedJournal = await accounting.CreateJournalAsync(new CreateJournalRequest
    {
        CompanyId = company.Id,
        EntryDate = DateTime.Today.AddDays(-3),
        VoucherType = (int)VoucherType.Contra,
        ReferenceNumber = "SMOKE-REVERSED",
        Lines =
        {
            new JournalLineRequest { LedgerId = bank.Id, DebitAmount = 7 },
            new JournalLineRequest { LedgerId = cash.Id, CreditAmount = 7 }
        }
    }, userId);
    await accounting.PostJournalAsync(reversedJournal.Id, userId);
    await companyService.SetPeriodLockDateAsync(company.Id, DateTime.Today.AddDays(-2), userId);
    var reversedJournalResult = await accounting.ReverseJournalAsync(reversedJournal.Id,
        new CorrectPostedDocumentRequest
        {
            CorrectionDate = DateTime.Today,
            Reason = "Smoke dated reversal"
        }, userId);
    var reversalEntries = await accounting.GetJournalsAsync(company.Id, DateTime.Today, DateTime.Today);
    Require(reversedJournalResult.ReversedJournalEntryId.HasValue
            && reversalEntries.Any(candidate =>
                candidate.Id == reversedJournalResult.ReversedJournalEntryId
                && candidate.EntryDate.Date == DateTime.Today),
        "Manual journal reversal did not use the selected correction date.");
    await companyService.SetPeriodLockDateAsync(company.Id, null, userId);

    var draftJournal = await accounting.CreateJournalAsync(new CreateJournalRequest
    {
        CompanyId = company.Id,
        EntryDate = DateTime.Today.AddDays(-2),
        VoucherType = (int)VoucherType.Contra,
        ReferenceNumber = "SMOKE-DRAFT",
        Lines =
        {
            new JournalLineRequest { LedgerId = bank.Id, DebitAmount = 999 },
            new JournalLineRequest { LedgerId = cash.Id, CreditAmount = 999 }
        }
    }, userId);

    var rangeStart = DateTime.Today.AddDays(-5);
    var rangeEnd = DateTime.Today.AddDays(1);
    var rangedLedger = await accounting.GetGeneralLedgerAsync(company.Id, bank.Id, rangeStart, rangeEnd);
    Require(rangedLedger.OpeningBalance == 40m,
        $"Ranged GL opening should include the prior posting only; got {rangedLedger.OpeningBalance:N2}.");
    Require(rangedLedger.ClosingBalance == 65m,
        $"Ranged GL closing should be opening 40 + current contra 25; got {rangedLedger.ClosingBalance:N2}.");
    Require(rangedLedger.Transactions.All(t => t.EntryNumber != draftJournal.EntryNumber),
        "Draft journal leaked into the general ledger.");
    Require(rangedLedger.Transactions.Any(t => t.EntryNumber == reversedJournal.EntryNumber)
            && rangedLedger.Transactions.Any(t => t.VoucherType == VoucherType.Reversal.ToString()),
        "Original and contra reversal were not both retained in the ledger audit trail.");

    var rangedTrialBalance = await accounting.GetTrialBalanceAsync(company.Id, rangeStart, rangeEnd);
    var rangedBank = rangedTrialBalance.Items.Single(i => i.LedgerId == bank.Id);
    Require(rangedBank.OpeningDebit == 40m && rangedBank.OpeningCredit == 0m,
        "Trial-balance opening did not include posted activity before fromDate.");
    Require(rangedBank.PeriodDebit == 32m && rangedBank.PeriodCredit == 7m,
        $"Trial balance should retain the original and contra reversal; got Dr {rangedBank.PeriodDebit:N2} / Cr {rangedBank.PeriodCredit:N2}.");
    Require(rangedTrialBalance.TotalDebit == rangedTrialBalance.TotalCredit,
        "Ranged trial balance is not balanced.");

    await ExpectThrowsAsync<InvalidOperationException>(
        () => accounting.GetGeneralLedgerAsync(company.Id, bank.Id, rangeEnd, rangeStart),
        "General ledger accepted an inverted date range.");

    // Historical ageing must reconstruct allocations through the requested date.
    var historicalCustomer = await data.GetOrCreateCustomerAsync(company.Id, "Historical Ageing Customer", userId);
    var historicalInvoice = await sales.CreateInvoiceAsync(new CreateSalesInvoiceRequest
    {
        CompanyId = company.Id,
        CustomerId = historicalCustomer.Id,
        InvoiceDate = DateTime.Today.AddDays(-5),
        DueDate = DateTime.Today.AddDays(-4),
        IsVatApplicable = false,
        VatRate = 0,
        Lines =
        {
            new SalesInvoiceLineRequest
            {
                Description = "Historical service",
                Quantity = 1,
                Rate = 100,
                TaxPercent = 0
            }
        }
    }, userId);
    historicalInvoice = await sales.PostInvoiceAsync(historicalInvoice.Id, userId);
    await sales.RecordReceiptAsync(new CreateReceiptRequest
    {
        CompanyId = company.Id,
        CustomerId = historicalCustomer.Id,
        ReceiptDate = DateTime.Today.AddDays(-1),
        Amount = 40,
        PaymentMethod = (int)PaymentMethod.Cash,
        Allocations =
        {
            new ReceiptAllocationRequest { SalesInvoiceId = historicalInvoice.Id, AmountAllocated = 40 }
        }
    }, userId);

    var historicalSupplier = await data.GetOrCreateSupplierAsync(company.Id, "Historical Ageing Supplier", userId);
    var historicalBill = await purchases.CreateBillAsync(new CreatePurchaseBillRequest
    {
        CompanyId = company.Id,
        SupplierId = historicalSupplier.Id,
        BillDate = DateTime.Today.AddDays(-5),
        DueDate = DateTime.Today.AddDays(-4),
        IsVatApplicable = false,
        VatRate = 0,
        Lines =
        {
            new PurchaseBillLineRequest
            {
                Description = "Historical service purchase",
                Quantity = 1,
                Rate = 100,
                TaxPercent = 0
            }
        }
    }, userId);
    historicalBill = await purchases.PostBillAsync(historicalBill.Id, userId);
    await purchases.RecordPaymentAsync(new CreatePaymentRequest
    {
        CompanyId = company.Id,
        SupplierId = historicalSupplier.Id,
        PaymentDate = DateTime.Today.AddDays(-1),
        Amount = 40,
        PaymentMethod = (int)PaymentMethod.Cash,
        Allocations =
        {
            new PaymentAllocationRequest { PurchaseBillId = historicalBill.Id, AmountAllocated = 40 }
        }
    }, userId);

    var historicalReceivables = await sales.GetReceivablesAgeingAsync(company.Id, DateTime.Today.AddDays(-2));
    var currentReceivables = await sales.GetReceivablesAgeingAsync(company.Id, DateTime.Today);
    Require(historicalReceivables.Single(item => item.ContactId == historicalCustomer.Id).TotalAmount == 100m,
        "Historical receivables ageing incorrectly applied a later receipt.");
    Require(currentReceivables.Single(item => item.ContactId == historicalCustomer.Id).TotalAmount == 60m,
        "Current receivables ageing did not apply the receipt.");

    var historicalPayables = await purchases.GetPayablesAgeingAsync(company.Id, DateTime.Today.AddDays(-2));
    var currentPayables = await purchases.GetPayablesAgeingAsync(company.Id, DateTime.Today);
    Require(historicalPayables.Single(item => item.ContactId == historicalSupplier.Id).TotalAmount == 100m,
        "Historical payables ageing incorrectly applied a later payment.");
    Require(currentPayables.Single(item => item.ContactId == historicalSupplier.Id).TotalAmount == 60m,
        "Current payables ageing did not apply the payment.");

    // Draft invoices/bills must not affect dashboard totals or ageing.
    var dashboardBeforeDrafts = await dashboard.GetDashboardAsync(company.Id);
    var draftCustomer = await data.GetOrCreateCustomerAsync(company.Id, "Draft Only Customer", userId);
    var draftSupplier = await data.GetOrCreateSupplierAsync(company.Id, "Draft Only Supplier", userId);
    var draftInvoice = await sales.CreateInvoiceAsync(new CreateSalesInvoiceRequest
    {
        CompanyId = company.Id,
        CustomerId = draftCustomer.Id,
        InvoiceDate = DateTime.Today,
        DueDate = DateTime.Today.AddDays(10),
        IsVatApplicable = false,
        VatRate = 0,
        Lines =
        {
            new SalesInvoiceLineRequest
            {
                Description = "Draft service",
                Quantity = 1,
                Rate = 500,
                DiscountPercent = 0,
                TaxPercent = 0
            }
        }
    }, userId);
    await purchases.CreateBillAsync(new CreatePurchaseBillRequest
    {
        CompanyId = company.Id,
        SupplierId = draftSupplier.Id,
        BillDate = DateTime.Today,
        DueDate = DateTime.Today.AddDays(10),
        IsVatApplicable = false,
        VatRate = 0,
        Lines =
        {
            new PurchaseBillLineRequest
            {
                Description = "Draft service purchase",
                Quantity = 1,
                Rate = 600,
                DiscountPercent = 0,
                TaxPercent = 0
            }
        }
    }, userId);

    var dashboardAfterDrafts = await dashboard.GetDashboardAsync(company.Id);
    Require(dashboardAfterDrafts.TotalSalesMonth == dashboardBeforeDrafts.TotalSalesMonth,
        "Draft invoice affected dashboard sales.");
    Require(dashboardAfterDrafts.TotalPurchasesMonth == dashboardBeforeDrafts.TotalPurchasesMonth,
        "Draft bill affected dashboard purchases.");
    var receivablesAgeing = await sales.GetReceivablesAgeingAsync(company.Id, DateTime.Today);
    var payablesAgeing = await purchases.GetPayablesAgeingAsync(company.Id, DateTime.Today);
    Require(receivablesAgeing.All(a => a.ContactId != draftCustomer.Id),
        "Draft invoice affected receivables ageing.");
    Require(payablesAgeing.All(a => a.ContactId != draftSupplier.Id),
        "Draft bill affected payables ageing.");

    // Invalid numeric, status, and allocation inputs must fail before mutation.
    await ExpectThrowsAsync<ArgumentOutOfRangeException>(
        () => sales.CreateInvoiceAsync(new CreateSalesInvoiceRequest
        {
            CompanyId = company.Id,
            CustomerId = customer.Id,
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today,
            IsVatApplicable = false,
            VatRate = 0,
            Lines =
            {
                new SalesInvoiceLineRequest
                {
                    Description = "Negative quantity",
                    Quantity = -1,
                    Rate = 10,
                    TaxPercent = 0
                }
            }
        }, userId),
        "Negative sales quantity was accepted.");

    await ExpectThrowsAsync<InvalidOperationException>(
        () => sales.RecordReceiptAsync(new CreateReceiptRequest
        {
            CompanyId = company.Id,
            CustomerId = customer.Id,
            ReceiptDate = DateTime.Today,
            Amount = 500,
            PaymentMethod = (int)PaymentMethod.Cash,
            Allocations =
            {
                new ReceiptAllocationRequest
                {
                    SalesInvoiceId = invoice.Id,
                    AmountAllocated = 500
                }
            }
        }, userId),
        "Receipt over-allocation was accepted.");

    await ExpectThrowsAsync<InvalidOperationException>(
        () => sales.RecordReceiptAsync(new CreateReceiptRequest
        {
            CompanyId = company.Id,
            CustomerId = draftCustomer.Id,
            ReceiptDate = DateTime.Today,
            Amount = 1,
            PaymentMethod = (int)PaymentMethod.Cash,
            Allocations =
            {
                new ReceiptAllocationRequest
                {
                    SalesInvoiceId = draftInvoice.Id,
                    AmountAllocated = 1
                }
            }
        }, userId),
        "Receipt allocation to a draft invoice was accepted.");

    await ExpectThrowsAsync<InvalidOperationException>(
        () => inventory.RecordStockMovementAsync(new CreateStockMovementRequest
        {
            CompanyId = company.Id,
            ItemId = item.Id,
            WarehouseId = warehouse.Id,
            MovementDate = DateTime.Today,
            MovementType = (int)MovementType.OpeningStock,
            Quantity = -1,
            UnitCost = 100
        }, userId),
        "Negative stock movement was accepted.");

    await ExpectThrowsAsync<InvalidOperationException>(
        () => inventory.RecordStockMovementAsync(new CreateStockMovementRequest
        {
            CompanyId = company.Id,
            ItemId = item.Id,
            WarehouseId = warehouse.Id,
            MovementDate = DateTime.Today,
            MovementType = (int)MovementType.StockTransferOut,
            Quantity = 1,
            UnitCost = 999
        }, userId),
        "Standalone stock transfer was accepted.");

    await ExpectThrowsAsync<InvalidOperationException>(
        () => inventory.RecordStockMovementAsync(new CreateStockMovementRequest
        {
            CompanyId = company.Id,
            ItemId = item.Id,
            WarehouseId = warehouse.Id,
            MovementDate = DateTime.Today,
            MovementType = (int)MovementType.PurchaseIn,
            Quantity = 1,
            UnitCost = 100
        }, userId),
        "Standalone purchase receipt was accepted without a supplier document.");

    await ExpectThrowsAsync<InvalidOperationException>(
        () => inventory.RecordStockMovementAsync(new CreateStockMovementRequest
        {
            CompanyId = company.Id,
            ItemId = item.Id,
            WarehouseId = warehouse.Id,
            MovementDate = DateTime.Today.AddDays(-1),
            MovementType = (int)MovementType.Damage,
            Quantity = 1,
            UnitCost = 0
        }, userId),
        "Backdated stock movement was accepted after later stock activity.");

    await ExpectThrowsAsync<InvalidOperationException>(
        () => inventory.CreateItemAsync(new CreateItemRequest
        {
            CompanyId = company.Id,
            Name = "Invalid Negative Inventory Item",
            UnitId = unit.Id,
            ItemType = (int)ItemType.Goods,
            CostingMethod = (int)CostingMethod.WeightedAverage,
            AllowNegativeStock = true
        }, userId),
        "Negative inventory was enabled for weighted-average costing.");

    // Cross-company references must be rejected at the application boundary.
    var secondaryCompany = await companyService.CreateCompanyAsync(new CreateCompanyRequest
    {
        Name = "Smoke Secondary Company",
        BaseCurrency = "NPR",
        FinancialYearStart = company.FinancialYearStart,
        FinancialYearEnd = company.FinancialYearEnd
    }, userId);
    await ExpectThrowsAsync<InvalidOperationException>(
        () => companyService.CreateCompanyAsync(new CreateCompanyRequest
        {
            Name = "Smoke Secondary Company",
            BaseCurrency = "NPR",
            FinancialYearStart = company.FinancialYearStart,
            FinancialYearEnd = company.FinancialYearEnd
        }, userId),
        "Same owner was allowed to create duplicate company names.");

    var hiddenCompany = await companyService.CreateCompanyAsync(new CreateCompanyRequest
    {
        Name = "Smoke Secondary Company",
        BaseCurrency = "NPR",
        FinancialYearStart = company.FinancialYearStart,
        FinancialYearEnd = company.FinancialYearEnd
    }, Guid.NewGuid());
    var ownedCompanies = await companyService.GetUserCompaniesAsync(userId);
    Require(ownedCompanies.Any(c => c.Id == secondaryCompany.Id)
            && ownedCompanies.All(c => c.Id != hiddenCompany.Id),
        "CompanyService did not filter companies by creator.");

    var updatedCompany = await companyService.UpdateCompanyAsync(company.Id, new CreateCompanyRequest
    {
        Name = company.Name,
        TradingName = "BatoBuzz Smoke Trading",
        CompanyRegNumber = "REG-SMOKE-001",
        BaseCurrency = "NPR",
        FinancialYearStart = company.FinancialYearStart,
        FinancialYearEnd = company.FinancialYearEnd
    }, userId);
    Require(updatedCompany.TradingName == "BatoBuzz Smoke Trading"
            && updatedCompany.CompanyRegNumber == "REG-SMOKE-001",
        "Company details update did not persist registration data.");
    await ExpectThrowsAsync<UnauthorizedAccessException>(
        () => companyService.UpdateCompanyAsync(company.Id, new CreateCompanyRequest
        {
            Name = company.Name,
            BaseCurrency = "NPR"
        }, Guid.NewGuid()),
        "A non-owner was allowed to edit company details.");

    await ExpectThrowsAsync<InvalidOperationException>(
        () => sales.CreateInvoiceAsync(new CreateSalesInvoiceRequest
        {
            CompanyId = secondaryCompany.Id,
            CustomerId = customer.Id,
            InvoiceDate = DateTime.Today,
            DueDate = DateTime.Today,
            IsVatApplicable = false,
            VatRate = 0,
            Lines =
            {
                new SalesInvoiceLineRequest
                {
                    Description = "Cross-company service",
                    Quantity = 1,
                    Rate = 10,
                    TaxPercent = 0
                }
            }
        }, userId),
        "Cross-company customer reference was accepted.");

    await ExpectThrowsAsync<InvalidOperationException>(
        () => accounting.CreateJournalAsync(new CreateJournalRequest
        {
            CompanyId = secondaryCompany.Id,
            EntryDate = DateTime.Today,
            VoucherType = (int)VoucherType.Journal,
            Lines =
            {
                new JournalLineRequest { LedgerId = bank.Id, DebitAmount = 1 },
                new JournalLineRequest { LedgerId = cash.Id, CreditAmount = 1 }
            }
        }, userId),
        "Cross-company ledger reference was accepted.");

    await ExpectThrowsAsync<InvalidOperationException>(
        () => accounting.GetGeneralLedgerAsync(
            secondaryCompany.Id,
            bank.Id,
            DateTime.Today.AddDays(-1),
            DateTime.Today.AddDays(1)),
        "General ledger accepted a company/ledger mismatch.");

    await ExpectThrowsAsync<InvalidOperationException>(
        () => inventory.RecordStockMovementAsync(new CreateStockMovementRequest
        {
            CompanyId = secondaryCompany.Id,
            ItemId = item.Id,
            WarehouseId = warehouse.Id,
            MovementDate = DateTime.Today,
            MovementType = (int)MovementType.OpeningStock,
            Quantity = 1,
            UnitCost = 100
        }, userId),
        "Cross-company inventory reference was accepted.");

    // Operational corrections must restore subledgers, inventory, and the GL atomically.
    var correctionStockBefore = (await inventory.GetStockBalancesAsync(company.Id, warehouse.Id))
        .Single(s => s.ItemId == item.Id);
    var correctionCustomerBefore = await db.Customers.AsNoTracking()
        .Where(c => c.Id == customer.Id).Select(c => c.CurrentBalance).SingleAsync();
    var correctionInvoice = await sales.CreateInvoiceAsync(new CreateSalesInvoiceRequest
    {
        CompanyId = company.Id,
        CustomerId = customer.Id,
        InvoiceDate = DateTime.Today,
        DueDate = DateTime.Today.AddDays(7),
        Reference = "CORRECTION-SALE",
        Lines =
        {
            new SalesInvoiceLineRequest
            {
                ItemId = item.Id, WarehouseId = warehouse.Id, Description = "Correction sale",
                Quantity = 1, Rate = 150, TaxPercent = 13
            }
        }
    }, userId);
    correctionInvoice = await sales.PostInvoiceAsync(correctionInvoice.Id, userId);
    Require(correctionInvoice.PostedJournalEntryId.HasValue, "Posted invoice was not linked to its journal.");

    var correctionReceipt = await sales.RecordReceiptAsync(new CreateReceiptRequest
    {
        CompanyId = company.Id,
        CustomerId = customer.Id,
        ReceiptDate = DateTime.Today,
        Amount = correctionInvoice.TotalAmount,
        PaymentMethod = (int)PaymentMethod.Cash,
        Narration = "Correction receipt",
        Allocations =
        {
            new ReceiptAllocationRequest
            {
                SalesInvoiceId = correctionInvoice.Id,
                AmountAllocated = correctionInvoice.TotalAmount
            }
        }
    }, userId);
    Require(correctionReceipt.PostedJournalEntryId.HasValue, "Receipt was not linked to its journal.");
    correctionReceipt = await sales.ReverseReceiptAsync(correctionReceipt.Id, new CorrectPostedDocumentRequest
    {
        CorrectionDate = DateTime.Today,
        Reason = "Smoke correction of receipt"
    }, userId);
    Require(correctionReceipt.Status == TransactionStatusDto.Reversed,
        "Receipt reversal did not mark the source journal as reversed.");
    await ExpectThrowsAsync<InvalidOperationException>(
        () => sales.ReverseReceiptAsync(correctionReceipt.Id, new CorrectPostedDocumentRequest
        {
            CorrectionDate = DateTime.Today,
            Reason = "Duplicate reversal must fail"
        }, userId),
        "The same receipt was reversed twice.");

    correctionInvoice = await sales.CancelInvoiceAsync(correctionInvoice.Id, new CorrectPostedDocumentRequest
    {
        CorrectionDate = DateTime.Today,
        Reason = "Smoke correction of invoice"
    }, userId);
    Require(correctionInvoice.Status == InvoiceStatusDto.Cancelled,
        "Invoice cancellation did not set the cancelled status.");
    var correctionStockAfter = (await inventory.GetStockBalancesAsync(company.Id, warehouse.Id))
        .Single(s => s.ItemId == item.Id);
    Require(correctionStockAfter.Quantity == correctionStockBefore.Quantity
            && correctionStockAfter.TotalValue == correctionStockBefore.TotalValue,
        "Invoice cancellation did not restore the exact inventory quantity and value.");
    var correctionCustomerAfter = await db.Customers.AsNoTracking()
        .Where(c => c.Id == customer.Id).Select(c => c.CurrentBalance).SingleAsync();
    Require(correctionCustomerAfter == correctionCustomerBefore,
        "Invoice/receipt corrections did not restore the customer subledger balance.");

    var correctionSupplierBefore = await db.Suppliers.AsNoTracking()
        .Where(s => s.Id == supplier.Id).Select(s => s.CurrentBalance).SingleAsync();
    var correctionBill = await purchases.CreateBillAsync(new CreatePurchaseBillRequest
    {
        CompanyId = company.Id,
        SupplierId = supplier.Id,
        BillDate = DateTime.Today,
        DueDate = DateTime.Today.AddDays(7),
        SupplierInvoiceNumber = "CORRECTION-PURCHASE",
        Lines =
        {
            new PurchaseBillLineRequest
            {
                ItemId = item.Id, WarehouseId = warehouse.Id, Description = "Correction purchase",
                Quantity = 1, Rate = 100, TaxPercent = 13
            }
        }
    }, userId);
    correctionBill = await purchases.PostBillAsync(correctionBill.Id, userId);
    Require(correctionBill.PostedJournalEntryId.HasValue, "Posted bill was not linked to its journal.");
    var correctionPayment = await purchases.RecordPaymentAsync(new CreatePaymentRequest
    {
        CompanyId = company.Id,
        SupplierId = supplier.Id,
        PaymentDate = DateTime.Today,
        Amount = correctionBill.TotalAmount - 10m,
        TdsAmount = 10m,
        PaymentMethod = (int)PaymentMethod.Cash,
        Narration = "Correction payment",
        Allocations =
        {
            new PaymentAllocationRequest
            {
                PurchaseBillId = correctionBill.Id,
                AmountAllocated = correctionBill.TotalAmount
            }
        }
    }, userId);
    Require(correctionPayment.PostedJournalEntryId.HasValue, "Payment was not linked to its journal.");
    Require(correctionPayment.Amount + correctionPayment.TdsAmount == correctionBill.TotalAmount,
        "Cash plus TDS did not settle the full purchase bill.");
    correctionPayment = await purchases.ReversePaymentAsync(correctionPayment.Id, new CorrectPostedDocumentRequest
    {
        CorrectionDate = DateTime.Today,
        Reason = "Smoke correction of payment"
    }, userId);
    Require(correctionPayment.Status == TransactionStatusDto.Reversed,
        "Payment reversal did not mark the source journal as reversed.");
    correctionBill = await purchases.CancelBillAsync(correctionBill.Id, new CorrectPostedDocumentRequest
    {
        CorrectionDate = DateTime.Today,
        Reason = "Smoke correction of purchase bill"
    }, userId);
    Require(correctionBill.Status == BillStatusDto.Cancelled,
        "Purchase bill cancellation did not set the cancelled status.");
    var correctionSupplierAfter = await db.Suppliers.AsNoTracking()
        .Where(s => s.Id == supplier.Id).Select(s => s.CurrentBalance).SingleAsync();
    Require(correctionSupplierAfter == correctionSupplierBefore,
        "Bill/payment corrections did not restore the supplier subledger balance.");

    var correctionTrialBalance = await accounting.GetTrialBalanceAsync(
        company.Id, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));
    Require(correctionTrialBalance.TotalDebit == correctionTrialBalance.TotalCredit,
        "Operational corrections left the trial balance out of balance.");
    var correctionAudit = await unitOfWork.AuditLogs.GetByCompanyAsync(company.Id, pageSize: 20);
    var correctionActions = correctionAudit.Select(log => log.Action).ToHashSet(StringComparer.Ordinal);
    Require(correctionActions.Contains("SalesInvoice.Cancelled")
            && correctionActions.Contains("Receipt.Reversed")
            && correctionActions.Contains("PurchaseBill.Cancelled")
            && correctionActions.Contains("Payment.Reversed"),
        "Operational corrections were not retained in the immutable audit log.");
    Require(correctionAudit
            .Where(log => correctionActions.Contains(log.Action))
            .All(log => string.Equals(log.UserName, "smokeowner", StringComparison.Ordinal)),
        "Correction audit records did not retain the acting username.");

    // A rolled-back tracked mutation must be detached and unable to leak into a later save.
    var rollbackCustomer = await unitOfWork.Customers.GetByIdWithLedgerAsync(customer.Id)
        ?? throw new InvalidOperationException("Rollback test customer not found.");
    var balanceBeforeRollback = rollbackCustomer.CurrentBalance;
    await unitOfWork.BeginTransactionAsync();
    rollbackCustomer.UpdateBalance(12_345m);
    await unitOfWork.SaveChangesAsync();
    await unitOfWork.RollbackTransactionAsync();
    Require(!db.ChangeTracker.Entries().Any(),
        "Rollback left tracked entities in the DbContext.");
    var balanceAfterRollback = await db.Customers.AsNoTracking()
        .Where(c => c.Id == customer.Id)
        .Select(c => c.CurrentBalance)
        .SingleAsync();
    Require(balanceAfterRollback == balanceBeforeRollback,
        "Rolled-back customer balance persisted.");

    var companyAfterRollback = await unitOfWork.Companies.GetByIdAsync(company.Id)
        ?? throw new InvalidOperationException("Company not found after rollback.");
    companyAfterRollback.SetModifiedBy(userId);
    await unitOfWork.SaveChangesAsync();
    var balanceAfterLaterSave = await db.Customers.AsNoTracking()
        .Where(c => c.Id == customer.Id)
        .Select(c => c.CurrentBalance)
        .SingleAsync();
    Require(balanceAfterLaterSave == balanceBeforeRollback,
        "Rolled-back mutation leaked into a later save.");

    var balanceSheet = await accounting.GetBalanceSheetAsync(company.Id, DateTime.Today);
    Require(balanceSheet.NetAssets == 0m,
        $"Balance sheet is out of balance by {balanceSheet.NetAssets:N2}.");

    // Period lock must protect the dated contra reversal.
    await companyService.SetPeriodLockDateAsync(company.Id, DateTime.Today, userId);
    await ExpectThrowsAsync<InvalidOperationException>(
        () => accounting.ReverseJournalAsync(journal.Id, "Must be blocked", userId),
        "Period lock did not block journal reversal.");
    await companyService.SetPeriodLockDateAsync(company.Id, null, userId);

    var rolloverDate = company.FinancialYearEnd.AddDays(1);
    var currentYearBeforeReport = await companyService.GetCurrentFinancialYearAsync(company.Id)
        ?? throw new InvalidOperationException("Current financial year was not available before the report.");
    var rolloverBalanceSheet = await accounting.GetBalanceSheetAsync(company.Id, rolloverDate);
    Require(rolloverBalanceSheet.NetAssets == 0m,
        "Balance sheet did not remain balanced for a future report date.");
    var currentYearAfterReport = await companyService.GetCurrentFinancialYearAsync(company.Id)
        ?? throw new InvalidOperationException("Current financial year was not available after the report.");
    Require(currentYearAfterReport.Id == currentYearBeforeReport.Id,
        "A balance sheet report changed the current financial year.");

    SqliteDatabaseGuard.CreateValidatedBackup(dbPath, backupPath);
    Require(File.Exists(backupPath) && new FileInfo(backupPath).Length > 0,
        "Validated online backup was not created.");
    SqliteDatabaseGuard.ValidateBackup(backupPath);
    await File.WriteAllTextAsync(invalidBackupPath, "not a SQLite database");
    await ExpectThrowsAsync<Exception>(
        () => Task.Run(() => SqliteDatabaseGuard.ValidateBackup(invalidBackupPath)),
        "An invalid backup file passed validation.");

    Directory.CreateDirectory(automaticBackupDataDirectory);
    var automaticBackupDatabasePath = Path.Combine(automaticBackupDataDirectory, "BatoBuzz.db");
    var automaticBackupDirectory = Path.Combine(automaticBackupDataDirectory, "Automatic");
    SqliteDatabaseGuard.CreateValidatedBackup(dbPath, automaticBackupDatabasePath);
    var automaticBackupService = new AutomaticBackupService(
        automaticBackupDatabasePath,
        automaticBackupDirectory);
    var firstAutomaticBackup = automaticBackupService.EnsureDailyBackup();
    Require(firstAutomaticBackup.Status == AutomaticBackupStatus.Created
            && !string.IsNullOrWhiteSpace(firstAutomaticBackup.BackupPath)
            && File.Exists(firstAutomaticBackup.BackupPath),
        $"The automatic daily backup was not created. Status: {firstAutomaticBackup.Status}; Path: {firstAutomaticBackup.BackupPath ?? "(none)"}.");
    SqliteDatabaseGuard.ValidateBackup(firstAutomaticBackup.BackupPath!);
    var repeatedAutomaticBackup = automaticBackupService.EnsureDailyBackup();
    Require(repeatedAutomaticBackup.Status == AutomaticBackupStatus.Current
            && string.Equals(repeatedAutomaticBackup.BackupPath, firstAutomaticBackup.BackupPath, StringComparison.OrdinalIgnoreCase),
        "The automatic backup created more than one backup for the same day.");

    const string exportHtml = "<html><body><h2>Smoke Report</h2><table><tr><th>Account</th><th>Amount</th></tr><tr><td>Cash</td><td>123.45</td></tr></table><h3>Balanced</h3></body></html>";
    var reportsType = typeof(ReportsViewModel);
    var parseReport = reportsType.GetMethod("ParseHtmlReport", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Report parser was not found.");
    var parsedReport = parseReport.Invoke(null, new object[] { exportHtml })
        ?? throw new InvalidOperationException("Report parser returned no result.");
    var writeExcel = reportsType.GetMethod("WriteReportExcel", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Excel report writer was not found.");
    var writePdf = reportsType.GetMethod("WriteReportPdf", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("PDF report writer was not found.");
    writeExcel.Invoke(null, new[] { parsedReport, reportExcelPath });
    writePdf.Invoke(null, new[] { parsedReport, reportPdfPath });
    using (var workbook = new XLWorkbook(reportExcelPath))
    {
        Require(workbook.Worksheet(1).Cell(1, 1).GetString() == "Smoke Report",
            "Excel report export could not be read back.");
    }
    var pdfHeader = await File.ReadAllBytesAsync(reportPdfPath);
    Require(pdfHeader.Length > 4 && pdfHeader[0] == (byte)'%' && pdfHeader[1] == (byte)'P'
            && pdfHeader[2] == (byte)'D' && pdfHeader[3] == (byte)'F',
        "PDF report export did not produce a valid PDF header.");

    Console.WriteLine("BatoBuzz smoke test passed.");
    Console.WriteLine($"Trial balance: Dr {trialBalance.TotalDebit:N2} / Cr {trialBalance.TotalCredit:N2}");
    Console.WriteLine($"Dashboard: Sales {dash.TotalSalesMonth:N2}, Purchases {dash.TotalPurchasesMonth:N2}, Receivables {dash.TotalReceivables:N2}, Payables {dash.TotalPayables:N2}");
}
finally
{
    SqliteConnection.ClearAllPools();
    foreach (var suffix in new[] { "", "-wal", "-shm" })
    {
        var path = dbPath + suffix;
        if (File.Exists(path))
            File.Delete(path);
    }
    var directory = Path.GetDirectoryName(dbPath)!;
    var prefix = Path.GetFileNameWithoutExtension(dbPath) + ".pre-upgrade-";
    foreach (var migrationBackupPath in Directory.EnumerateFiles(directory, prefix + "*.db"))
        File.Delete(migrationBackupPath);
    foreach (var testArtifact in new[] { backupPath, invalidBackupPath, reportExcelPath, reportPdfPath })
    {
        if (File.Exists(testArtifact))
            File.Delete(testArtifact);
    }
    if (Directory.Exists(automaticBackupDataDirectory))
        Directory.Delete(automaticBackupDataDirectory, recursive: true);
}

static async Task ExpectThrowsAsync<TException>(Func<Task> action, string message)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
