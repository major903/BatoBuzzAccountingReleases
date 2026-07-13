using BatoBuzz.Application.Interfaces;
using BatoBuzz.Contracts.Requests;
using BatoBuzz.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BatoBuzz.Desktop.ViewModels;

public partial class ChangePasswordViewModel : ObservableObject, IDisposable
{
    private const int MinimumPasswordLength = 8;
    private const int MaximumPasswordLength = 128;

    private readonly IAuthService _authService;
    private readonly DesktopSession _session;

    [ObservableProperty]
    private string _currentPassword = "";

    [ObservableProperty]
    private string _newPassword = "";

    [ObservableProperty]
    private string _confirmPassword = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isStatusSuccess;

    public string SignedInUserName => _session.UserName;

    public ChangePasswordViewModel(IAuthService authService, DesktopSession session)
    {
        _authService = authService;
        _session = session;
    }

    [RelayCommand]
    private async Task ChangePassword()
    {
        ClearStatus();

        if (_session.UserId == Guid.Empty)
        {
            SetError("Your sign-in session is no longer valid. Please sign in again.");
            return;
        }

        if (string.IsNullOrEmpty(CurrentPassword)
            || string.IsNullOrEmpty(NewPassword)
            || string.IsNullOrEmpty(ConfirmPassword))
        {
            SetError("Enter your current password, new password, and confirmation.");
            return;
        }

        if (CurrentPassword.Length > MaximumPasswordLength)
        {
            SetError("The current password is too long.");
            return;
        }

        if (NewPassword.Length is < MinimumPasswordLength or > MaximumPasswordLength)
        {
            SetError($"The new password must be between {MinimumPasswordLength} and {MaximumPasswordLength} characters.");
            return;
        }

        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            SetError("The new password and confirmation do not match.");
            return;
        }

        if (string.Equals(CurrentPassword, NewPassword, StringComparison.Ordinal))
        {
            SetError("Choose a new password that is different from your current password.");
            return;
        }

        try
        {
            var changed = await _authService.ChangePasswordAsync(
                _session.UserId,
                new ChangePasswordRequest
                {
                    CurrentPassword = CurrentPassword,
                    NewPassword = NewPassword
                });

            if (!changed)
            {
                SetError("The password could not be changed. Check your current password and try again.");
                return;
            }

            CurrentPassword = "";
            NewPassword = "";
            ConfirmPassword = "";
            IsStatusSuccess = true;
            StatusMessage = "Password changed successfully.";
        }
        catch (Exception)
        {
            SetError("The password could not be changed because of an unexpected error. Please try again.");
        }
    }

    [RelayCommand]
    private void Clear()
    {
        CurrentPassword = "";
        NewPassword = "";
        ConfirmPassword = "";
        ClearStatus();
    }

    partial void OnCurrentPasswordChanged(string value) => ClearStatus();

    partial void OnNewPasswordChanged(string value) => ClearStatus();

    partial void OnConfirmPasswordChanged(string value) => ClearStatus();

    private void SetError(string message)
    {
        IsStatusSuccess = false;
        StatusMessage = message;
    }

    private void ClearStatus()
    {
        IsStatusSuccess = false;
        StatusMessage = "";
    }

    public void Dispose()
    {
        CurrentPassword = "";
        NewPassword = "";
        ConfirmPassword = "";
        IsStatusSuccess = false;
        StatusMessage = "";
        GC.SuppressFinalize(this);
    }
}
