using BatoBuzz.Api.Security;
using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AccountingController : ControllerBase
{
    private readonly IAccountingService _accountingService;
    private readonly ICompanyAccessAuthorizer _companyAccess;

    public AccountingController(
        IAccountingService accountingService,
        ICompanyAccessAuthorizer companyAccess)
    {
        _accountingService = accountingService;
        _companyAccess = companyAccess;
    }

    [HttpPost("journals")]
    public async Task<ActionResult<ApiResponse<JournalEntryDto>>> CreateJournal(
        [FromBody] CreateJournalRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsureCompanyAccessAsync(userId, request.CompanyId, cancellationToken);
        var entry = await _accountingService.CreateJournalAsync(request, userId);
        return Ok(ApiResponse<JournalEntryDto>.Ok(entry));
    }

    [HttpPost("journals/{id:guid}/post")]
    public async Task<ActionResult<ApiResponse<JournalEntryDto>>> PostJournal(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsureJournalAccessAsync(userId, id, cancellationToken);
        var entry = await _accountingService.PostJournalAsync(id, userId);
        return Ok(ApiResponse<JournalEntryDto>.Ok(entry));
    }

    [HttpGet("journals")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<JournalEntryDto>>>> GetJournals(
        [FromQuery] Guid companyId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(User.GetRequiredUserId(), companyId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<JournalEntryDto>>.Ok(
            await _accountingService.GetJournalsAsync(companyId, fromDate, toDate)));
    }

    [HttpPost("journals/{id:guid}/reverse")]
    public async Task<ActionResult<ApiResponse<JournalEntryDto>>> ReverseJournal(
        Guid id, [FromBody] CorrectPostedDocumentRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsureJournalAccessAsync(userId, id, cancellationToken);
        return Ok(ApiResponse<JournalEntryDto>.Ok(
            await _accountingService.ReverseJournalAsync(id, request, userId)));
    }

    [HttpGet("trial-balance")]
    public async Task<ActionResult<ApiResponse<TrialBalanceReportDto>>> GetTrialBalance(
        [FromQuery] Guid companyId,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), companyId, cancellationToken);
        var report = await _accountingService.GetTrialBalanceAsync(companyId, fromDate, toDate);
        return Ok(ApiResponse<TrialBalanceReportDto>.Ok(report));
    }

    [HttpGet("profit-loss")]
    public async Task<ActionResult<ApiResponse<ProfitAndLossReportDto>>> GetProfitAndLoss(
        [FromQuery] Guid companyId,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), companyId, cancellationToken);
        var report = await _accountingService.GetProfitAndLossAsync(companyId, fromDate, toDate);
        return Ok(ApiResponse<ProfitAndLossReportDto>.Ok(report));
    }

    [HttpGet("balance-sheet")]
    public async Task<ActionResult<ApiResponse<BalanceSheetReportDto>>> GetBalanceSheet(
        [FromQuery] Guid companyId,
        [FromQuery] DateTime asOfDate,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), companyId, cancellationToken);
        var report = await _accountingService.GetBalanceSheetAsync(companyId, asOfDate);
        return Ok(ApiResponse<BalanceSheetReportDto>.Ok(report));
    }

    [HttpGet("general-ledger/{ledgerId:guid}")]
    public async Task<ActionResult<ApiResponse<GeneralLedgerReportDto>>> GetGeneralLedger(
        Guid ledgerId,
        [FromQuery] Guid companyId,
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), companyId, cancellationToken);
        var report = await _accountingService.GetGeneralLedgerAsync(
            companyId, ledgerId, fromDate, toDate);
        return Ok(ApiResponse<GeneralLedgerReportDto>.Ok(report));
    }
}
