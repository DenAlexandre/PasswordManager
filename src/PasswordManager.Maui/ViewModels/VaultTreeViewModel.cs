using System.Collections.ObjectModel;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Crypto;
using PasswordManager.Maui.Data;
using PasswordManager.Maui.Models;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

// KeePass-style tree: SiteGroup > Folder (nested arbitrarily via Site.ParentSiteId) > Entry
// (Credential, a leaf). Flattened into one ObservableCollection since MAUI has no TreeView;
// Group/Folder rows insert/remove their direct children on expand/collapse.
public partial class VaultTreeViewModel : ObservableObject
{
    private readonly LocalCacheDb _cache;
    private readonly SyncService _sync;
    private readonly VaultSession _session;
    private readonly AuthService _auth;
    private readonly ApiClient _api;

    private Dictionary<Guid, CachedSiteGroup> _groupsById = new();
    private ILookup<Guid?, CachedSite> _foldersByParent = Enumerable.Empty<CachedSite>().ToLookup(s => s.ParentSiteId);
    private ILookup<Guid, CachedCredential> _credentialsByFolder = Enumerable.Empty<CachedCredential>().ToLookup(c => c.SiteId);

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public ObservableCollection<VaultTreeRow> Rows { get; } = new();

    public VaultTreeViewModel(LocalCacheDb cache, SyncService sync, VaultSession session, AuthService auth, ApiClient api)
    {
        _cache = cache;
        _sync = sync;
        _session = session;
        _auth = auth;
        _api = api;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        // RefreshView re-invokes this command whenever IsRefreshing flips to true, including
        // when WE set it below - guard against the resulting reentrant call.
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            _ = await _sync.SyncAsync();
            var groups = (await _cache.GetSiteGroupsAsync()).OrderBy(g => g.Name).ToList();
            _groupsById = groups.ToDictionary(g => g.Id);
            _foldersByParent = (await _cache.GetAllSitesAsync()).ToLookup(s => s.ParentSiteId);
            _credentialsByFolder = (await _cache.GetAllCredentialsAsync()).ToLookup(c => c.SiteId);

            var expandedIds = Rows.Where(r => r.Kind != TreeRowKind.Entry && r.IsExpanded).Select(r => r.Id).ToHashSet();
            var flat = new List<VaultTreeRow>();
            foreach (var g in groups)
            {
                var role = (AccessRole)g.Role;
                var groupRow = new VaultTreeRow
                {
                    Kind = TreeRowKind.Group,
                    Id = g.Id,
                    GroupId = g.Id,
                    Depth = 0,
                    Name = g.Name,
                    Subtitle = role switch
                    {
                        AccessRole.Write => "Lecture/Écriture",
                        AccessRole.Read => "Lecture seule",
                        _ => "Aucun accès"
                    },
                    CanWrite = role == AccessRole.Write,
                    IsExpanded = expandedIds.Contains(g.Id)
                };
                flat.Add(groupRow);
                if (groupRow.IsExpanded)
                    AppendExpandedChildren(flat, g, parentFolderId: null, depth: 1, groupRow.CanWrite, expandedIds);
            }

            Rows.Clear();
            foreach (var row in flat) Rows.Add(row);
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

    // Recursively appends the already-expanded subtree under (group, parentFolderId).
    private void AppendExpandedChildren(List<VaultTreeRow> flat, CachedSiteGroup group, Guid? parentFolderId, int depth, bool canWrite, HashSet<Guid> expandedIds)
    {
        foreach (var folder in _foldersByParent[parentFolderId].Where(s => s.SiteGroupId == group.Id).OrderBy(s => s.Name))
        {
            var folderRow = new VaultTreeRow
            {
                Kind = TreeRowKind.Folder,
                Id = folder.Id,
                GroupId = group.Id,
                ParentFolderId = parentFolderId,
                Depth = depth,
                Name = folder.Name,
                Subtitle = folder.Url,
                CanWrite = canWrite,
                IsExpanded = expandedIds.Contains(folder.Id)
            };
            flat.Add(folderRow);
            if (folderRow.IsExpanded)
                AppendExpandedChildren(flat, group, folder.Id, depth + 1, canWrite, expandedIds);
        }

        if (parentFolderId is not null)
            flat.AddRange(BuildEntryRows(group, parentFolderId.Value, depth, canWrite));
    }

    private List<VaultTreeRow> BuildEntryRows(CachedSiteGroup group, Guid folderId, int depth, bool canWrite)
    {
        var result = new List<VaultTreeRow>();
        byte[] groupKey;
        try
        {
            groupKey = _session.GetOrUnwrapGroupKey(group.Id, group.EncryptedGroupKey);
        }
        catch (InvalidOperationException)
        {
            return result; // vault locked - shouldn't happen post-unlock, but don't crash the tree
        }

        foreach (var c in _credentialsByFolder[folderId])
        {
            try
            {
                var label = AesGcmCipher.Decrypt(groupKey, c.EncryptedLabel);
                var username = AesGcmCipher.Decrypt(groupKey, c.EncryptedUsername);
                var password = AesGcmCipher.Decrypt(groupKey, c.EncryptedPassword);
                var url = c.EncryptedUrl is null ? null : AesGcmCipher.Decrypt(groupKey, c.EncryptedUrl);
                var notes = c.EncryptedNotes is null ? null : AesGcmCipher.Decrypt(groupKey, c.EncryptedNotes);

                result.Add(new VaultTreeRow
                {
                    Kind = TreeRowKind.Entry,
                    Id = c.Id,
                    GroupId = group.Id,
                    ParentFolderId = folderId,
                    Depth = depth,
                    Name = label,
                    Subtitle = username,
                    Username = username,
                    Password = password,
                    Url = url,
                    Notes = notes,
                    CanWrite = canWrite
                });
            }
            catch (CryptographicException)
            {
                // Corrupt/foreign entry - skip rather than crash the whole list.
            }
        }
        return result.OrderBy(e => e.Name).ToList();
    }

    private void Toggle(VaultTreeRow row)
    {
        if (row.IsExpanded)
        {
            var startIndex = Rows.IndexOf(row) + 1;
            var descendantCount = 0;
            while (startIndex + descendantCount < Rows.Count && Rows[startIndex + descendantCount].Depth > row.Depth)
                descendantCount++;
            for (var i = 0; i < descendantCount; i++)
                Rows.RemoveAt(startIndex);
            row.IsExpanded = false;
            return;
        }

        row.IsExpanded = true;
        var group = _groupsById[row.GroupId];
        var parentFolderId = row.Kind == TreeRowKind.Group ? (Guid?)null : row.Id;
        var children = new List<VaultTreeRow>();
        AppendExpandedChildren(children, group, parentFolderId, row.Depth + 1, row.CanWrite, expandedIds: new HashSet<Guid>());

        var insertIndex = Rows.IndexOf(row) + 1;
        foreach (var child in children) Rows.Insert(insertIndex++, child);
    }

    [RelayCommand]
    private async Task TapRowAsync(VaultTreeRow row)
    {
        if (row.Kind == TreeRowKind.Entry)
        {
            await Shell.Current.GoToAsync(nameof(Views.CredentialEditPage), new Dictionary<string, object>
            {
                ["siteId"] = row.ParentFolderId!.Value,
                ["siteGroupId"] = row.GroupId,
                ["item"] = row.ToCredentialItem()
            });
            return;
        }

        Toggle(row);
    }

    [RelayCommand]
    private void ToggleReveal(VaultTreeRow row) => row.IsRevealed = !row.IsRevealed;

    [RelayCommand]
    private async Task CopyPasswordAsync(VaultTreeRow row) => await Clipboard.SetTextAsync(row.Password);

    [RelayCommand]
    private async Task AddAsync(VaultTreeRow row)
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null) return;

