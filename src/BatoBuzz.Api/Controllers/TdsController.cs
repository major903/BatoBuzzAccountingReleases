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
public class TdsController : ControllerBase
{
    private readonly ITdsService _tdsService;
    private readonly ICompanyAccessAuthorizer _companyAccess;

    public TdsController(
        ITdsService tdsService,
        ICompanyAccessAuthorizer companyAccess)
    {
        _tdsService = tdsService;
        _companyAccess = companyAccess;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<TdsRateDto>>>> GetRates(
        [FromQuery] Guid companyId,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), companyId, cancellationToken);
        var rates = await _tdsService.GetRatesAsync(companyId, activeOnly);
        return Ok(ApiResponse<IReadOnlyList<TdsRateDto>>.Ok(rates));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<TdsRateDto>>> CreateRate(
        [FromBody] CreateTdsRateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        await _companyAccess.EnsureCompanyAccessAsync(userId, request.CompanyId, cancellationToken);
        var rate = await _tdsService.CreateRateAsync(request, userId);
        return Ok(ApiResponse<TdsRateDto>.Ok(rate));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<TdsRateDto>>> UpdateRate(
        Guid id,
        [FromBody] UpdateTdsRateRequest request,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureTdsRateAccessAsync(
            User.GetRequiredUserId(), id, cancellationToken);
        var rate = await _tdsService.UpdateRateAsync(
            id, request.Name, request.RatePercent, request.Description, request.IsActive);
        return Ok(ApiResponse<TdsRateDto>.Ok(rate));
    }
}
