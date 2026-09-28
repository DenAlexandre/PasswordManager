using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Crypto;
using PasswordManager.Maui.Models;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

// Personal-vault counterpart to CredentialEditPage - same fields/flow, but encrypts with the
// user's personal-vault key (VaultSession.PersonalVaultKey) and talks to PersonalVaultController
// instead of a Site/SiteGroup. Kept separate from CredentialEditViewModel rather than generalizing
// it, since entangling the two scoping models would complicate both for no real benefit.
public partial class PersonalPasswordEditViewModel : ObservableObject, IQueryAttributable
{
    private readonly ApiClient _api;
    private readonly VaultSession _session;

    private Guid? _editingId;

    [ObservableProperty] private string title = "Nouveau mot de passe";
    [ObservableProperty] private string label = string.Empty;
    [ObservableProperty] private string username = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private bool isPasswordVisible;
    [ObservableProperty] private string url = string.Empty;
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private bool isBusy;

    public PersonalPasswordEditViewModel(ApiClient api, VaultSession session)
    {
        _api = api;
        _session = session;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("item", out var itemObj) && itemObj is CredentialItem item)
        {
            _editingId = item.Id;
            Title = "Modifier le mot de passe";
            Label = item.Label;
            Username = item.Username;
            Password = item.Password;
            Url = item.Url ?? string.Empty;
            Notes = item.Notes ?? string.Empty;
        }
    }

    [RelayCommand]
    private void TogglePasswordVisible() => IsPasswordVisible = !IsPasswordVisible;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(Label))
        {
            ErrorMessage = "Le libellé est requis.";
            return;
        }
        if (_session.PersonalVaultKey is not { } key)
        {
            ErrorMessage = "Coffre personnel non initialisé.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var request = new UpsertPersonalPasswordRequest(
                AesGcmCipher.Encrypt(key, Label),
                AesGcmCipher.Encrypt(key, Username),
                AesGcmCipher.Encrypt(key, Password),
                string.IsNullOrEmpty(Url) ? null : AesGcmCipher.Encrypt(key, Url),
                string.IsNullOrEmpty(Notes) ? null : AesGcmCipher.Encrypt(key, Notes));

            if (_editingId is null)
            {
                var created = await _api.CreatePersonalPasswordAsync(request);
                if (created is null)
                {
                    ErrorMessage = "Impossible d'enregistrer (êtes-vous en ligne ?).";
                    return;
                }
            }
            else
            {
                var updated = await _api.UpdatePersonalPasswordAsync(_editingId.Value, request);
                if (!updated)
                {
                    ErrorMessage = "Impossible d'enregistrer (êtes-vous en ligne ?).";
                    return;
                }
            }

            await Shell.Current.GoToAsync("..");
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
    private async Task CancelAsync() => await Shell.Current.GoToAsync("..");
}
