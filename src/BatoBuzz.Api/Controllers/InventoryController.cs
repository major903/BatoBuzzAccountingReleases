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
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ICompanyAccessAuthorizer _companyAccess;

    public InventoryController(
        IInventoryService inventoryService,
        ICompanyAccessAuthorizer companyAccess)
    {
        _inventoryService = inventoryService;
        _companyAccess = companyAccess;
    }

    [HttpPost("items")]
    public async Task<ActionResult<ApiResponse<ItemDto>>> CreateItem(
        [FromBody] CreateItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsureCompanyAccessAsync(userId, request.CompanyId, cancellationToken);
        var item = await _inventoryService.CreateItemAsync(request, userId);
        return Ok(ApiResponse<ItemDto>.Ok(item));
    }

    [HttpPost("movements")]
    public async Task<ActionResult<ApiResponse>> RecordMovement(
        [FromBody] CreateStockMovementRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsureCompanyAccessAsync(userId, request.CompanyId, cancellationToken);
        await _inventoryService.RecordStockMovementAsync(request, userId);
        return Ok(ApiResponse.Ok("Stock movement recorded."));
    }

    [HttpPost("adjustments")]
    public async Task<ActionResult<ApiResponse>> AdjustStock(
        [FromBody] CreateStockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsureCompanyAccessAsync(userId, request.CompanyId, cancellationToken);
        await _inventoryService.AdjustStockAsync(request, userId);
        return Ok(ApiResponse.Ok("Stock adjusted."));
    }

    [HttpGet("balances")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<StockBalanceDto>>>> GetBalances(
        [FromQuery] Guid companyId,
        [FromQuery] Guid? warehouseId,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), companyId, cancellationToken);
        var balances = await _inventoryService.GetStockBalancesAsync(companyId, warehouseId);
        return Ok(ApiResponse<IReadOnlyList<StockBalanceDto>>.Ok(balances));
    }

    [HttpGet("report")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InventoryReportDto>>>> GetReport(
        [FromQuery] Guid companyId,
        [FromQuery] Guid? warehouseId,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), companyId, cancellationToken);
        var report = await _inventoryService.GetInventoryReportAsync(companyId, warehouseId);
        return Ok(ApiResponse<IReadOnlyList<InventoryReportDto>>.Ok(report));
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ItemDto>>>> GetLowStock(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), companyId, cancellationToken);
        var items = await _inventoryService.GetLowStockItemsAsync(companyId);
        return Ok(ApiResponse<IReadOnlyList<ItemDto>>.Ok(items));
    }
}
