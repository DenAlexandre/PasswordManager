using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Data;
using PasswordManager.Maui.Models;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

public partial class VaultTreeViewModel : ObservableObject
{
    private readonly LocalCacheDb _cache;
    private readonly SyncService _sync;
    private readonly AuthService _auth;
    private readonly ApiClient _api;

    private ILookup<Guid, CachedSite> _sitesByGroup = Enumerable.Empty<CachedSite>().ToLookup(s => s.SiteGroupId);

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isAdmin;
    [ObservableProperty] private string? errorMessage;

    public ObservableCollection<VaultTreeRow> Rows { get; } = new();

    public VaultTreeViewModel(LocalCacheDb cache, SyncService sync, VaultSession session, AuthService auth, ApiClient api)
    {
        _cache = cache;
        _sync = sync;
        _auth = auth;
        _api = api;
        IsAdmin = session.IsAdmin;
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
            _sitesByGroup = (await _cache.GetAllSitesAsync()).ToLookup(s => s.SiteGroupId);

            var expandedGroupIds = Rows.Where(r => r.IsGroup && r.IsExpanded).Select(r => r.Id).ToHashSet();
            Rows.Clear();
            foreach (var g in groups)
            {
                var role = (AccessRole)g.Role;
                var groupRow = new VaultTreeRow
                {
                    IsGroup = true,
                    Id = g.Id,
                    Name = g.Name,
                    Subtitle = role switch
                    {
                        AccessRole.Write => "Lecture/Écriture",
                        AccessRole.Read => "Lecture seule",
                        _ => "Aucun accès"
                    },
                    CanWrite = role == AccessRole.Write,
                    IsExpanded = expandedGroupIds.Contains(g.Id)
                };
                Rows.Add(groupRow);
                if (groupRow.IsExpanded) InsertSiteRows(groupRow);
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

    private void InsertSiteRows(VaultTreeRow groupRow)
    {
        var insertIndex = Rows.IndexOf(groupRow) + 1;
        foreach (var s in _sitesByGroup[groupRow.Id].OrderBy(s => s.Name))
        {
            Rows.Insert(insertIndex++, new VaultTreeRow
            {
                IsGroup = false,
                Id = s.Id,
                ParentGroupId = groupRow.Id,
                Name = s.Name,
                Subtitle = s.Url
            });
        }
    }

    [RelayCommand]
    private async Task TapRowAsync(VaultTreeRow row)
    {
        if (row.IsGroup)
        {
            if (row.IsExpanded)
            {
                foreach (var child in Rows.Where(r => !r.IsGroup && r.ParentGroupId == row.Id).ToList())
                    Rows.Remove(child);
                row.IsExpanded = false;
            }
            else
            {
                row.IsExpanded = true;
                InsertSiteRows(row);
            }
            return;
        }

        await Shell.Current.GoToAsync(
            $"{nameof(Views.CredentialsPage)}?siteId={row.Id}&siteName={Uri.EscapeDataString(row.Name)}&siteGroupId={row.ParentGroupId}");
    }

    [RelayCommand]
    private async Task AddSiteAsync(VaultTreeRow groupRow)
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null || !groupRow.IsGroup) return;

        var name = await page.DisplayPromptAsync("Nouveau site", "Nom du site client");
        if (string.IsNullOrWhiteSpace(name)) return;
        var url = await page.DisplayPromptAsync("Nouveau site", "URL (optionnel)");

        var created = await _api.CreateSiteAsync(groupRow.Id, new UpsertSiteRequest(name, url, null));
        if (created is null)
        {
            await page.DisplayAlert("Erreur", "Impossible de créer le site (êtes-vous en ligne ?).", "OK");
            return;
        }

        await _cache.UpsertSitesAsync(new[]
        {
            new CachedSite { Id = created.Id, SiteGroupId = groupRow.Id, Name = created.Name, Url = created.Url, Notes = created.Notes, UpdatedAt = created.UpdatedAt }
        });

        if (!groupRow.IsExpanded)
        {
            groupRow.IsExpanded = true;
        }
        await LoadAsync();
    }

    [RelayCommand]
    private async Task OpenAdminAsync() => await Shell.Current.GoToAsync(nameof(Views.AdminSiteGroupsPage));

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        await Shell.Current.GoToAsync($"//{nameof(Views.LoginPage)}");
    }
}
