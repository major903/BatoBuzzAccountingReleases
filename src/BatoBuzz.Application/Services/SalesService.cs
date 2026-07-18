using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Common;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;
using BatoBuzz.Domain.Common;
using BatoBuzz.Domain.Entities;
using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Application.Services;

public class SalesService : ISalesService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AccountingPostingHelper _posting;

    public SalesService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _posting = new AccountingPostingHelper(unitOfWork);
    }

    public async Task<SalesInvoiceDto> CreateInvoiceAsync(CreateSalesInvoiceRequest request, Guid userId)
    {
        await ValidateCompanyBranchAsync(request.CompanyId, request.BranchId);
        _ = await GetCustomerForCompanyAsync(request.CompanyId, request.CustomerId);
        if (request.Lines.Count == 0)
            throw new InvalidOperationException("A sales invoice requires at least one line.");
        await ValidateSalesLineReferencesAsync(
            request.CompanyId,
            request.Lines.Select(l => (l.ItemId, l.WarehouseId, l.Description)));

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var invoiceNumber = await _unitOfWork.SalesInvoices.GetNextInvoiceNumberAsync(request.CompanyId);
            var invoice = SalesInvoice.Create(
                request.CompanyId, request.CustomerId, invoiceNumber,
                request.InvoiceDate, request.DueDate, userId,
                request.Reference, request.Narration, request.IsVatApplicable,
                request.VatRate, request.BranchId);

            foreach (var line in request.Lines)
            {
                var draftLine = invoice.AddLine(line.ItemId, line.Description, line.Quantity, line.Rate,
                    line.DiscountPercent, line.TaxPercent, line.WarehouseId);
                await _unitOfWork.SalesInvoices.AddLineAsync(draftLine);
            }

            await _unitOfWork.SalesInvoices.AddAsync(invoice);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return MapInvoiceToDto(invoice);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<SalesInvoiceDto> UpdateDraftInvoiceAsync(Guid invoiceId, CreateSalesInvoiceRequest request, Guid userId)
    {
        var invoice = await _unitOfWork.SalesInvoices.GetByIdWithDetailsAsync(invoiceId)
            ?? throw new InvalidOperationException("Invoice not found.");
        if (invoice.Status != InvoiceStatus.Draft || invoice.PostedJournalEntryId.HasValue)
            throw new InvalidOperationException("Only an unposted draft invoice can be edited.");

        await ValidateCompanyBranchAsync(invoice.CompanyId, request.BranchId);
        if (request.CompanyId != invoice.CompanyId)
            throw new InvalidOperationException("A draft invoice cannot be moved to another company.");
        _ = await GetCustomerForCompanyAsync(request.CompanyId, request.CustomerId);
        if (request.Lines.Count == 0)
            throw new InvalidOperationException("A sales invoice requires at least one line.");
        await ValidateSalesLineReferencesAsync(
            request.CompanyId,
            request.Lines.Select(l => (l.ItemId, l.WarehouseId, l.Description)));

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            invoice.UpdateDraft(
                request.CustomerId,
                request.InvoiceDate,
                request.DueDate,
                request.Reference,
                request.Narration,
                request.IsVatApplicable,
                request.VatRate,
                request.BranchId,
                userId);
            foreach (var line in request.Lines)
            {
                var draftLine = invoice.AddLine(line.ItemId, line.Description, line.Quantity, line.Rate,
                    line.DiscountPercent, line.TaxPercent, line.WarehouseId);
                await _unitOfWork.SalesInvoices.AddLineAsync(draftLine);
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return MapInvoiceToDto(invoice);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<SalesInvoiceDto> PostInvoiceAsync(Guid invoiceId, Guid userId)
    {
        var invoice = await _unitOfWork.SalesInvoices.GetByIdWithDetailsAsync(invoiceId)
            ?? throw new InvalidOperationException("Invoice not found.");

        var customer = await GetCustomerForCompanyAsync(invoice.CompanyId, invoice.CustomerId);
        await ValidateCompanyBranchAsync(invoice.CompanyId, invoice.BranchId);
        await ValidateSalesLineReferencesAsync(
            invoice.CompanyId,
            invoice.Lines.Select(l => (l.ItemId, l.WarehouseId, l.Description)));
        if (customer.CreditLimit > 0
            && Money.Round(customer.CurrentBalance + invoice.TotalAmount) > customer.CreditLimit)
            throw new InvalidOperationException(
                $"Posting this invoice would exceed {customer.Name}'s credit limit of {customer.CreditLimit:N2}.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _posting.EnsurePeriodOpenAsync(invoice.CompanyId, invoice.InvoiceDate);

            var salesLedger = await _posting.GetOrCreateSalesLedgerAsync(invoice.CompanyId);
            var vatLedger = invoice.VatAmount > 0
                ? await _posting.GetOrCreateSalesVatLedgerAsync(invoice.CompanyId)
                : null;

            invoice.Issue();
            customer.UpdateBalance(invoice.TotalAmount);
            var costOfGoodsSold = await RecordStockOutAsync(invoice);

            var lines = new List<PostingLine>
            {
                new(customer.LedgerId, invoice.TotalAmount, 0, $"Sales invoice {invoice.InvoiceNumber}"),
                new(salesLedger.Id, 0, invoice.TaxableAmount, $"Sales invoice {invoice.InvoiceNumber}")
            };

            if (vatLedger != null)
                lines.Add(new PostingLine(vatLedger.Id, 0, invoice.VatAmount, $"VAT on sales invoice {invoice.InvoiceNumber}", invoice.VatRate, "OUTPUT-VAT"));

            if (costOfGoodsSold > 0)
            {
                var costOfSalesLedger = await _posting.GetOrCreateCostOfSalesLedgerAsync(invoice.CompanyId);
                var inventoryLedger = await _posting.GetOrCreateInventoryLedgerAsync(invoice.CompanyId);
                lines.Add(new PostingLine(costOfSalesLedger.Id, costOfGoodsSold, 0, $"Cost of sales for invoice {invoice.InvoiceNumber}"));
                lines.Add(new PostingLine(inventoryLedger.Id, 0, costOfGoodsSold, $"Inventory relieved for invoice {invoice.InvoiceNumber}"));
            }

            var postedJournal = await _posting.CreateAndPostJournalAsync(
                invoice.CompanyId,
                invoice.InvoiceDate,
                VoucherType.Sales,
                userId,
                invoice.InvoiceNumber,
                invoice.Narration ?? $"Sales invoice {invoice.InvoiceNumber}",
                invoice.BranchId,
                lines);
            invoice.AttachPostedJournal(postedJournal.Id);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync();
            return MapInvoiceToDto(invoice);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ReceiptDto> RecordReceiptAsync(CreateReceiptRequest request, Guid userId)
    {
        if (request.ReceiptDate == default)
            throw new InvalidOperationException("Receipt date is required.");
        var receiptAmount = Money.Round(request.Amount);
        if (receiptAmount <= 0 || receiptAmount > 9_999_999_999_999_999.99m)
            throw new InvalidOperationException("Receipt amount must be positive and within the supported range.");
        if (!Enum.IsDefined(typeof(PaymentMethod), request.PaymentMethod))
            throw new InvalidOperationException("Payment method is invalid.");

        var customer = await GetCustomerForCompanyAsync(request.CompanyId, request.CustomerId);
        var allocationInvoices = new Dictionary<Guid, SalesInvoice>();
        var allocationAmounts = new Dictionary<Guid, decimal>();
        decimal allocatedAmount = 0;
        foreach (var allocation in request.Allocations)
        {
            var allocationAmount = Money.Round(allocation.AmountAllocated);
            if (allocationAmount <= 0)
                throw new InvalidOperationException("Receipt allocation amounts must be greater than zero.");
            if (!allocationInvoices.TryAdd(
                    allocation.SalesInvoiceId,
                    await _unitOfWork.SalesInvoices.GetByIdWithDetailsAsync(allocation.SalesInvoiceId)
                        ?? throw new InvalidOperationException("Sales invoice allocation target not found.")))
                throw new InvalidOperationException("Duplicate allocations for the same sales invoice are not allowed.");

            var invoice = allocationInvoices[allocation.SalesInvoiceId];
            if (invoice.CompanyId != request.CompanyId || invoice.CustomerId != request.CustomerId)
                throw new InvalidOperationException("Receipt allocation target belongs to another company or customer.");
            if (invoice.Status is not (InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Overdue))
                throw new InvalidOperationException("Receipt allocations require an issued, partially paid, or overdue invoice.");
            if (allocationAmount > invoice.BalanceDue)
                throw new InvalidOperationException("Receipt allocation exceeds the invoice balance due.");

            allocationAmounts.Add(allocation.SalesInvoiceId, allocationAmount);
            allocatedAmount = Money.Round(allocatedAmount + allocationAmount);
        }

        if (allocatedAmount > receiptAmount)
            throw new InvalidOperationException("Receipt allocations exceed the receipt amount.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _posting.EnsurePeriodOpenAsync(request.CompanyId, request.ReceiptDate);

            var settlementLedger = await _posting.GetOrCreateSettlementLedgerAsync(
                request.CompanyId,
                (PaymentMethod)request.PaymentMethod,
                request.BankName);

            var receiptNumber = await _unitOfWork.Receipts.GetNextReceiptNumberAsync(request.CompanyId);
            var receipt = Receipt.Create(
                request.CompanyId, request.CustomerId, receiptNumber,
                request.ReceiptDate, receiptAmount, userId,
                request.Narration, (PaymentMethod)request.PaymentMethod,
                request.ChequeNumber, request.ChequeDate, request.BankName,
                request.Reference, request.IsAdvance);

            foreach (var alloc in request.Allocations)
            {
                var allocationAmount = allocationAmounts[alloc.SalesInvoiceId];
                receipt.AllocateToInvoice(alloc.SalesInvoiceId, allocationAmount);
                allocationInvoices[alloc.SalesInvoiceId].RecordReceipt(allocationAmount);
            }

            await _unitOfWork.Receipts.AddAsync(receipt);

            customer.UpdateBalance(-receiptAmount);

            var postedJournal = await _posting.CreateAndPostJournalAsync(
                request.CompanyId,
                request.ReceiptDate,
                VoucherType.Receipt,
                userId,
                receiptNumber,
                request.Narration ?? $"Receipt {receiptNumber}",
                null,
                new[]
                {
                    new PostingLine(settlementLedger.Id, receiptAmount, 0, $"Receipt {receiptNumber}"),
                    new PostingLine(customer.LedgerId, 0, receiptAmount, $"Receipt from {customer.Name}")
                });
            receipt.AttachPostedJournal(postedJournal.Id);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync();
            return MapPaymentToReceiptDto(receipt, customer.Name);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<SalesInvoiceDto> CancelInvoiceAsync(
        Guid invoiceId, CorrectPostedDocumentRequest request, Guid userId)
    {
        ValidateCorrection(request);
        var invoice = await _unitOfWork.SalesInvoices.GetByIdWithDetailsAsync(invoiceId)
            ?? throw new InvalidOperationException("Invoice not found.");
        if (request.CorrectionDate.Date < invoice.InvoiceDate.Date)
            throw new InvalidOperationException("Correction date cannot be before the invoice date.");
        var journal = invoice.PostedJournalEntry
            ?? throw new InvalidOperationException("The invoice is not linked to its posted journal and cannot be cancelled safely.");
        var customer = await GetCustomerForCompanyAsync(invoice.CompanyId, invoice.CustomerId);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _posting.EnsurePeriodOpenAsync(invoice.CompanyId, request.CorrectionDate);
            invoice.Cancel(userId);
            customer.UpdateBalance(-invoice.TotalAmount);
            await RestoreInvoiceStockAsync(invoice, request.CorrectionDate, request.Reason);
            await _posting.ReversePostedJournalWithinCurrentTransactionAsync(
                journal, request.CorrectionDate, request.Reason, userId);
            var auditUser = await _unitOfWork.Users.GetByIdAsync(userId);
            await _unitOfWork.AuditLogs.AddAsync(AuditLog.Create(
                "SalesInvoice.Cancelled", nameof(SalesInvoice), invoice.Id, invoice.CompanyId,
                userId, userName: auditUser?.UserName ?? userId.ToString(), newValues: request.Reason.Trim()));
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return MapInvoiceToDto(invoice);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<OperationalNoteDto> IssueCreditNoteAsync(
        Guid invoiceId, CorrectPostedDocumentRequest request, Guid userId)
    {
        ValidateCorrection(request);
        var invoice = await _unitOfWork.SalesInvoices.GetByIdWithDetailsAsync(invoiceId)
            ?? throw new InvalidOperationException("Invoice not found.");
        if (request.CorrectionDate.Date < invoice.InvoiceDate.Date)
            throw new InvalidOperationException("Credit-note date cannot be before the invoice date.");
        if (!invoice.PostedJournalEntryId.HasValue)
            throw new InvalidOperationException("The invoice has not been posted and cannot receive a credit note.");
        if (request.ReturnPercent <= 0 || request.ReturnPercent > 100)
            throw new InvalidOperationException("Return percent must be greater than zero and no more than 100.");
        var customer = await GetCustomerForCompanyAsync(invoice.CompanyId, invoice.CustomerId);
        var returnFactor = request.ReturnPercent / 100m;
        var taxableAmount = Money.Round(invoice.TaxableAmount * returnFactor);
        var vatAmount = Money.Round(invoice.VatAmount * returnFactor);
        var returnAmount = Money.Round(taxableAmount + vatAmount);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _posting.EnsurePeriodOpenAsync(invoice.CompanyId, request.CorrectionDate);
            invoice.IssueCreditNote(returnAmount, userId);
            customer.UpdateBalance(-returnAmount);
            var stockCostReturned = await RestoreInvoiceStockAsync(
                invoice, request.CorrectionDate, request.Reason, "CreditNote", returnFactor);
            var salesLedger = await _posting.GetOrCreateSalesLedgerAsync(invoice.CompanyId);
            var lines = new List<PostingLine>
            {
                new(salesLedger.Id, taxableAmount, 0, $"Credit note for {invoice.InvoiceNumber}"),
                new(customer.LedgerId, 0, returnAmount, $"Credit note for {invoice.InvoiceNumber}")
            };
            if (vatAmount > 0)
            {
                var vatLedger = await _posting.GetOrCreateSalesVatLedgerAsync(invoice.CompanyId);
                lines.Add(new PostingLine(vatLedger.Id, vatAmount, 0,
                    $"VAT reversal for credit note {invoice.InvoiceNumber}", invoice.VatRate, "OUTPUT-VAT"));
            }
            if (stockCostReturned > 0)
            {
                var inventoryLedger = await _posting.GetOrCreateInventoryLedgerAsync(invoice.CompanyId);
                var costOfSalesLedger = await _posting.GetOrCreateCostOfSalesLedgerAsync(invoice.CompanyId);
                lines.Add(new PostingLine(inventoryLedger.Id, stockCostReturned, 0, $"Inventory returned from {invoice.InvoiceNumber}"));
                lines.Add(new PostingLine(costOfSalesLedger.Id, 0, stockCostReturned, $"Cost reversal for {invoice.InvoiceNumber}"));
            }

            var noteNumber = await _unitOfWork.SalesInvoices.GetNextCreditNoteNumberAsync(invoice.CompanyId);
            var journal = await _posting.CreateAndPostJournalAsync(
                invoice.CompanyId, request.CorrectionDate, VoucherType.CreditNote, userId, noteNumber,
                $"Credit note {noteNumber} for {invoice.InvoiceNumber}: {request.Reason.Trim()}", invoice.BranchId, lines);
            var auditUser = await _unitOfWork.Users.GetByIdAsync(userId);
            await _unitOfWork.AuditLogs.AddAsync(AuditLog.Create(
                "SalesInvoice.CreditNoteIssued", nameof(SalesInvoice), invoice.Id, invoice.CompanyId,
                userId, userName: auditUser?.UserName ?? userId.ToString(), newValues: noteNumber));
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return new OperationalNoteDto
            {
                JournalEntryId = journal.Id,
                NoteNumber = noteNumber,
                NoteDate = request.CorrectionDate.Date,
                Amount = returnAmount,
                SourceDocumentNumber = invoice.InvoiceNumber
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task DeleteDraftInvoiceAsync(Guid invoiceId, Guid userId)
    {
        var invoice = await _unitOfWork.SalesInvoices.GetByIdWithDetailsAsync(invoiceId)
            ?? throw new InvalidOperationException("Invoice not found.");
        if (invoice.Status != InvoiceStatus.Draft || invoice.PostedJournalEntryId.HasValue)
            throw new InvalidOperationException("Only an unposted draft invoice can be discarded.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var auditUser = await _unitOfWork.Users.GetByIdAsync(userId);
            await _unitOfWork.SalesInvoices.DeleteAsync(invoice);
            await _unitOfWork.AuditLogs.AddAsync(AuditLog.Create(
                "SalesInvoice.DraftDiscarded", nameof(SalesInvoice), invoice.Id, invoice.CompanyId,
                userId, userName: auditUser?.UserName ?? userId.ToString(), newValues: invoice.InvoiceNumber));
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ReceiptDto> ReverseReceiptAsync(
        Guid receiptId, CorrectPostedDocumentRequest request, Guid userId)
    {
        ValidateCorrection(request);
        var receipt = await _unitOfWork.Receipts.GetByIdWithDetailsAsync(receiptId)
            ?? throw new InvalidOperationException("Receipt not found.");
        if (request.CorrectionDate.Date < receipt.ReceiptDate.Date)
            throw new InvalidOperationException("Correction date cannot be before the receipt date.");
        var journal = receipt.PostedJournalEntry
            ?? throw new InvalidOperationException("The receipt is not linked to its posted journal and cannot be reversed safely.");
        var customer = await GetCustomerForCompanyAsync(receipt.CompanyId, receipt.CustomerId);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _posting.EnsurePeriodOpenAsync(receipt.CompanyId, request.CorrectionDate);
            foreach (var allocation in receipt.Allocations)
            {
                var invoice = await _unitOfWork.SalesInvoices.GetByIdWithDetailsAsync(allocation.SalesInvoiceId)
                    ?? throw new InvalidOperationException("A receipt allocation invoice was not found.");
                if (invoice.CompanyId != receipt.CompanyId || invoice.CustomerId != receipt.CustomerId)
                    throw new InvalidOperationException("A receipt allocation does not belong to the receipt company and customer.");
                invoice.UnapplyReceipt(allocation.AmountAllocated, request.CorrectionDate);
            }

            customer.UpdateBalance(receipt.Amount);
            await _posting.ReversePostedJournalWithinCurrentTransactionAsync(
                journal, request.CorrectionDate, request.Reason, userId);
            var auditUser = await _unitOfWork.Users.GetByIdAsync(userId);
            await _unitOfWork.AuditLogs.AddAsync(AuditLog.Create(
                "Receipt.Reversed", nameof(Receipt), receipt.Id, receipt.CompanyId,
                userId, userName: auditUser?.UserName ?? userId.ToString(), newValues: request.Reason.Trim()));
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return MapPaymentToReceiptDto(receipt, customer.Name);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<SalesInvoiceDto>> GetInvoicesAsync(Guid companyId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var invoices = await _unitOfWork.SalesInvoices.GetByCompanyAsync(companyId, fromDate, toDate);
        return invoices.Select(MapInvoiceToDto).ToList();
    }

    public async Task<IReadOnlyList<ReceiptDto>> GetReceiptsAsync(
        Guid companyId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var receipts = await _unitOfWork.Receipts.GetByCompanyAsync(companyId, fromDate, toDate);
        return receipts.Select(receipt => MapPaymentToReceiptDto(receipt, receipt.Customer.Name)).ToList();
    }

    public async Task<IReadOnlyList<AgeingItemDto>> GetReceivablesAgeingAsync(Guid companyId, DateTime asOfDate)
    {
        if (asOfDate == default)
            throw new InvalidOperationException("As-of date is required.");

        var customers = await _unitOfWork.Customers.GetByCompanyAsync(companyId);
        var invoices = await _unitOfWork.SalesInvoices.GetByCompanyAsync(companyId);

        var result = new List<AgeingItemDto>();

        foreach (var customer in customers)
        {
            var receipts = await _unitOfWork.Receipts.GetByCustomerAsync(customer.Id);

            var customerInvoices = invoices.Where(i => i.CustomerId == customer.Id
                && i.InvoiceDate.Date <= asOfDate.Date
                && i.Status is InvoiceStatus.Issued or InvoiceStatus.PartiallyPaid or InvoiceStatus.Paid or InvoiceStatus.Overdue);
            decimal current = 0, d1to30 = 0, d31to60 = 0, d61to90 = 0, over90 = 0;

            foreach (var inv in customerInvoices)
            {
                var daysOverdue = (asOfDate - inv.DueDate).Days;
                var allocatedThroughDate = receipts
                    .Where(receipt => receipt.ReceiptDate.Date <= asOfDate.Date)
                    .SelectMany(receipt => receipt.Allocations)
                    .Where(allocation => allocation.SalesInvoiceId == inv.Id)
                    .Sum(allocation => allocation.AmountAllocated);
                var balance = Money.Round(inv.TotalAmount - allocatedThroughDate);
                if (balance <= 0)
                    continue;

                if (daysOverdue <= 0) current += balance;
                else if (daysOverdue <= 30) d1to30 += balance;
                else if (daysOverdue <= 60) d31to60 += balance;
                else if (daysOverdue <= 90) d61to90 += balance;
                else over90 += balance;
            }

            if (current + d1to30 + d31to60 + d61to90 + over90 > 0)
            {
                result.Add(new AgeingItemDto
                {
                    ContactId = customer.Id,
                    ContactName = customer.Name,
                    CurrentAmount = current,
                    Days1To30 = d1to30,
                    Days31To60 = d31to60,
                    Days61To90 = d61to90,
                    Over90Days = over90,
                    TotalAmount = current + d1to30 + d31to60 + d61to90 + over90
                });
            }
        }

        return result;
    }

    private static SalesInvoiceDto MapInvoiceToDto(SalesInvoice inv) => new()
    {
        Id = inv.Id,
        InvoiceNumber = inv.InvoiceNumber,
        InvoiceDate = inv.InvoiceDate,
        DueDate = inv.DueDate,
        CustomerId = inv.CustomerId,
        CustomerName = inv.Customer?.Name ?? string.Empty,
        Reference = inv.Reference,
        Narration = inv.Narration,
        IsVatApplicable = inv.IsVatApplicable,
        VatRate = inv.VatRate,
        SubTotal = inv.SubTotal,
        DiscountAmount = inv.DiscountAmount,
        VatAmount = inv.VatAmount,
        TotalAmount = inv.TotalAmount,
        AmountReceived = inv.AmountReceived,
        BalanceDue = inv.BalanceDue,
        Status = (InvoiceStatusDto)inv.Status,
        PostedJournalEntryId = inv.PostedJournalEntryId,
        CorrectionDate = inv.PostedJournalEntry?.ReversalJournalEntry?.EntryDate,
        CorrectionReason = inv.PostedJournalEntry?.ReversalReason,
        Lines = inv.Lines.Select(l => new SalesInvoiceLineDto
        {
            Id = l.Id, ItemId = l.ItemId, WarehouseId = l.WarehouseId,
            Description = l.Description, Quantity = l.Quantity, Rate = l.Rate,
            DiscountPercent = l.DiscountPercent, TaxPercent = l.TaxPercent,
            DiscountAmount = l.DiscountAmount, TaxAmount = l.TaxAmount, LineTotal = l.LineTotal
        }).ToList()
    };

    private static ReceiptDto MapPaymentToReceiptDto(Receipt r, string customerName) => new()
    {
        Id = r.Id,
        ReceiptNumber = r.ReceiptNumber,
        ReceiptDate = r.ReceiptDate,
        CustomerId = r.CustomerId,
        CustomerName = customerName,
        Amount = r.Amount,
        PaymentMethod = (PaymentMethodDto)r.PaymentMethod,
        PostedJournalEntryId = r.PostedJournalEntryId,
        Status = (TransactionStatusDto)(r.PostedJournalEntry?.Status ?? TransactionStatus.Draft),
        CorrectionDate = r.PostedJournalEntry?.ReversalJournalEntry?.EntryDate,
        CorrectionReason = r.PostedJournalEntry?.ReversalReason
    };

    private static void ValidateCorrection(CorrectPostedDocumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CorrectionDate == default)
            throw new InvalidOperationException("Correction date is required.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("A correction reason is required.");
        if (request.Reason.Trim().Length > 500)
            throw new InvalidOperationException("Correction reason cannot exceed 500 characters.");
    }

    private async Task ValidateCompanyBranchAsync(Guid companyId, Guid? branchId)
    {
        if (companyId == Guid.Empty)
            throw new InvalidOperationException("Company is required.");

        var company = await _unitOfWork.Companies.GetByIdWithDetailsAsync(companyId)
            ?? throw new InvalidOperationException("Company not found.");
        if (!company.IsActive)
            throw new InvalidOperationException("Company is inactive.");
        if (branchId.HasValue && company.Branches.All(b => b.Id != branchId.Value || !b.IsActive))
            throw new InvalidOperationException("Branch does not belong to the selected company or is inactive.");
    }

    private async Task<Customer> GetCustomerForCompanyAsync(Guid companyId, Guid customerId)
    {
        var customer = await _unitOfWork.Customers.GetByIdWithLedgerAsync(customerId)
            ?? throw new InvalidOperationException("Customer not found.");
        if (customer.CompanyId != companyId || customer.Ledger.CompanyId != companyId)
            throw new InvalidOperationException("Customer does not belong to the selected company.");
        if (!customer.IsActive || !customer.Ledger.IsActive)
            throw new InvalidOperationException("Customer or customer ledger is inactive.");
        return customer;
    }

    private async Task ValidateSalesLineReferencesAsync(
        Guid companyId,
        IEnumerable<(Guid? ItemId, Guid? WarehouseId, string Description)> lines)
    {
        var lineList = lines.ToList();
        var warehouses = (await _unitOfWork.Warehouses.GetByCompanyAsync(companyId)).Where(w => w.IsActive).ToList();
        var warehouseIds = warehouses.Select(w => w.Id).ToHashSet();

        foreach (var line in lineList)
        {
            if (line.WarehouseId.HasValue && !warehouseIds.Contains(line.WarehouseId.Value))
                throw new InvalidOperationException($"Warehouse for line '{line.Description}' does not belong to the selected company.");
            if (!line.ItemId.HasValue)
            {
                if (line.WarehouseId.HasValue)
                    throw new InvalidOperationException("A warehouse can only be specified for an item line.");
                continue;
            }

            var item = await _unitOfWork.Items.GetByIdWithDetailsAsync(line.ItemId.Value)
                ?? throw new InvalidOperationException($"Item for line '{line.Description}' was not found.");
            if (item.CompanyId != companyId)
                throw new InvalidOperationException($"Item for line '{line.Description}' does not belong to the selected company.");
            if (!item.IsActive)
                throw new InvalidOperationException($"Item for line '{line.Description}' is inactive.");
            if (item.ItemType == ItemType.Goods && item.AllowNegativeStock)
                throw new InvalidOperationException("Negative inventory is not supported with weighted-average costing.");
            if (item.ItemType == ItemType.Goods && !line.WarehouseId.HasValue && warehouses.Count == 0)
                throw new InvalidOperationException("A warehouse is required for stock item posting.");
            if (item.ItemType != ItemType.Goods && line.WarehouseId.HasValue)
                throw new InvalidOperationException("A warehouse cannot be assigned to a non-stock item.");
        }
    }

    private async Task<decimal> RecordStockOutAsync(SalesInvoice invoice)
    {
        decimal totalCostRemoved = 0;
        foreach (var line in invoice.Lines.Where(l => l.ItemId.HasValue && l.Quantity > 0))
        {
            var item = await _unitOfWork.Items.GetByIdWithDetailsAsync(line.ItemId!.Value)
                ?? throw new InvalidOperationException($"Item for invoice line '{line.Description}' was not found.");
            if (item.CompanyId != invoice.CompanyId)
                throw new InvalidOperationException("Invoice item does not belong to the invoice company.");

            if (item.ItemType != ItemType.Goods)
                continue;
            if (item.AllowNegativeStock)
                throw new InvalidOperationException("Negative inventory is not supported with weighted-average costing.");

            var warehouseId = line.WarehouseId ?? await GetDefaultWarehouseIdAsync(invoice.CompanyId);
            var companyWarehouses = (await _unitOfWork.Warehouses.GetByCompanyAsync(invoice.CompanyId)).Where(w => w.IsActive).ToList();
            if (companyWarehouses.All(w => w.Id != warehouseId))
                throw new InvalidOperationException("Invoice warehouse does not belong to the invoice company.");

            await _posting.EnsureStockDateIsNotBackdatedAsync(
                item.Id, warehouseId, invoice.InvoiceDate, "post this sales invoice");

            var balance = await _unitOfWork.StockBalances.GetByItemWarehouseAsync(item.Id, warehouseId);
            if (balance == null)
            {
                balance = StockBalance.Create(invoice.CompanyId, item.Id, warehouseId);
                await _unitOfWork.StockBalances.AddAsync(balance);
            }
            else if (balance.CompanyId != invoice.CompanyId)
                throw new InvalidOperationException("Stock balance does not belong to the invoice company.");

            var unitCost = balance.AverageCost;
            var valueBeforeRemoval = balance.TotalValue;
            balance.RemoveStock(line.Quantity, unitCost, allowNegativeStock: false);
            var costRemoved = Money.Round(valueBeforeRemoval - balance.TotalValue);
            totalCostRemoved = Money.Round(totalCostRemoved + costRemoved);

            var movement = StockMovement.Create(
                invoice.CompanyId,
                item.Id,
                warehouseId,
                MovementType.SaleOut,
                line.Quantity,
                unitCost,
                balance.Quantity,
                balance.TotalValue,
                invoice.InvoiceDate,
                invoice.Id,
                "SalesInvoice",
                narration: $"Sales invoice {invoice.InvoiceNumber}: {line.Description}");

            await _unitOfWork.StockMovements.AddAsync(movement);
        }

        return totalCostRemoved;
    }

    private async Task<decimal> RestoreInvoiceStockAsync(
        SalesInvoice invoice, DateTime correctionDate, string reason, string sourceDocumentType = "SalesInvoiceCancellation", decimal returnFactor = 1m)
    {
        decimal totalCostReturned = 0;
        var movements = await _unitOfWork.StockMovements.GetBySourceDocumentAsync(
            invoice.CompanyId, invoice.Id, "SalesInvoice");
        foreach (var movement in movements.Where(m => m.MovementType == MovementType.SaleOut))
        {
            await _posting.EnsureStockDateIsNotBackdatedAsync(
                movement.ItemId, movement.WarehouseId, correctionDate, "cancel this sales invoice");
            var balance = await _unitOfWork.StockBalances.GetByItemWarehouseAsync(
                movement.ItemId, movement.WarehouseId)
                ?? throw new InvalidOperationException("The stock balance needed to cancel this invoice was not found.");
            var quantity = Money.Round(movement.Quantity * returnFactor);
            var totalCost = Money.Round(movement.TotalCost * returnFactor);
            if (quantity <= 0)
                continue;
            balance.AddStockAtValue(quantity, totalCost);
            var returnMovement = StockMovement.Create(
                invoice.CompanyId,
                movement.ItemId,
                movement.WarehouseId,
                MovementType.SalesReturn,
                quantity,
                quantity == 0 ? 0 : totalCost / quantity,
                balance.Quantity,
                balance.TotalValue,
                correctionDate,
                invoice.Id,
                sourceDocumentType,
                narration: $"{(sourceDocumentType == "CreditNote" ? "Credit note" : "Cancellation")} of {invoice.InvoiceNumber}: {reason.Trim()}",
                totalCostOverride: totalCost);
            await _unitOfWork.StockMovements.AddAsync(returnMovement);
            totalCostReturned = Money.Round(totalCostReturned + totalCost);
        }
        return totalCostReturned;
    }

    private async Task<Guid> GetDefaultWarehouseIdAsync(Guid companyId)
    {
        var warehouses = (await _unitOfWork.Warehouses.GetByCompanyAsync(companyId)).Where(w => w.IsActive).ToList();
        return warehouses.FirstOrDefault(w => w.IsDefault)?.Id
            ?? warehouses.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("A warehouse is required for stock item posting.");
    }
}
