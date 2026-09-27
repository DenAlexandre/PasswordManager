using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

[QueryProperty(nameof(SetupMode), "setup")]
public partial class VaultUnlockViewModel : ObservableObject
{
    private readonly AuthService _auth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSetup))]
    private string setupMode = "false";
    [ObservableProperty] private string masterPassword = string.Empty;
    [ObservableProperty] private string confirmMasterPassword = string.Empty;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isBusy;

    public bool IsSetup => bool.TryParse(SetupMode, out var v) && v;

    public VaultUnlockViewModel(AuthService auth)
    {
        _auth = auth;
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;

        if (IsSetup)
        {
            if (MasterPassword.Length < 10)
            {
                ErrorMessage = "Le mot de passe maître doit contenir au moins 10 caractères.";
                return;
            }
            if (MasterPassword != ConfirmMasterPassword)
            {
                ErrorMessage = "Les mots de passe ne correspondent pas.";
                return;
            }
        }

        IsBusy = true;
        try
        {
            var ok = IsSetup
                ? await _auth.SetupVaultAsync(MasterPassword)
                : await _auth.UnlockAsync(MasterPassword);

            if (!ok)
            {
                ErrorMessage = IsSetup
                    ? "Impossible de créer le coffre (êtes-vous en ligne ?)."
                    : "Mot de passe maître incorrect.";
                return;
            }

            // VaultTreePage triggers its own sync on appearing - avoid a second concurrent
            // sync here, which previously corrupted the local cache's SQLite transaction state.
            await Shell.Current.GoToAsync(nameof(Views.VaultTreePage));
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
