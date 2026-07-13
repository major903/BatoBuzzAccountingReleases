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
public class PurchasesController : ControllerBase
{
    private readonly IPurchaseService _purchaseService;
    private readonly ICompanyAccessAuthorizer _companyAccess;

    public PurchasesController(
        IPurchaseService purchaseService,
        ICompanyAccessAuthorizer companyAccess)
    {
        _purchaseService = purchaseService;
        _companyAccess = companyAccess;
    }

    [HttpGet("bills")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PurchaseBillDto>>>> GetBills(
        [FromQuery] Guid companyId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), companyId, cancellationToken);
        var bills = await _purchaseService.GetBillsAsync(companyId, fromDate, toDate);
        return Ok(ApiResponse<IReadOnlyList<PurchaseBillDto>>.Ok(bills));
    }

    [HttpPost("bills")]
    public async Task<ActionResult<ApiResponse<PurchaseBillDto>>> CreateBill(
        [FromBody] CreatePurchaseBillRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsureCompanyAccessAsync(userId, request.CompanyId, cancellationToken);
        var bill = await _purchaseService.CreateBillAsync(request, userId);
        return Ok(ApiResponse<PurchaseBillDto>.Ok(bill));
    }

    [HttpPost("bills/{id:guid}/post")]
    public async Task<ActionResult<ApiResponse<PurchaseBillDto>>> PostBill(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsurePurchaseBillAccessAsync(userId, id, cancellationToken);
        var bill = await _purchaseService.PostBillAsync(id, userId);
        return Ok(ApiResponse<PurchaseBillDto>.Ok(bill));
    }

    [HttpPost("payments")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> RecordPayment(
        [FromBody] CreatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsureCompanyAccessAsync(userId, request.CompanyId, cancellationToken);
        var payment = await _purchaseService.RecordPaymentAsync(request, userId);
        return Ok(ApiResponse<PaymentDto>.Ok(payment));
    }

    [HttpGet("payments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PaymentDto>>>> GetPayments(
        [FromQuery] Guid companyId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(User.GetRequiredUserId(), companyId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PaymentDto>>.Ok(
            await _purchaseService.GetPaymentsAsync(companyId, fromDate, toDate)));
    }

    [HttpPost("bills/{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<PurchaseBillDto>>> CancelBill(
        Guid id, [FromBody] CorrectPostedDocumentRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsurePurchaseBillAccessAsync(userId, id, cancellationToken);
        return Ok(ApiResponse<PurchaseBillDto>.Ok(await _purchaseService.CancelBillAsync(id, request, userId)));
    }

    [HttpPost("payments/{id:guid}/reverse")]
    public async Task<ActionResult<ApiResponse<PaymentDto>>> ReversePayment(
        Guid id, [FromBody] CorrectPostedDocumentRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsurePaymentAccessAsync(userId, id, cancellationToken);
        return Ok(ApiResponse<PaymentDto>.Ok(await _purchaseService.ReversePaymentAsync(id, request, userId)));
    }

    [HttpGet("ageing")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AgeingItemDto>>>> GetPayablesAgeing(
        [FromQuery] Guid companyId,
        [FromQuery] DateTime asOfDate,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), companyId, cancellationToken);
        var ageing = await _purchaseService.GetPayablesAgeingAsync(companyId, asOfDate);
        return Ok(ApiResponse<IReadOnlyList<AgeingItemDto>>.Ok(ageing));
    }
}
