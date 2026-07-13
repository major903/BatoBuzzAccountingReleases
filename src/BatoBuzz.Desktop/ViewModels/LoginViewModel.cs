using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Desktop.Services;

namespace BatoBuzz.Desktop.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private readonly DesktopDataService _dataService;
    private readonly IAuthService _authService;
    private readonly RememberedLoginService _rememberedLogin;

    [ObservableProperty]
    private string _userName = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _isOfflineMode;

    [ObservableProperty]
    private bool _rememberMe = true;

    [ObservableProperty]
    private bool _isFirstRun;

    [ObservableProperty]
    private string _loginButtonText = "Login";

    public LoginViewModel(MainViewModel mainViewModel, DesktopDataService dataService, IAuthService authService, RememberedLoginService rememberedLogin)
    {
        _mainViewModel = mainViewModel;
        _dataService = dataService;
        _authService = authService;
        _rememberedLogin = rememberedLogin;
    }

    [RelayCommand]
    private async Task Login()
    {
        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter username and password.";
            return;
        }

        try
        {
            ErrorMessage = "";
            var authResult = IsFirstRun
                ? await _authService.RegisterAsync(new RegisterRequest
                {
                    UserName = UserName.Trim(),
                    Email = $"{UserName.Trim()}@local.batobuzz",
                    Password = Password,
                    FullName = UserName.Trim()
                })
                : await _authService.LoginAsync(new LoginRequest
                {
                    UserName = UserName.Trim(),
                    Password = Password,
                    RememberMe = IsOfflineMode
                });

            if (!authResult.Success || authResult.User == null)
            {
                ErrorMessage = string.Join(Environment.NewLine, authResult.Errors);
                return;
            }

            var company = await _dataService.GetOwnedCompanyAsync(authResult.User.Id);
            if (RememberMe)
                _rememberedLogin.Save(authResult.User.Id, authResult.User.UserName, TimeSpan.FromDays(14), company?.Id);
            else
                _rememberedLogin.Clear();

            _mainViewModel.SetSession(authResult.User.Id, authResult.User.UserName, company?.Id, company?.Name);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task LoginOffline()
    {
        IsOfflineMode = true;
        await Login();
    }

    public async Task InitializeAsync()
    {
        try
        {
            IsFirstRun = !await _dataService.HasUsersAsync();
            LoginButtonText = IsFirstRun ? "Create Owner Account" : "Login";
            if (IsFirstRun)
            {
                ErrorMessage = "First run: create the owner account. Use a password with at least 8 characters.";
                return;
            }

            var remembered = _rememberedLogin.TryLoad();
            if (remembered == null)
                return;

            var user = await _dataService.GetUserByIdAsync(remembered.UserId);
            if (user == null || !user.IsActive || user.IsLockedOut)
            {
                _rememberedLogin.Clear();
                return;
            }

            var company = await _dataService.GetOwnedCompanyAsync(user.Id, remembered.LastCompanyId);
            _mainViewModel.SetSession(user.Id, user.UserName, company?.Id, company?.Name);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
