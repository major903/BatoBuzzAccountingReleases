using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Contracts.Responses;
using BatoBuzz.Domain.Entities;

namespace BatoBuzz.Application.Services;

public class TdsService : ITdsService
{
    private readonly IUnitOfWork _unitOfWork;

    public TdsService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<TdsRateDto> CreateRateAsync(CreateTdsRateRequest request, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Rate name is required.");
        if (request.RatePercent < 0 || request.RatePercent >= 100)
            throw new InvalidOperationException("Rate percent must be between 0 and less than 100.");

        var rate = TdsRate.Create(request.CompanyId, request.Name.Trim(), request.RatePercent, request.Description?.Trim());
        rate.SetCreatedBy(userId);

        await _unitOfWork.TdsRates.AddAsync(rate);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(rate);
    }

    public async Task<TdsRateDto> UpdateRateAsync(Guid rateId, string name, decimal ratePercent, string? description, bool isActive)
    {
        var rate = await _unitOfWork.TdsRates.GetByIdAsync(rateId)
            ?? throw new InvalidOperationException("TDS rate not found.");

        if (ratePercent < 0 || ratePercent >= 100)
            throw new InvalidOperationException("Rate percent must be between 0 and less than 100.");

        rate.Update(name.Trim(), ratePercent, description?.Trim(), isActive);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(rate);
    }

    public async Task<IReadOnlyList<TdsRateDto>> GetRatesAsync(Guid companyId, bool activeOnly = true)
    {
        var rates = await _unitOfWork.TdsRates.GetByCompanyAsync(companyId, activeOnly);
        return rates.Select(MapToDto).ToList();
    }

    private static TdsRateDto MapToDto(TdsRate rate) => new()
    {
        Id = rate.Id,
        Name = rate.Name,
        RatePercent = rate.RatePercent,
        Description = rate.Description,
        IsActive = rate.IsActive
    };
}
