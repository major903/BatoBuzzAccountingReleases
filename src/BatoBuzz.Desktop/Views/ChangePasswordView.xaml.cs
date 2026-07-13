using BatoBuzz.Desktop.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace BatoBuzz.Desktop.Views;

public partial class ChangePasswordView : UserControl
{
    private ChangePasswordViewModel? _viewModel;
    private bool _isSynchronizing;

    public ChangePasswordView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        => AttachViewModel(e.NewValue as ChangePasswordViewModel);

    private void OnLoaded(object sender, RoutedEventArgs e)
        => AttachViewModel(DataContext as ChangePasswordViewModel);

    private void OnUnloaded(object sender, RoutedEventArgs e) => DetachViewModel();

    private void AttachViewModel(ChangePasswordViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
            return;

        DetachViewModel();

        _viewModel = viewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            SynchronizePasswordBoxes();
        }
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSynchronizing || _viewModel == null || sender is not PasswordBox passwordBox)
            return;

        if (ReferenceEquals(passwordBox, CurrentPasswordBox))
            _viewModel.CurrentPassword = passwordBox.Password;
        else if (ReferenceEquals(passwordBox, NewPasswordBox))
            _viewModel.NewPassword = passwordBox.Password;
        else if (ReferenceEquals(passwordBox, ConfirmPasswordBox))
            _viewModel.ConfirmPassword = passwordBox.Password;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChangePasswordViewModel.CurrentPassword)
            or nameof(ChangePasswordViewModel.NewPassword)
            or nameof(ChangePasswordViewModel.ConfirmPassword))
        {
            SynchronizePasswordBoxes();
        }
    }

    private void SynchronizePasswordBoxes()
    {
        if (_viewModel == null)
            return;

        _isSynchronizing = true;
        try
        {
            SetPassword(CurrentPasswordBox, _viewModel.CurrentPassword);
            SetPassword(NewPasswordBox, _viewModel.NewPassword);
            SetPassword(ConfirmPasswordBox, _viewModel.ConfirmPassword);
        }
        finally
        {
            _isSynchronizing = false;
        }
    }

    private static void SetPassword(PasswordBox passwordBox, string password)
    {
        if (!string.Equals(passwordBox.Password, password, StringComparison.Ordinal))
            passwordBox.Password = password;
    }

    private void DetachViewModel()
    {
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = null;
    }
}
