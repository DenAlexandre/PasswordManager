using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Crypto;
using PasswordManager.Maui.Data;
using PasswordManager.Maui.Models;
using PasswordManager.Maui.Services;
using PasswordManager.Maui.Utils;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedNode))]
    [NotifyPropertyChangedFor(nameof(IsSelectedNodeFolder))]
    private VaultTreeRow? selectedNode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedEntry))]
    private VaultTreeRow? selectedEntry;

    public bool HasSelectedNode => SelectedNode is not null;
    public bool IsSelectedNodeFolder => SelectedNode?.Kind == TreeRowKind.Folder;
    public bool HasSelectedEntry => SelectedEntry is not null;

    public ObservableRangeCollection<VaultTreeRow> Rows { get; } = new();
    public ObservableRangeCollection<VaultTreeRow> NodeEntries { get; } = new();

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

            var expandedIds = Rows.Where(r => r.IsExpanded).Select(r => r.Id).ToHashSet();
            var previousNodeId = SelectedNode?.Id;
            var previousEntryId = SelectedEntry?.Id;
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

            Rows.ReplaceAll(flat);

            SelectedNode = null;
            SelectedEntry = null;
            NodeEntries.Clear();
            var nodeMatch = previousNodeId is Guid nodeId ? Rows.FirstOrDefault(r => r.Id == nodeId) : null;
            if (nodeMatch is not null)
            {
                SelectNode(nodeMatch);
                var entryMatch = previousEntryId is Guid entryId ? NodeEntries.FirstOrDefault(e => e.Id == entryId) : null;
                if (entryMatch is not null) SelectEntry(entryMatch);
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

    // Recursively appends the already-expanded subtree under (group, parentFolderId). Only
    // Group/Folder rows ever live in the flat left-tree list - entries are looked up on demand
    // for whichever folder is currently selected (see SelectNode) and shown in the right pane.
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
            if (descendantCount > 0) Rows.RemoveRange(startIndex, descendantCount);
            row.IsExpanded = false;
            return;
        }

        row.IsExpanded = true;
        var group = _groupsById[row.GroupId];
        var parentFolderId = row.Kind == TreeRowKind.Group ? (Guid?)null : row.Id;
        var children = new List<VaultTreeRow>();
        AppendExpandedChildren(children, group, parentFolderId, row.Depth + 1, row.CanWrite, expandedIds: new HashSet<Guid>());

        if (children.Count > 0) Rows.InsertRange(Rows.IndexOf(row) + 1, children);
    }

    // Left tree only ever holds Group/Folder rows - tapping one both toggles its sub-folders and
    // selects it, populating the right-hand pane (summary + its entries, for a Folder).
    [RelayCommand]
    private void TapRow(VaultTreeRow row)
    {
        SelectNode(row);
        Toggle(row);
    }

    private void SelectNode(VaultTreeRow row)
    {
        if (SelectedNode is not null) SelectedNode.IsSelected = false;
        row.IsSelected = true;
        SelectedNode = row;

        if (SelectedEntry is not null) SelectedEntry.IsSelected = false;
        SelectedEntry = null;

        var entries = row.Kind == TreeRowKind.Folder
            ? BuildEntryRows(_groupsById[row.GroupId], row.Id, depth: 0, row.CanWrite)
            : new List<VaultTreeRow>();
        NodeEntries.ReplaceAll(entries);
    }

    // Single click in the right-hand entries list: shows the entry's detail (read-only, with a
    // "Modifier" button). Double click/tap opens the edit page directly (EditEntryAsync below).
    [RelayCommand]
    private void SelectEntry(VaultTreeRow entry)
    {
        if (SelectedEntry is not null) SelectedEntry.IsSelected = false;
        entry.IsSelected = true;
        SelectedEntry = entry;
    }

    [RelayCommand]
    private async Task EditEntryAsync(VaultTreeRow entry)
    {
        await Shell.Current.GoToAsync(nameof(Views.CredentialEditPage), new Dictionary<string, object>
        {
            ["siteId"] = entry.ParentFolderId!.Value,
            ["siteGroupId"] = entry.GroupId,
            ["item"] = entry.ToCredentialItem()
        });
    }

    [RelayCommand]
    private void ToggleReveal(VaultTreeRow row) => row.IsRevealed = !row.IsRevealed;

    [RelayCommand]
    private async Task CopyPasswordAsync(VaultTreeRow row) => await Clipboard.SetTextAsync(row.Password);

    [RelayCommand]
    private async Task AddFolderAsync(VaultTreeRow row)
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null) return;

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

    // Only ever called on a Folder row - a Group cannot directly hold entries (see CanAddEntry).
    [RelayCommand]
    private async Task AddEntryAsync(VaultTreeRow row)
    {
        await Shell.Current.GoToAsync(nameof(Views.CredentialEditPage), new Dictionary<string, object>
        {
            ["siteId"] = row.Id,
            ["siteGroupId"] = row.GroupId
        });
    }

    [RelayCommand]
    private async Task EditNodeAsync(VaultTreeRow row)
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null || row.Kind == TreeRowKind.Entry) return;

        if (row.Kind == TreeRowKind.Group)
        {
            var groupName = await page.DisplayPromptAsync("Modifier la database", "Nom de la database", initialValue: row.Name);
            if (string.IsNullOrWhiteSpace(groupName)) return;

            var groupUpdated = await _api.UpdateSiteGroupAsync(row.Id, new UpdateSiteGroupRequest(groupName, null));
            if (!groupUpdated)
            {
                await page.DisplayAlert("Erreur", "Impossible de modifier la database (êtes-vous en ligne ?).", "OK");
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
                $"Supprimer la database « {row.Name} » ? Elle doit être vide (pas de dossier à l'intérieur). " +
                "Cela retire aussi l'accès de tous les utilisateurs à cette database.",
                "Supprimer", "Annuler");
            if (!confirmGroup) return;

            var (groupSuccess, groupError) = await _api.DeleteSiteGroupWithReasonAsync(row.Id);
            if (!groupSuccess)
            {
                await page.DisplayAlert("Erreur", groupError ?? "Impossible de supprimer la database.", "OK");
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
