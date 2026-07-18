using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Common;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;
using BatoBuzz.Domain.Common;
using BatoBuzz.Domain.Entities;
using BatoBuzz.Domain.Enums;

namespace BatoBuzz.Application.Services;

public class PurchaseService : IPurchaseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AccountingPostingHelper _posting;

    public PurchaseService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
        _posting = new AccountingPostingHelper(unitOfWork);
    }

    public async Task<PurchaseBillDto> CreateBillAsync(CreatePurchaseBillRequest request, Guid userId)
    {
        await ValidateCompanyBranchAsync(request.CompanyId, request.BranchId);
        _ = await GetSupplierForCompanyAsync(request.CompanyId, request.SupplierId);
        if (request.Lines.Count == 0)
            throw new InvalidOperationException("A purchase bill requires at least one line.");
        await ValidatePurchaseLineReferencesAsync(
            request.CompanyId,
            request.Lines.Select(l => (l.ItemId, l.WarehouseId, l.Description)));

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var billNumber = await _unitOfWork.PurchaseBills.GetNextBillNumberAsync(request.CompanyId);
            var bill = PurchaseBill.Create(
                request.CompanyId, request.SupplierId, billNumber,
                request.BillDate, request.DueDate, userId,
                request.SupplierInvoiceNumber, request.Reference, request.Narration,
                request.IsVatApplicable, request.VatRate, request.BranchId);

            foreach (var line in request.Lines)
            {
                var draftLine = bill.AddLine(line.ItemId, line.Description, line.Quantity, line.Rate,
                    line.DiscountPercent, line.TaxPercent, line.WarehouseId);
                await _unitOfWork.PurchaseBills.AddLineAsync(draftLine);
            }

            await _unitOfWork.PurchaseBills.AddAsync(bill);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return MapBillToDto(bill);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<PurchaseBillDto> UpdateDraftBillAsync(Guid billId, CreatePurchaseBillRequest request, Guid userId)
    {
        var bill = await _unitOfWork.PurchaseBills.GetByIdWithDetailsAsync(billId)
            ?? throw new InvalidOperationException("Purchase bill not found.");
        if (bill.Status != BillStatus.Draft || bill.PostedJournalEntryId.HasValue)
            throw new InvalidOperationException("Only an unposted draft bill can be edited.");

        await ValidateCompanyBranchAsync(bill.CompanyId, request.BranchId);
        if (request.CompanyId != bill.CompanyId)
            throw new InvalidOperationException("A draft bill cannot be moved to another company.");
        _ = await GetSupplierForCompanyAsync(request.CompanyId, request.SupplierId);
        if (request.Lines.Count == 0)
            throw new InvalidOperationException("A purchase bill requires at least one line.");
        await ValidatePurchaseLineReferencesAsync(
            request.CompanyId,
            request.Lines.Select(l => (l.ItemId, l.WarehouseId, l.Description)));

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            bill.UpdateDraft(
                request.SupplierId,
                request.BillDate,
                request.DueDate,
                request.SupplierInvoiceNumber,
                request.Reference,
                request.Narration,
                request.IsVatApplicable,
                request.VatRate,
                request.BranchId,
                userId);
            foreach (var line in request.Lines)
            {
                var draftLine = bill.AddLine(line.ItemId, line.Description, line.Quantity, line.Rate,
                    line.DiscountPercent, line.TaxPercent, line.WarehouseId);
                await _unitOfWork.PurchaseBills.AddLineAsync(draftLine);
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return MapBillToDto(bill);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<PurchaseBillDto> PostBillAsync(Guid billId, Guid userId)
    {
        var bill = await _unitOfWork.PurchaseBills.GetByIdWithDetailsAsync(billId)
            ?? throw new InvalidOperationException("Purchase bill not found.");

        var supplier = await GetSupplierForCompanyAsync(bill.CompanyId, bill.SupplierId);
        await ValidateCompanyBranchAsync(bill.CompanyId, bill.BranchId);
        await ValidatePurchaseLineReferencesAsync(
            bill.CompanyId,
            bill.Lines.Select(l => (l.ItemId, l.WarehouseId, l.Description)));

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _posting.EnsurePeriodOpenAsync(bill.CompanyId, bill.BillDate);

            var vatLedger = bill.VatAmount > 0
                ? await _posting.GetOrCreatePurchaseVatLedgerAsync(bill.CompanyId)
                : null;

            bill.Receive();
            supplier.UpdateBalance(bill.TotalAmount);
            var inventoryCost = await RecordStockInAsync(bill);

            var lines = new List<PostingLine>();
            if (inventoryCost > 0)
            {
                var inventoryLedger = await _posting.GetOrCreateInventoryLedgerAsync(bill.CompanyId);
                lines.Add(new PostingLine(inventoryLedger.Id, inventoryCost, 0, $"Inventory on purchase bill {bill.BillNumber}"));
            }

            var nonInventoryCost = Money.Round(bill.TaxableAmount - inventoryCost);
            if (nonInventoryCost < 0)
                throw new InvalidOperationException("Inventory valuation exceeds the bill taxable amount.");
            if (nonInventoryCost > 0)
            {
                var purchaseLedger = await _posting.GetOrCreatePurchaseLedgerAsync(bill.CompanyId);
                lines.Add(new PostingLine(purchaseLedger.Id, nonInventoryCost, 0, $"Non-stock purchase bill {bill.BillNumber}"));
            }

            if (vatLedger != null)
                lines.Add(new PostingLine(vatLedger.Id, bill.VatAmount, 0, $"VAT on purchase bill {bill.BillNumber}", bill.VatRate, "INPUT-VAT"));

            lines.Add(new PostingLine(supplier.LedgerId, 0, bill.TotalAmount, $"Purchase bill {bill.BillNumber}"));

            var postedJournal = await _posting.CreateAndPostJournalAsync(
                bill.CompanyId,
                bill.BillDate,
                VoucherType.Purchase,
                userId,
                bill.SupplierInvoiceNumber ?? bill.BillNumber,
                bill.Narration ?? $"Purchase bill {bill.BillNumber}",
                bill.BranchId,
                lines);
            bill.AttachPostedJournal(postedJournal.Id);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync();
            return MapBillToDto(bill);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<PaymentDto> RecordPaymentAsync(CreatePaymentRequest request, Guid userId)
    {
        if (request.PaymentDate == default)
            throw new InvalidOperationException("Payment date is required.");
        var paymentAmount = Money.Round(request.Amount);
        var tdsAmount = Money.Round(request.TdsAmount);
        if (paymentAmount <= 0 || paymentAmount > 9_999_999_999_999_999.99m)
            throw new InvalidOperationException("Payment amount must be positive and within the supported range.");
        if (tdsAmount < 0)
            throw new InvalidOperationException("TDS amount cannot be negative.");
        if (!Enum.IsDefined(typeof(PaymentMethod), request.PaymentMethod))
            throw new InvalidOperationException("Payment method is invalid.");

        var totalSettled = Money.Round(paymentAmount + tdsAmount);
        if (totalSettled > 9_999_999_999_999_999.99m)
            throw new InvalidOperationException("Total settlement amount exceeds the supported range.");

        var supplier = await GetSupplierForCompanyAsync(request.CompanyId, request.SupplierId);
        var allocationBills = new Dictionary<Guid, PurchaseBill>();
        var allocationAmounts = new Dictionary<Guid, decimal>();
        decimal allocatedAmount = 0;
        foreach (var allocation in request.Allocations)
        {
            var allocationAmount = Money.Round(allocation.AmountAllocated);
            if (allocationAmount <= 0)
                throw new InvalidOperationException("Payment allocation amounts must be greater than zero.");
            if (!allocationBills.TryAdd(
                    allocation.PurchaseBillId,
                    await _unitOfWork.PurchaseBills.GetByIdWithDetailsAsync(allocation.PurchaseBillId)
                        ?? throw new InvalidOperationException("Purchase bill allocation target not found.")))
                throw new InvalidOperationException("Duplicate allocations for the same purchase bill are not allowed.");

            var bill = allocationBills[allocation.PurchaseBillId];
            if (bill.CompanyId != request.CompanyId || bill.SupplierId != request.SupplierId)
                throw new InvalidOperationException("Payment allocation target belongs to another company or supplier.");
            if (bill.Status is not (BillStatus.Received or BillStatus.PartiallyPaid or BillStatus.Overdue))
                throw new InvalidOperationException("Payment allocations require a received, partially paid, or overdue bill.");
            if (allocationAmount > bill.BalanceDue)
                throw new InvalidOperationException("Payment allocation exceeds the bill balance due.");

            allocationAmounts.Add(allocation.PurchaseBillId, allocationAmount);
            allocatedAmount = Money.Round(allocatedAmount + allocationAmount);
        }

        if (allocatedAmount > totalSettled)
            throw new InvalidOperationException("Payment allocations exceed the total settlement amount.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _posting.EnsurePeriodOpenAsync(request.CompanyId, request.PaymentDate);

            var settlementLedger = await _posting.GetOrCreateSettlementLedgerAsync(
                request.CompanyId,
                (PaymentMethod)request.PaymentMethod,
                request.BankName);

            var paymentNumber = await _unitOfWork.Payments.GetNextPaymentNumberAsync(request.CompanyId);
            var payment = Payment.Create(
                request.CompanyId, request.SupplierId, paymentNumber,
                request.PaymentDate, paymentAmount, userId,
                request.Narration, (PaymentMethod)request.PaymentMethod,
                request.ChequeNumber, request.ChequeDate, request.BankName,
                request.Reference, request.IsAdvance, tdsAmount);

            foreach (var alloc in request.Allocations)
            {
                var allocationAmount = allocationAmounts[alloc.PurchaseBillId];
                payment.AllocateToBill(alloc.PurchaseBillId, allocationAmount);
                allocationBills[alloc.PurchaseBillId].RecordPayment(allocationAmount);
            }

            await _unitOfWork.Payments.AddAsync(payment);

            // The supplier's payable is relieved by cash paid AND any TDS withheld on their behalf.
            supplier.UpdateBalance(-totalSettled);

            var lines = new List<PostingLine>
            {
                new(supplier.LedgerId, totalSettled, 0, $"Payment to {supplier.Name}"),
                new(settlementLedger.Id, 0, paymentAmount, $"Payment {paymentNumber}")
            };

            if (tdsAmount > 0)
            {
                var tdsLedger = await _posting.GetOrCreateTdsPayableLedgerAsync(request.CompanyId);
                lines.Add(new PostingLine(tdsLedger.Id, 0, tdsAmount, $"TDS withheld on payment {paymentNumber}"));
            }

            var postedJournal = await _posting.CreateAndPostJournalAsync(
                request.CompanyId,
                request.PaymentDate,
                VoucherType.Payment,
                userId,
                paymentNumber,
                request.Narration ?? $"Payment {paymentNumber}",
                null,
                lines);
            payment.AttachPostedJournal(postedJournal.Id);
            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync();
            return MapPaymentToDto(payment, supplier.Name);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<PurchaseBillDto> CancelBillAsync(
        Guid billId, CorrectPostedDocumentRequest request, Guid userId)
    {
        ValidateCorrection(request);
        var bill = await _unitOfWork.PurchaseBills.GetByIdWithDetailsAsync(billId)
            ?? throw new InvalidOperationException("Purchase bill not found.");
        if (request.CorrectionDate.Date < bill.BillDate.Date)
            throw new InvalidOperationException("Correction date cannot be before the bill date.");
        var journal = bill.PostedJournalEntry
            ?? throw new InvalidOperationException("The purchase bill is not linked to its posted journal and cannot be cancelled safely.");
        var supplier = await GetSupplierForCompanyAsync(bill.CompanyId, bill.SupplierId);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _posting.EnsurePeriodOpenAsync(bill.CompanyId, request.CorrectionDate);
            bill.Cancel(userId);
            supplier.UpdateBalance(-bill.TotalAmount);
            await RemoveCancelledBillStockAsync(bill, request.CorrectionDate, request.Reason);
            await _posting.ReversePostedJournalWithinCurrentTransactionAsync(
                journal, request.CorrectionDate, request.Reason, userId);
            var auditUser = await _unitOfWork.Users.GetByIdAsync(userId);
            await _unitOfWork.AuditLogs.AddAsync(AuditLog.Create(
                "PurchaseBill.Cancelled", nameof(PurchaseBill), bill.Id, bill.CompanyId,
                userId, userName: auditUser?.UserName ?? userId.ToString(), newValues: request.Reason.Trim()));
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return MapBillToDto(bill);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<OperationalNoteDto> IssueDebitNoteAsync(
        Guid billId, CorrectPostedDocumentRequest request, Guid userId)
    {
        ValidateCorrection(request);
        var bill = await _unitOfWork.PurchaseBills.GetByIdWithDetailsAsync(billId)
            ?? throw new InvalidOperationException("Purchase bill not found.");
        if (request.CorrectionDate.Date < bill.BillDate.Date)
            throw new InvalidOperationException("Debit-note date cannot be before the bill date.");
        if (!bill.PostedJournalEntryId.HasValue)
            throw new InvalidOperationException("The purchase bill has not been posted and cannot receive a debit note.");
        if (request.ReturnPercent <= 0 || request.ReturnPercent > 100)
            throw new InvalidOperationException("Return percent must be greater than zero and no more than 100.");
        var supplier = await GetSupplierForCompanyAsync(bill.CompanyId, bill.SupplierId);
        var returnFactor = request.ReturnPercent / 100m;
        var taxableAmount = Money.Round(bill.TaxableAmount * returnFactor);
        var vatAmount = Money.Round(bill.VatAmount * returnFactor);
        var returnAmount = Money.Round(taxableAmount + vatAmount);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _posting.EnsurePeriodOpenAsync(bill.CompanyId, request.CorrectionDate);
            bill.IssueDebitNote(returnAmount, userId);
            supplier.UpdateBalance(-returnAmount);
            var inventoryCostReturned = await RemoveCancelledBillStockAsync(
                bill, request.CorrectionDate, request.Reason, "DebitNote", returnFactor);
            var lines = new List<PostingLine>();
            if (inventoryCostReturned > 0)
            {
                var inventoryLedger = await _posting.GetOrCreateInventoryLedgerAsync(bill.CompanyId);
                lines.Add(new PostingLine(inventoryLedger.Id, 0, inventoryCostReturned,
                    $"Inventory returned on debit note for {bill.BillNumber}"));
            }
            var nonInventoryCost = Money.Round(taxableAmount - inventoryCostReturned);
            if (nonInventoryCost < 0)
                throw new InvalidOperationException("Inventory return value exceeds the purchase bill taxable amount.");
            if (nonInventoryCost > 0)
            {
                var purchaseLedger = await _posting.GetOrCreatePurchaseLedgerAsync(bill.CompanyId);
                lines.Add(new PostingLine(purchaseLedger.Id, 0, nonInventoryCost,
                    $"Purchase reversal for debit note {bill.BillNumber}"));
            }
            if (vatAmount > 0)
            {
                var vatLedger = await _posting.GetOrCreatePurchaseVatLedgerAsync(bill.CompanyId);
                lines.Add(new PostingLine(vatLedger.Id, 0, vatAmount,
                    $"VAT reversal for debit note {bill.BillNumber}", bill.VatRate, "INPUT-VAT"));
            }
            lines.Add(new PostingLine(supplier.LedgerId, returnAmount, 0,
                $"Debit note for {bill.BillNumber}"));

            var noteNumber = await _unitOfWork.PurchaseBills.GetNextDebitNoteNumberAsync(bill.CompanyId);
            var journal = await _posting.CreateAndPostJournalAsync(
                bill.CompanyId, request.CorrectionDate, VoucherType.DebitNote, userId, noteNumber,
                $"Debit note {noteNumber} for {bill.BillNumber}: {request.Reason.Trim()}", bill.BranchId, lines);
            var auditUser = await _unitOfWork.Users.GetByIdAsync(userId);
            await _unitOfWork.AuditLogs.AddAsync(AuditLog.Create(
                "PurchaseBill.DebitNoteIssued", nameof(PurchaseBill), bill.Id, bill.CompanyId,
                userId, userName: auditUser?.UserName ?? userId.ToString(), newValues: noteNumber));
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return new OperationalNoteDto
            {
                JournalEntryId = journal.Id,
                NoteNumber = noteNumber,
                NoteDate = request.CorrectionDate.Date,
                Amount = returnAmount,
                SourceDocumentNumber = bill.BillNumber
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task DeleteDraftBillAsync(Guid billId, Guid userId)
    {
        var bill = await _unitOfWork.PurchaseBills.GetByIdWithDetailsAsync(billId)
            ?? throw new InvalidOperationException("Purchase bill not found.");
        if (bill.Status != BillStatus.Draft || bill.PostedJournalEntryId.HasValue)
            throw new InvalidOperationException("Only an unposted draft bill can be discarded.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var auditUser = await _unitOfWork.Users.GetByIdAsync(userId);
            await _unitOfWork.PurchaseBills.DeleteAsync(bill);
            await _unitOfWork.AuditLogs.AddAsync(AuditLog.Create(
                "PurchaseBill.DraftDiscarded", nameof(PurchaseBill), bill.Id, bill.CompanyId,
                userId, userName: auditUser?.UserName ?? userId.ToString(), newValues: bill.BillNumber));
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<PaymentDto> ReversePaymentAsync(
        Guid paymentId, CorrectPostedDocumentRequest request, Guid userId)
    {
        ValidateCorrection(request);
        var payment = await _unitOfWork.Payments.GetByIdWithDetailsAsync(paymentId)
            ?? throw new InvalidOperationException("Payment not found.");
        if (request.CorrectionDate.Date < payment.PaymentDate.Date)
            throw new InvalidOperationException("Correction date cannot be before the payment date.");
        var journal = payment.PostedJournalEntry
            ?? throw new InvalidOperationException("The payment is not linked to its posted journal and cannot be reversed safely.");
        var supplier = await GetSupplierForCompanyAsync(payment.CompanyId, payment.SupplierId);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _posting.EnsurePeriodOpenAsync(payment.CompanyId, request.CorrectionDate);
            foreach (var allocation in payment.Allocations)
            {
                var bill = await _unitOfWork.PurchaseBills.GetByIdWithDetailsAsync(allocation.PurchaseBillId)
                    ?? throw new InvalidOperationException("A payment allocation bill was not found.");
                if (bill.CompanyId != payment.CompanyId || bill.SupplierId != payment.SupplierId)
                    throw new InvalidOperationException("A payment allocation does not belong to the payment company and supplier.");
                bill.UnapplyPayment(allocation.AmountAllocated, request.CorrectionDate);
            }

            supplier.UpdateBalance(Money.Round(payment.Amount + payment.TdsAmount));
            await _posting.ReversePostedJournalWithinCurrentTransactionAsync(
                journal, request.CorrectionDate, request.Reason, userId);
            var auditUser = await _unitOfWork.Users.GetByIdAsync(userId);
            await _unitOfWork.AuditLogs.AddAsync(AuditLog.Create(
                "Payment.Reversed", nameof(Payment), payment.Id, payment.CompanyId,
                userId, userName: auditUser?.UserName ?? userId.ToString(), newValues: request.Reason.Trim()));
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return MapPaymentToDto(payment, supplier.Name);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<IReadOnlyList<PurchaseBillDto>> GetBillsAsync(Guid companyId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var bills = await _unitOfWork.PurchaseBills.GetByCompanyAsync(companyId, fromDate, toDate);
        return bills.Select(MapBillToDto).ToList();
    }

    public async Task<IReadOnlyList<PaymentDto>> GetPaymentsAsync(
        Guid companyId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var payments = await _unitOfWork.Payments.GetByCompanyAsync(companyId, fromDate, toDate);
        return payments.Select(payment => MapPaymentToDto(payment, payment.Supplier.Name)).ToList();
    }

    public async Task<IReadOnlyList<AgeingItemDto>> GetPayablesAgeingAsync(Guid companyId, DateTime asOfDate)
    {
        if (asOfDate == default)
            throw new InvalidOperationException("As-of date is required.");

        var suppliers = await _unitOfWork.Suppliers.GetByCompanyAsync(companyId);
        var bills = await _unitOfWork.PurchaseBills.GetByCompanyAsync(companyId);

        var result = new List<AgeingItemDto>();

        foreach (var supplier in suppliers)
        {
            var payments = await _unitOfWork.Payments.GetBySupplierAsync(supplier.Id);

            var supplierBills = bills.Where(b => b.SupplierId == supplier.Id
                && b.BillDate.Date <= asOfDate.Date
                && b.Status is BillStatus.Received or BillStatus.PartiallyPaid or BillStatus.Paid or BillStatus.Overdue);
            decimal current = 0, d1to30 = 0, d31to60 = 0, d61to90 = 0, over90 = 0;

            foreach (var bill in supplierBills)
            {
                var daysOverdue = (asOfDate - bill.DueDate).Days;
                var allocatedThroughDate = payments
                    .Where(payment => payment.PaymentDate.Date <= asOfDate.Date)
                    .SelectMany(payment => payment.Allocations)
                    .Where(allocation => allocation.PurchaseBillId == bill.Id)
                    .Sum(allocation => allocation.AmountAllocated);
                var balance = Money.Round(bill.TotalAmount - allocatedThroughDate);
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
                    ContactId = supplier.Id,
                    ContactName = supplier.Name,
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

    private static PurchaseBillDto MapBillToDto(PurchaseBill bill) => new()
    {
        Id = bill.Id,
        BillNumber = bill.BillNumber,
        BillDate = bill.BillDate,
        DueDate = bill.DueDate,
        SupplierId = bill.SupplierId,
        SupplierName = bill.Supplier?.Name ?? string.Empty,
        SupplierInvoiceNumber = bill.SupplierInvoiceNumber,
        Reference = bill.Reference,
        Narration = bill.Narration,
        IsVatApplicable = bill.IsVatApplicable,
        VatRate = bill.VatRate,
        SubTotal = bill.SubTotal,
        DiscountAmount = bill.DiscountAmount,
        VatAmount = bill.VatAmount,
        TotalAmount = bill.TotalAmount,
        AmountPaid = bill.AmountPaid,
        BalanceDue = bill.BalanceDue,
        Status = (BillStatusDto)bill.Status,
        PostedJournalEntryId = bill.PostedJournalEntryId,
        CorrectionDate = bill.PostedJournalEntry?.ReversalJournalEntry?.EntryDate,
        CorrectionReason = bill.PostedJournalEntry?.ReversalReason,
        Lines = bill.Lines.Select(l => new PurchaseBillLineDto
        {
            Id = l.Id,
            ItemId = l.ItemId,
            WarehouseId = l.WarehouseId,
            Description = l.Description,
            Quantity = l.Quantity,
            Rate = l.Rate,
            DiscountPercent = l.DiscountPercent,
            TaxPercent = l.TaxPercent,
            DiscountAmount = l.DiscountAmount,
            TaxAmount = l.TaxAmount,
            LineTotal = l.LineTotal
        }).ToList()
    };

    private static PaymentDto MapPaymentToDto(Payment p, string supplierName) => new()
    {
        Id = p.Id,
        PaymentNumber = p.PaymentNumber,
        PaymentDate = p.PaymentDate,
        SupplierId = p.SupplierId,
        SupplierName = supplierName,
        Amount = p.Amount,
        TdsAmount = p.TdsAmount,
        PaymentMethod = (PaymentMethodDto)p.PaymentMethod,
        PostedJournalEntryId = p.PostedJournalEntryId,
        Status = (TransactionStatusDto)(p.PostedJournalEntry?.Status ?? TransactionStatus.Draft),
        CorrectionDate = p.PostedJournalEntry?.ReversalJournalEntry?.EntryDate,
        CorrectionReason = p.PostedJournalEntry?.ReversalReason
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

    private async Task<Supplier> GetSupplierForCompanyAsync(Guid companyId, Guid supplierId)
    {
        var supplier = await _unitOfWork.Suppliers.GetByIdWithLedgerAsync(supplierId)
            ?? throw new InvalidOperationException("Supplier not found.");
        if (supplier.CompanyId != companyId || supplier.Ledger.CompanyId != companyId)
            throw new InvalidOperationException("Supplier does not belong to the selected company.");
        if (!supplier.IsActive || !supplier.Ledger.IsActive)
            throw new InvalidOperationException("Supplier or supplier ledger is inactive.");
        return supplier;
    }

    private async Task ValidatePurchaseLineReferencesAsync(
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
            if (item.ItemType == ItemType.Goods && !line.WarehouseId.HasValue && warehouses.Count == 0)
                throw new InvalidOperationException("A warehouse is required for stock item posting.");
            if (item.ItemType != ItemType.Goods && line.WarehouseId.HasValue)
                throw new InvalidOperationException("A warehouse cannot be assigned to a non-stock item.");
        }
    }

    private async Task<decimal> RecordStockInAsync(PurchaseBill bill)
    {
        decimal totalInventoryCost = 0;
        foreach (var line in bill.Lines.Where(l => l.ItemId.HasValue && l.Quantity > 0))
        {
            var item = await _unitOfWork.Items.GetByIdWithDetailsAsync(line.ItemId!.Value)
                ?? throw new InvalidOperationException($"Item for purchase line '{line.Description}' was not found.");
            if (item.CompanyId != bill.CompanyId)
                throw new InvalidOperationException("Purchase item does not belong to the bill company.");

            if (item.ItemType != ItemType.Goods)
                continue;

            var warehouseId = line.WarehouseId ?? await GetDefaultWarehouseIdAsync(bill.CompanyId);
            var companyWarehouses = (await _unitOfWork.Warehouses.GetByCompanyAsync(bill.CompanyId)).Where(w => w.IsActive).ToList();
            if (companyWarehouses.All(w => w.Id != warehouseId))
                throw new InvalidOperationException("Purchase warehouse does not belong to the bill company.");

            await _posting.EnsureStockDateIsNotBackdatedAsync(
                item.Id, warehouseId, bill.BillDate, "post this purchase bill");

            var balance = await _unitOfWork.StockBalances.GetByItemWarehouseAsync(item.Id, warehouseId);
            if (balance == null)
            {
                balance = StockBalance.Create(bill.CompanyId, item.Id, warehouseId);
                await _unitOfWork.StockBalances.AddAsync(balance);
            }
            else if (balance.CompanyId != bill.CompanyId)
                throw new InvalidOperationException("Stock balance does not belong to the bill company.");

            var unitCost = line.Quantity == 0 ? line.Rate : line.NetAmount / line.Quantity;
            var valueBeforeAddition = balance.TotalValue;
            balance.AddStock(line.Quantity, unitCost);
            var costAdded = Money.Round(balance.TotalValue - valueBeforeAddition);
            totalInventoryCost = Money.Round(totalInventoryCost + costAdded);

            var movement = StockMovement.Create(
                bill.CompanyId,
                item.Id,
                warehouseId,
                MovementType.PurchaseIn,
                line.Quantity,
                unitCost,
                balance.Quantity,
                balance.TotalValue,
                bill.BillDate,
                bill.Id,
                "PurchaseBill",
                narration: $"Purchase bill {bill.BillNumber}: {line.Description}");

            await _unitOfWork.StockMovements.AddAsync(movement);
        }

        return totalInventoryCost;
    }

    private async Task<decimal> RemoveCancelledBillStockAsync(
        PurchaseBill bill, DateTime correctionDate, string reason, string sourceDocumentType = "PurchaseBillCancellation", decimal returnFactor = 1m)
    {
        decimal totalCostReturned = 0;
        var movements = await _unitOfWork.StockMovements.GetBySourceDocumentAsync(
            bill.CompanyId, bill.Id, "PurchaseBill");
        foreach (var movement in movements.Where(m => m.MovementType == MovementType.PurchaseIn))
        {
            await _posting.EnsureStockDateIsNotBackdatedAsync(
                movement.ItemId, movement.WarehouseId, correctionDate, "cancel this purchase bill");
            var balance = await _unitOfWork.StockBalances.GetByItemWarehouseAsync(
                movement.ItemId, movement.WarehouseId)
                ?? throw new InvalidOperationException("The stock balance needed to cancel this bill was not found.");
            var quantity = Money.Round(movement.Quantity * returnFactor);
            var totalCost = Money.Round(movement.TotalCost * returnFactor);
            if (quantity <= 0)
                continue;
            balance.RemoveStockAtValue(quantity, totalCost);
            var returnMovement = StockMovement.Create(
                bill.CompanyId,
                movement.ItemId,
                movement.WarehouseId,
                MovementType.PurchaseReturn,
                quantity,
                quantity == 0 ? 0 : totalCost / quantity,
                balance.Quantity,
                balance.TotalValue,
                correctionDate,
                bill.Id,
                sourceDocumentType,
                narration: $"{(sourceDocumentType == "DebitNote" ? "Debit note" : "Cancellation")} of {bill.BillNumber}: {reason.Trim()}",
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
