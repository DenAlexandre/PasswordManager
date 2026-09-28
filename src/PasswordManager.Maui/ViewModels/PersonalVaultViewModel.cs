using System.Collections.ObjectModel;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Crypto;
using PasswordManager.Maui.Models;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

public partial class PersonalPasswordItem : ObservableObject
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Notes { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaskedPassword))]
    private bool isRevealed;

    public string MaskedPassword => IsRevealed ? Password : new string('•', Math.Max(Password.Length, 8));

    public CredentialItem ToCredentialItem() => new()
    {
        Id = Id, Label = Label, Username = Username, Password = Password, Url = Url, Notes = Notes
    };
}

public class PersonalDocumentItem
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string SizeLabel => FileSizeBytes < 1_000_000
        ? $"{FileSizeBytes / 1024.0:0.#} Ko"
        : $"{FileSizeBytes / 1_000_000.0:0.#} Mo";
}

// Single-owner vault, distinct from the shared SiteGroup tree (VaultTreePage) - online-only (no
// LocalCacheDb caching), loaded fresh every time the page appears.
public partial class PersonalVaultViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly VaultSession _session;
    private readonly DocumentService _documents;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public ObservableCollection<PersonalPasswordItem> Passwords { get; } = new();
    public ObservableCollection<PersonalDocumentItem> Documents { get; } = new();

    public PersonalVaultViewModel(ApiClient api, VaultSession session, DocumentService documents)
    {
        _api = api;
        _session = session;
        _documents = documents;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (_session.PersonalVaultKey is null && !await EnsurePersonalVaultKeyAsync())
                return;
            var key = _session.PersonalVaultKey!;

            var passwords = await _api.GetPersonalPasswordsAsync();
            Passwords.Clear();
            foreach (var p in passwords ?? new List<PersonalPasswordDto>())
            {
                try
                {
                    Passwords.Add(new PersonalPasswordItem
                    {
                        Id = p.Id,
                        Label = AesGcmCipher.Decrypt(key, p.EncryptedLabel),
                        Username = AesGcmCipher.Decrypt(key, p.EncryptedUsername),
                        Password = AesGcmCipher.Decrypt(key, p.EncryptedPassword),
                        Url = p.EncryptedUrl is null ? null : AesGcmCipher.Decrypt(key, p.EncryptedUrl),
                        Notes = p.EncryptedNotes is null ? null : AesGcmCipher.Decrypt(key, p.EncryptedNotes)
                    });
                }
                catch (CryptographicException) { /* corrupt entry - skip rather than crash the list */ }
            }

            var documents = await _api.GetPersonalDocumentsAsync();
            Documents.Clear();
            foreach (var d in documents ?? new List<PersonalDocumentDto>())
            {
                try
                {
                    Documents.Add(new PersonalDocumentItem
                    {
                        Id = d.Id,
                        FileName = AesGcmCipher.Decrypt(key, d.EncryptedFileName),
                        ContentType = d.ContentType,
                        FileSizeBytes = d.FileSizeBytes
                    });
                }
                catch (CryptographicException) { }
            }
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

    // Handles both a brand-new signup (key already set during vault-setup) and a pre-existing
    // account opening the Personal Vault for the first time since this feature shipped.
    private async Task<bool> EnsurePersonalVaultKeyAsync()
    {
        var keyMaterial = await _api.GetKeyMaterialAsync();
        if (keyMaterial is null)
        {
            ErrorMessage = "Connexion requise pour accéder au coffre personnel.";
            return false;
        }

        if (!string.IsNullOrEmpty(keyMaterial.EncryptedPersonalVaultKey))
        {
            _session.UnlockPersonalVaultKey(keyMaterial.EncryptedPersonalVaultKey);
            return true;
        }

        var newKey = _session.CreatePersonalVaultKey();
        var wrapped = RsaKeyWrapping.WrapKey(keyMaterial.PublicKey, newKey);
        var set = await _api.SetPersonalVaultKeyAsync(new SetPersonalVaultKeyRequest(wrapped));
        if (!set)
        {
            ErrorMessage = "Impossible d'initialiser le coffre personnel.";
            return false;
        }
        return true;
    }

    [RelayCommand]
    private async Task AddPasswordAsync() => await Shell.Current.GoToAsync(nameof(Views.PersonalPasswordEditPage));

    [RelayCommand]
    private async Task EditPasswordAsync(PersonalPasswordItem item) =>
        await Shell.Current.GoToAsync(nameof(Views.PersonalPasswordEditPage), new Dictionary<string, object>
        {
            ["item"] = item.ToCredentialItem()
        });

    [RelayCommand]
    private void ToggleReveal(PersonalPasswordItem item) => item.IsRevealed = !item.IsRevealed;

    [RelayCommand]
    private async Task CopyPasswordAsync(PersonalPasswordItem item) => await Clipboard.SetTextAsync(item.Password);

    [RelayCommand]
    private async Task DeletePasswordAsync(PersonalPasswordItem item)
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null) return;

        var confirm = await page.DisplayAlert("Confirmer la suppression", $"Supprimer « {item.Label} » ?", "Supprimer", "Annuler");
        if (!confirm) return;

        var deleted = await _api.DeletePersonalPasswordAsync(item.Id);
        if (!deleted)
        {
            await page.DisplayAlert("Erreur", "Impossible de supprimer (êtes-vous en ligne ?).", "OK");
            return;
        }
        await LoadAsync();
    }

    [RelayCommand]
    private async Task AddDocumentAsync()
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null || IsBusy) return;

        IsBusy = true;
        try
        {
            var (success, error) = await _documents.PickAndUploadAsync();
            if (error is not null)
            {
                await page.DisplayAlert("Erreur", error, "OK");
                return;
            }
            if (success) await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenDocumentAsync(PersonalDocumentItem item)
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null) return;

        var (success, error) = await _documents.DownloadAndOpenAsync(item.Id, item.FileName);
        if (!success && error is not null) await page.DisplayAlert("Erreur", error, "OK");
    }

    [RelayCommand]
    private async Task DeleteDocumentAsync(PersonalDocumentItem item)
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null) return;

        var confirm = await page.DisplayAlert("Confirmer la suppression", $"Supprimer « {item.FileName} » ?", "Supprimer", "Annuler");
        if (!confirm) return;

        var deleted = await _api.DeletePersonalDocumentAsync(item.Id);
        if (!deleted)
        {
            await page.DisplayAlert("Erreur", "Impossible de supprimer (êtes-vous en ligne ?).", "OK");
            return;
        }
        await LoadAsync();
    }
}
