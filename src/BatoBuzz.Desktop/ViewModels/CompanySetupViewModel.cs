using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Desktop.Services;
using BatoBuzz.Domain.Common;

namespace BatoBuzz.Desktop.ViewModels;

public partial class CompanySetupViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly ICompanyService _companyService;
    private readonly DesktopSession _session;
    private Guid? _existingCompanyId;

    [ObservableProperty]
    private string _companyName = "";

    [ObservableProperty]
    private string _tradingName = "";

    [ObservableProperty]
    private string _address = "";

    [ObservableProperty]
    private string _city = "";

    [ObservableProperty]
    private string _province = "";

    [ObservableProperty]
    private string _phone = "";

    [ObservableProperty]
    private string _email = "";

    [ObservableProperty]
    private string _panNumber = "";

    [ObservableProperty]
    private string _vatNumber = "";

    [ObservableProperty]
    private string _companyRegNumber = "";

    [ObservableProperty]
    private string _heading = "Company Setup";

    [ObservableProperty]
    private string _actionText = "Create Company";

    [ObservableProperty]
    private bool _isFinancialYearEditable = true;

    [ObservableProperty]
    private string _baseCurrency = "NPR";

    [ObservableProperty]
    private DateTime _financialYearStart = BikramSambatConverter.GetCurrentNepaliFiscalYearStart(DateTime.Now); // Nepal FY start (Shrawan)

    [ObservableProperty]
    private DateTime _financialYearEnd = BikramSambatConverter.GetCurrentNepaliFiscalYearStart(DateTime.Now).AddYears(1).AddDays(-1); // Nepal FY end (Ashad)

    [ObservableProperty]
    private string _financialYearStartBs = "";

    [ObservableProperty]
    private string _financialYearEndBs = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private int _currentStep = 1;

    public string[] Provinces { get; } =
    {
        "Koshi",
        "Madhesh",
        "Bagmati",
        "Gandaki",
        "Lumbini",
        "Karnali",
        "Sudurpashchim"
    };

    public CompanySetupViewModel(MainViewModel mainViewModel, ICompanyService companyService, DesktopSession session)
    {
        _mainViewModel = mainViewModel;
        _companyService = companyService;
        _session = session;
        UpdateBsDates();
        _ = LoadExistingCompanyAsync();
    }

    private async Task LoadExistingCompanyAsync()
    {
        if (!_session.CompanyId.HasValue)
            return;

        try
        {
            var company = await _companyService.GetCompanyAsync(_session.CompanyId.Value);
            if (company == null)
                return;

            _existingCompanyId = company.Id;
            Heading = "Company Details";
            ActionText = "Save Changes";
            IsFinancialYearEditable = false;
            CompanyName = company.Name;
            TradingName = company.TradingName ?? "";
            Address = company.Address ?? "";
            City = company.City ?? "";
            Province = company.Province ?? "";
            Phone = company.Phone ?? "";
            Email = company.Email ?? "";
            PanNumber = company.PanNumber ?? "";
            VatNumber = company.VatNumber ?? "";
            CompanyRegNumber = company.CompanyRegNumber ?? "";
            BaseCurrency = company.BaseCurrency;
            FinancialYearStart = company.FinancialYearStart;
            FinancialYearEnd = company.FinancialYearEnd;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    partial void OnFinancialYearStartChanged(DateTime value) => UpdateBsDates();

    partial void OnFinancialYearEndChanged(DateTime value) => UpdateBsDates();

    private void UpdateBsDates()
    {
        FinancialYearStartBs = BikramSambatConverter.IsSupported(FinancialYearStart)
            ? BikramSambatConverter.ToBsDisplayString(FinancialYearStart)
            : "";
        FinancialYearEndBs = BikramSambatConverter.IsSupported(FinancialYearEnd)
            ? BikramSambatConverter.ToBsDisplayString(FinancialYearEnd)
            : "";
    }

    [RelayCommand]
    private void NextStep()
    {
        if (CurrentStep < 3) CurrentStep++;
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStep > 1) CurrentStep--;
    }

    [RelayCommand]
    private async Task CreateCompany()
    {
        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            ErrorMessage = "Company name is required.";
            return;
        }

        if (FinancialYearEnd.Date < FinancialYearStart.Date)
        {
            ErrorMessage = "Financial year end date cannot be before the start date.";
            return;
        }
        if (_session.UserId == Guid.Empty)
        {
            ErrorMessage = "Sign in before creating a company.";
            return;
        }


        try
        {
            ErrorMessage = "";
            var userId = _session.UserId;
            var request = new CreateCompanyRequest
            {
                Name = CompanyName.Trim(),
                TradingName = string.IsNullOrWhiteSpace(TradingName) ? null : TradingName.Trim(),
                Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                City = string.IsNullOrWhiteSpace(City) ? null : City.Trim(),
                Province = string.IsNullOrWhiteSpace(Province) ? null : Province.Trim(),
                Phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
                Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim(),
                PanNumber = string.IsNullOrWhiteSpace(PanNumber) ? null : PanNumber.Trim(),
                VatNumber = string.IsNullOrWhiteSpace(VatNumber) ? null : VatNumber.Trim(),
                CompanyRegNumber = string.IsNullOrWhiteSpace(CompanyRegNumber) ? null : CompanyRegNumber.Trim(),
                BaseCurrency = BaseCurrency,
                FinancialYearStart = FinancialYearStart,
                FinancialYearEnd = FinancialYearEnd
            };
            var company = _existingCompanyId.HasValue
                ? await _companyService.UpdateCompanyAsync(_existingCompanyId.Value, request, userId)
                : await _companyService.CreateCompanyAsync(request, userId);

            _mainViewModel.SetSession(userId, _session.UserName, company.Id, company.Name);
            ErrorMessage = _existingCompanyId.HasValue ? "Company details saved." : "";
            _existingCompanyId = company.Id;
            Heading = "Company Details";
            ActionText = "Save Changes";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _mainViewModel.ShowDashboardCommand.Execute(null);
    }
}
