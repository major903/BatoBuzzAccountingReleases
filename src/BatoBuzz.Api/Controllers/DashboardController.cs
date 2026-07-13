using BatoBuzz.Api.Security;
using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BatoBuzz.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly ICompanyAccessAuthorizer _companyAccess;

    public DashboardController(
        IDashboardService dashboardService,
        ICompanyAccessAuthorizer companyAccess)
    {
        _dashboardService = dashboardService;
        _companyAccess = companyAccess;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<DashboardDto>>> GetDashboard(
        [FromQuery] Guid companyId,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), companyId, cancellationToken);
        var dashboard = await _dashboardService.GetDashboardAsync(companyId);
        return Ok(ApiResponse<DashboardDto>.Ok(dashboard));
    }
}
