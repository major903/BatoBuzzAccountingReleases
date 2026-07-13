using BatoBuzz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BatoBuzz.Api.Security;

public interface ICompanyAccessAuthorizer
{
    Task EnsureCompanyAccessAsync(Guid userId, Guid companyId, CancellationToken cancellationToken = default);
    Task EnsureJournalAccessAsync(Guid userId, Guid journalId, CancellationToken cancellationToken = default);
    Task EnsureInvoiceAccessAsync(Guid userId, Guid invoiceId, CancellationToken cancellationToken = default);
    Task EnsureReceiptAccessAsync(Guid userId, Guid receiptId, CancellationToken cancellationToken = default);
    Task EnsurePurchaseBillAccessAsync(Guid userId, Guid billId, CancellationToken cancellationToken = default);
    Task EnsurePaymentAccessAsync(Guid userId, Guid paymentId, CancellationToken cancellationToken = default);
    Task EnsureTdsRateAccessAsync(Guid userId, Guid rateId, CancellationToken cancellationToken = default);
}

public sealed class CompanyAccessAuthorizer : ICompanyAccessAuthorizer
{
    private readonly BatoBuzzDbContext _dbContext;

    public CompanyAccessAuthorizer(BatoBuzzDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureCompanyAccessAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("Company ID is required.", nameof(companyId));

        var company = await _dbContext.Companies
            .AsNoTracking()
            .Where(candidate => candidate.Id == companyId)
            .Select(candidate => new { candidate.CreatedByUserId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Company not found.");

        if (company.CreatedByUserId != userId)
            throw new UnauthorizedAccessException("You do not have access to this company.");
    }

    public async Task EnsureJournalAccessAsync(
        Guid userId,
        Guid journalId,
        CancellationToken cancellationToken = default)
    {
        var companyId = await _dbContext.JournalEntries
            .AsNoTracking()
            .Where(entry => entry.Id == journalId)
            .Select(entry => (Guid?)entry.CompanyId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Journal entry not found.");

        await EnsureCompanyAccessAsync(userId, companyId, cancellationToken);
    }

    public async Task EnsureInvoiceAccessAsync(
        Guid userId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        var companyId = await _dbContext.SalesInvoices
            .AsNoTracking()
            .Where(invoice => invoice.Id == invoiceId)
            .Select(invoice => (Guid?)invoice.CompanyId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Invoice not found.");

        await EnsureCompanyAccessAsync(userId, companyId, cancellationToken);
    }

    public async Task EnsurePurchaseBillAccessAsync(
        Guid userId,
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        var companyId = await _dbContext.PurchaseBills
            .AsNoTracking()
            .Where(bill => bill.Id == billId)
            .Select(bill => (Guid?)bill.CompanyId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Purchase bill not found.");

        await EnsureCompanyAccessAsync(userId, companyId, cancellationToken);
    }

    public async Task EnsureReceiptAccessAsync(
        Guid userId,
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        var companyId = await _dbContext.Receipts.AsNoTracking()
            .Where(receipt => receipt.Id == receiptId)
            .Select(receipt => (Guid?)receipt.CompanyId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Receipt not found.");
        await EnsureCompanyAccessAsync(userId, companyId, cancellationToken);
    }

    public async Task EnsurePaymentAccessAsync(
        Guid userId,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var companyId = await _dbContext.Payments.AsNoTracking()
            .Where(payment => payment.Id == paymentId)
            .Select(payment => (Guid?)payment.CompanyId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Payment not found.");
        await EnsureCompanyAccessAsync(userId, companyId, cancellationToken);
    }

    public async Task EnsureTdsRateAccessAsync(
        Guid userId,
        Guid rateId,
        CancellationToken cancellationToken = default)
    {
        var companyId = await _dbContext.TdsRates
            .AsNoTracking()
            .Where(rate => rate.Id == rateId)
            .Select(rate => (Guid?)rate.CompanyId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("TDS rate not found.");

        await EnsureCompanyAccessAsync(userId, companyId, cancellationToken);
    }
}
