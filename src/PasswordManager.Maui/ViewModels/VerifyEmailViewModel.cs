using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

[QueryProperty(nameof(Email), "email")]
public partial class VerifyEmailViewModel : ObservableObject
{
    private readonly AuthService _auth;

    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string code = string.Empty;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool isBusy;

    public VerifyEmailViewModel(AuthService auth)
    {
        _auth = auth;
    }

    [RelayCommand]
    private async Task VerifyAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(Code))
        {
            ErrorMessage = "Entrez le code reçu par email.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            var (success, error) = await _auth.VerifyEmailAsync(Email, Code.Trim());
            if (!success)
            {
                ErrorMessage = error ?? "Code invalide.";
                return;
            }

            await Shell.Current.GoToAsync($"{nameof(Views.VaultUnlockPage)}?setup=true");
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

    [RelayCommand]
    private async Task ResendAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            var sent = await _auth.ResendVerificationAsync(Email);
            StatusMessage = sent ? "Un nouveau code a été envoyé." : "Impossible d'envoyer le code.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
