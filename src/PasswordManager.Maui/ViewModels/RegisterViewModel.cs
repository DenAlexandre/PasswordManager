using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

public partial class RegisterViewModel : ObservableObject
{
    private readonly AuthService _auth;

    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private string confirmPassword = string.Empty;
    [ObservableProperty] private bool isPasswordVisible;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isBusy;

    public RegisterViewModel(AuthService auth)
    {
        _auth = auth;
    }

    [RelayCommand]
    private void TogglePasswordVisible() => IsPasswordVisible = !IsPasswordVisible;

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;

        var email = Email.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            ErrorMessage = "Adresse email requise.";
            return;
        }
        if (Password.Length < 8)
        {
            ErrorMessage = "Le mot de passe doit contenir au moins 8 caractères.";
            return;
        }
        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Les mots de passe ne correspondent pas.";
            return;
        }

        IsBusy = true;
        try
        {
            var (success, error) = await _auth.RegisterAsync(email, Password);
            if (!success)
            {
                ErrorMessage = error ?? "Impossible de créer le compte.";
                return;
            }

            await Shell.Current.GoToAsync($"{nameof(Views.VerifyEmailPage)}?email={Uri.EscapeDataString(email)}");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
