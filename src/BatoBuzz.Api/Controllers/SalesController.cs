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
public class SalesController : ControllerBase
{
    private readonly ISalesService _salesService;
    private readonly ICompanyAccessAuthorizer _companyAccess;

    public SalesController(
        ISalesService salesService,
        ICompanyAccessAuthorizer companyAccess)
    {
        _salesService = salesService;
        _companyAccess = companyAccess;
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SalesInvoiceDto>>>> GetInvoices(
        [FromQuery] Guid companyId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), companyId, cancellationToken);
        var invoices = await _salesService.GetInvoicesAsync(companyId, fromDate, toDate);
        return Ok(ApiResponse<IReadOnlyList<SalesInvoiceDto>>.Ok(invoices));
    }

    [HttpPost("invoices")]
    public async Task<ActionResult<ApiResponse<SalesInvoiceDto>>> CreateInvoice(
        [FromBody] CreateSalesInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsureCompanyAccessAsync(userId, request.CompanyId, cancellationToken);
        var invoice = await _salesService.CreateInvoiceAsync(request, userId);
        return Ok(ApiResponse<SalesInvoiceDto>.Ok(invoice));
    }

    [HttpPost("invoices/{id:guid}/post")]
    public async Task<ActionResult<ApiResponse<SalesInvoiceDto>>> PostInvoice(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsureInvoiceAccessAsync(userId, id, cancellationToken);
        var invoice = await _salesService.PostInvoiceAsync(id, userId);
        return Ok(ApiResponse<SalesInvoiceDto>.Ok(invoice));
    }

    [HttpPost("receipts")]
    public async Task<ActionResult<ApiResponse<ReceiptDto>>> RecordReceipt(
        [FromBody] CreateReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsureCompanyAccessAsync(userId, request.CompanyId, cancellationToken);
        var receipt = await _salesService.RecordReceiptAsync(request, userId);
        return Ok(ApiResponse<ReceiptDto>.Ok(receipt));
    }

    [HttpGet("receipts")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ReceiptDto>>>> GetReceipts(
        [FromQuery] Guid companyId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(User.GetRequiredUserId(), companyId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<ReceiptDto>>.Ok(
            await _salesService.GetReceiptsAsync(companyId, fromDate, toDate)));
    }

    [HttpPost("invoices/{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<SalesInvoiceDto>>> CancelInvoice(
        Guid id, [FromBody] CorrectPostedDocumentRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsureInvoiceAccessAsync(userId, id, cancellationToken);
        return Ok(ApiResponse<SalesInvoiceDto>.Ok(await _salesService.CancelInvoiceAsync(id, request, userId)));
    }

    [HttpPost("receipts/{id:guid}/reverse")]
    public async Task<ActionResult<ApiResponse<ReceiptDto>>> ReverseReceipt(
        Guid id, [FromBody] CorrectPostedDocumentRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsureReceiptAccessAsync(userId, id, cancellationToken);
        return Ok(ApiResponse<ReceiptDto>.Ok(await _salesService.ReverseReceiptAsync(id, request, userId)));
    }

    [HttpGet("ageing")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AgeingItemDto>>>> GetReceivablesAgeing(
        [FromQuery] Guid companyId,
        [FromQuery] DateTime asOfDate,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), companyId, cancellationToken);
        var ageing = await _salesService.GetReceivablesAgeingAsync(companyId, asOfDate);
        return Ok(ApiResponse<IReadOnlyList<AgeingItemDto>>.Ok(ageing));
    }
}
