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
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly ICompanyAccessAuthorizer _companyAccess;

    public CompaniesController(
        ICompanyService companyService,
        ICompanyAccessAuthorizer companyAccess)
    {
        _companyService = companyService;
        _companyAccess = companyAccess;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CompanyDto>>>> GetUserCompanies()
    {
        var companies = await _companyService.GetUserCompaniesAsync(User.GetRequiredUserId());
        return Ok(ApiResponse<IReadOnlyList<CompanyDto>>.Ok(companies));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CompanyDto>>> GetCompany(
        Guid id,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), id, cancellationToken);
        var company = await _companyService.GetCompanyAsync(id);
        return company == null
            ? NotFound(ApiResponse<CompanyDto>.Fail("Company not found."))
            : Ok(ApiResponse<CompanyDto>.Ok(company));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CompanyDto>>> CreateCompany(
        [FromBody] CreateCompanyRequest request)
    {
        var company = await _companyService.CreateCompanyAsync(
            request, User.GetRequiredUserId());
        return CreatedAtAction(
            nameof(GetCompany),
            new { id = company.Id },
            ApiResponse<CompanyDto>.Ok(company, "Company created successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<CompanyDto>>> UpdateCompany(
        Guid id,
        [FromBody] CreateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), id, cancellationToken);
        var company = await _companyService.UpdateCompanyAsync(
            id, request, User.GetRequiredUserId());
        return Ok(ApiResponse<CompanyDto>.Ok(company, "Company updated successfully."));
    }


    [HttpGet("{companyId:guid}/financial-year/current")]
    public async Task<ActionResult<ApiResponse<FinancialYearDto>>> GetCurrentFinancialYear(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        await _companyAccess.EnsureCompanyAccessAsync(
            User.GetRequiredUserId(), companyId, cancellationToken);
        var financialYear = await _companyService.GetCurrentFinancialYearAsync(companyId);
        return financialYear == null
            ? NotFound(ApiResponse<FinancialYearDto>.Fail("No current financial year found."))
            : Ok(ApiResponse<FinancialYearDto>.Ok(financialYear));
    }
}