        var choice = "Nouveau dossier";
        if (row.Kind == TreeRowKind.Folder)
        {
            var picked = await page.DisplayActionSheet("Ajouter", "Annuler", null, "Nouveau dossier", "Nouvelle entrée");
            if (picked is null || picked == "Annuler") return;
            choice = picked;
        }

        if (choice == "Nouvelle entrée")
        {
            await Shell.Current.GoToAsync(nameof(Views.CredentialEditPage), new Dictionary<string, object>
            {
                ["siteId"] = row.Id,
                ["siteGroupId"] = row.GroupId
            });
            return;
        }

        var name = await page.DisplayPromptAsync("Nouveau dossier", "Nom du dossier");
        if (string.IsNullOrWhiteSpace(name)) return;
        var url = await page.DisplayPromptAsync("Nouveau dossier", "URL (optionnel)");

        var parentSiteId = row.Kind == TreeRowKind.Group ? (Guid?)null : row.Id;
        var created = await _api.CreateSiteAsync(row.GroupId, new UpsertSiteRequest(name, url, null, parentSiteId));
        if (created is null)
        {
            await page.DisplayAlert("Erreur", "Impossible de créer le dossier (êtes-vous en ligne ?).", "OK");
            return;
        }

        await _cache.UpsertSitesAsync(new[]
        {
            new CachedSite
            {
                Id = created.Id, SiteGroupId = row.GroupId, ParentSiteId = created.ParentSiteId,
                Name = created.Name, Url = created.Url, Notes = created.Notes, UpdatedAt = created.UpdatedAt
            }
        });

