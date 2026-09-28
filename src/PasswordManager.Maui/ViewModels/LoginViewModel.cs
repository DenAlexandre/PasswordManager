using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;

    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private bool isPasswordVisible;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isBusy;

    public LoginViewModel(AuthService auth)
    {
        _auth = auth;
    }

    [RelayCommand]
    private void TogglePasswordVisible() => IsPasswordVisible = !IsPasswordVisible;

    [RelayCommand]
    private async Task GoToRegisterAsync() => await Shell.Current.GoToAsync(nameof(Views.RegisterPage));

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var outcome = await _auth.LoginAsync(Email.Trim(), Password);
            switch (outcome)
            {
                case LoginOutcome.InvalidCredentials:
                    ErrorMessage = "Email ou mot de passe incorrect.";
                    break;
                case LoginOutcome.VaultSetupRequired:
                    await Shell.Current.GoToAsync($"{nameof(Views.VaultUnlockPage)}?setup=true");
                    break;
                case LoginOutcome.NeedsUnlock:
                    await Shell.Current.GoToAsync($"{nameof(Views.VaultUnlockPage)}?setup=false");
                    break;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Connexion impossible : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
