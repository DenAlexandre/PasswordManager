using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Crypto;
using PasswordManager.Maui.Data;
using PasswordManager.Maui.Models;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

public partial class CredentialItem : ObservableObject
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Notes { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaskedPassword))]
    private bool isRevealed;

    public string MaskedPassword => IsRevealed ? Password : new string('•', Math.Max(Password.Length, 8));
}

[QueryProperty(nameof(SiteId), "siteId")]
[QueryProperty(nameof(SiteName), "siteName")]
[QueryProperty(nameof(SiteGroupId), "siteGroupId")]
public partial class CredentialsViewModel : ObservableObject
{
    private readonly LocalCacheDb _cache;
    private readonly ApiClient _api;
    private readonly VaultSession _session;

    [ObservableProperty] private string siteId = string.Empty;
    [ObservableProperty] private string siteName = string.Empty;
    [ObservableProperty] private string siteGroupId = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool canWrite;
    [ObservableProperty] private string? errorMessage;

    public ObservableCollection<CredentialItem> Credentials { get; } = new();

    public CredentialsViewModel(LocalCacheDb cache, ApiClient api, VaultSession session)
    {
        _cache = cache;
        _api = api;
        _session = session;
    }

    private Guid SiteGuid => Guid.Parse(SiteId);
    private Guid GroupGuid => Guid.Parse(SiteGroupId);

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(SiteId) || string.IsNullOrEmpty(SiteGroupId) || IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var groups = await _cache.GetSiteGroupsAsync();
            var group = groups.FirstOrDefault(g => g.Id == GroupGuid);
            if (group is null)
            {
                ErrorMessage = "Groupe de sites introuvable dans le cache local.";
                return;
            }
            CanWrite = (AccessRole)group.Role == AccessRole.Write;

            var groupKey = _session.GetOrUnwrapGroupKey(group.Id, group.EncryptedGroupKey);
            var credentials = await _cache.GetCredentialsAsync(SiteGuid);

            Credentials.Clear();
            foreach (var c in credentials.OrderBy(c => c.Id))
            {
                try
                {
                    Credentials.Add(new CredentialItem
                    {
                        Id = c.Id,
                        Label = AesGcmCipher.Decrypt(groupKey, c.EncryptedLabel),
                        Username = AesGcmCipher.Decrypt(groupKey, c.EncryptedUsername),
                        Password = AesGcmCipher.Decrypt(groupKey, c.EncryptedPassword),
                        Notes = c.EncryptedNotes is null ? null : AesGcmCipher.Decrypt(groupKey, c.EncryptedNotes)
                    });
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    // Corrupt/foreign entry - skip rather than crash the whole list.
                }
            }
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = "Le coffre est verrouillé.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur de chargement : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleReveal(CredentialItem item)
    {
        item.IsRevealed = !item.IsRevealed;
    }

    [RelayCommand]
    private async Task CopyPasswordAsync(CredentialItem item)
    {
        await Clipboard.SetTextAsync(item.Password);
    }

    [RelayCommand]
    private async Task AddCredentialAsync()
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null) return;

        var label = await page.DisplayPromptAsync("Nouveau mot de passe", "Libellé (ex: Accès FTP)");
        if (string.IsNullOrWhiteSpace(label)) return;
        var username = await page.DisplayPromptAsync("Nouveau mot de passe", "Identifiant") ?? "";
        var password = await page.DisplayPromptAsync("Nouveau mot de passe", "Mot de passe");
        if (password is null) return;

        var groups = await _cache.GetSiteGroupsAsync();
        var group = groups.First(g => g.Id == GroupGuid);
        var groupKey = _session.GetOrUnwrapGroupKey(group.Id, group.EncryptedGroupKey);

        var request = new UpsertCredentialRequest(
            AesGcmCipher.Encrypt(groupKey, label),
            AesGcmCipher.Encrypt(groupKey, username),
            AesGcmCipher.Encrypt(groupKey, password),
            null);

        var created = await _api.CreateCredentialAsync(SiteGuid, request);
        if (created is null)
        {
            await page.DisplayAlert("Erreur", "Impossible d'enregistrer (êtes-vous en ligne ?).", "OK");
            return;
        }

        await _cache.UpsertCredentialsAsync(new[]
        {
            new CachedCredential
            {
                Id = created.Id, SiteId = SiteGuid, EncryptedLabel = created.EncryptedLabel,
                EncryptedUsername = created.EncryptedUsername, EncryptedPassword = created.EncryptedPassword,
                EncryptedNotes = created.EncryptedNotes, UpdatedAt = created.UpdatedAt
            }
        });
        await LoadAsync();
    }
}