        row.IsExpanded = true; // captured by LoadAsync's expand-state preservation, revealing the new folder
        await LoadAsync();
    }

    [RelayCommand]
    private async Task EditNodeAsync(VaultTreeRow row)
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null || row.Kind == TreeRowKind.Entry) return;

        if (row.Kind == TreeRowKind.Group)
        {
            var groupName = await page.DisplayPromptAsync("Modifier le groupe", "Nom du groupe", initialValue: row.Name);
            if (string.IsNullOrWhiteSpace(groupName)) return;

            var groupUpdated = await _api.UpdateSiteGroupAsync(row.Id, new UpdateSiteGroupRequest(groupName, null));
            if (!groupUpdated)
            {
                await page.DisplayAlert("Erreur", "Impossible de modifier le groupe (êtes-vous en ligne ?).", "OK");
                return;
            }
            await LoadAsync();
            return;
        }

        var name = await page.DisplayPromptAsync("Modifier le dossier", "Nom du dossier", initialValue: row.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        var url = await page.DisplayPromptAsync("Modifier le dossier", "URL (optionnel)", initialValue: row.Subtitle);

        var updated = await _api.UpdateSiteAsync(row.GroupId, row.Id, new UpsertSiteRequest(name, url, null, row.ParentFolderId));
        if (!updated)
        {
            await page.DisplayAlert("Erreur", "Impossible de modifier le dossier (êtes-vous en ligne ?).", "OK");
            return;
        }
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteNodeAsync(VaultTreeRow row)
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null || row.Kind == TreeRowKind.Entry) return;

        if (row.Kind == TreeRowKind.Group)
        {
            var confirmGroup = await page.DisplayAlert("Confirmer la suppression",
                $"Supprimer le groupe « {row.Name} » ? Il doit être vide (pas de dossier à l'intérieur). " +
                "Cela retire aussi l'accès de tous les utilisateurs à ce groupe.",
                "Supprimer", "Annuler");
            if (!confirmGroup) return;

            var (groupSuccess, groupError) = await _api.DeleteSiteGroupWithReasonAsync(row.Id);
            if (!groupSuccess)
            {
                await page.DisplayAlert("Erreur", groupError ?? "Impossible de supprimer le groupe.", "OK");
                return;
            }
            await LoadAsync();
            return;
        }

        var confirm = await page.DisplayAlert("Confirmer la suppression",
            $"Supprimer le dossier « {row.Name} » ? Il doit être vide (pas de sous-dossier ni de mot de passe).",
            "Supprimer", "Annuler");
        if (!confirm) return;

        var (success, error) = await _api.DeleteSiteWithReasonAsync(row.GroupId, row.Id);
        if (!success)
        {
            await page.DisplayAlert("Erreur", error ?? "Impossible de supprimer le dossier.", "OK");
            return;
        }
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteEntryAsync(VaultTreeRow row)
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null || row.Kind != TreeRowKind.Entry) return;

        var confirm = await page.DisplayAlert("Confirmer la suppression",
            $"Supprimer le mot de passe « {row.Name} » ? Cette action est irréversible.", "Supprimer", "Annuler");
        if (!confirm) return;

        var deleted = await _api.DeleteCredentialAsync(row.ParentFolderId!.Value, row.Id);
        if (!deleted)
        {
            await page.DisplayAlert("Erreur", "Impossible de supprimer (êtes-vous en ligne ?).", "OK");
            return;
        }
        await LoadAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        await Shell.Current.GoToAsync($"//{nameof(Views.LoginPage)}");
    }
}
