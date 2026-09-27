using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Crypto;
using PasswordManager.Maui.Data;
using PasswordManager.Maui.Models;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

// Single modal covering everything a KeePass-style entry needs at once (label, username,
// password, URL, notes) instead of a chain of separate prompts - used for both create and edit.
public partial class CredentialEditViewModel : ObservableObject, IQueryAttributable
{
    private readonly ApiClient _api;
    private readonly LocalCacheDb _cache;
    private readonly VaultSession _session;

    private Guid _siteId;
    private Guid _siteGroupId;
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

    public CredentialEditViewModel(ApiClient api, LocalCacheDb cache, VaultSession session)
    {
        _api = api;
        _cache = cache;
        _session = session;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _siteId = (Guid)query["siteId"];
        _siteGroupId = (Guid)query["siteGroupId"];

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

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var groups = await _cache.GetSiteGroupsAsync();
            var group = groups.FirstOrDefault(g => g.Id == _siteGroupId);
            if (group is null)
            {
                ErrorMessage = "Groupe de sites introuvable.";
                return;
            }
            var groupKey = _session.GetOrUnwrapGroupKey(group.Id, group.EncryptedGroupKey);

            var request = new UpsertCredentialRequest(
                AesGcmCipher.Encrypt(groupKey, Label),
                AesGcmCipher.Encrypt(groupKey, Username),
                AesGcmCipher.Encrypt(groupKey, Password),
                string.IsNullOrEmpty(Url) ? null : AesGcmCipher.Encrypt(groupKey, Url),
                string.IsNullOrEmpty(Notes) ? null : AesGcmCipher.Encrypt(groupKey, Notes));

            if (_editingId is null)
            {
                var created = await _api.CreateCredentialAsync(_siteId, request);
                if (created is null)
                {
                    ErrorMessage = "Impossible d'enregistrer (êtes-vous en ligne ?).";
                    return;
                }
                await _cache.UpsertCredentialsAsync(new[] { ToCached(created.Id, request, created.UpdatedAt) });
            }
            else
            {
                var updated = await _api.UpdateCredentialAsync(_siteId, _editingId.Value, request);
                if (!updated)
                {
                    ErrorMessage = "Impossible d'enregistrer (droits insuffisants ou hors-ligne).";
                    return;
                }
                await _cache.UpsertCredentialsAsync(new[] { ToCached(_editingId.Value, request, DateTimeOffset.UtcNow) });
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

    private CachedCredential ToCached(Guid id, UpsertCredentialRequest request, DateTimeOffset updatedAt) => new()
    {
        Id = id,
        SiteId = _siteId,
        EncryptedLabel = request.EncryptedLabel,
        EncryptedUsername = request.EncryptedUsername,
        EncryptedPassword = request.EncryptedPassword,
        EncryptedUrl = request.EncryptedUrl,
        EncryptedNotes = request.EncryptedNotes,
        UpdatedAt = updatedAt
    };
}
