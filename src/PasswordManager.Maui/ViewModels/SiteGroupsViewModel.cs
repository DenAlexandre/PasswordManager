using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Data;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

public class SiteGroupItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
}

public partial class SiteGroupsViewModel : ObservableObject
{
    private readonly LocalCacheDb _cache;
    private readonly SyncService _sync;
    private readonly VaultSession _session;
    private readonly AuthService _auth;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isAdmin;
    [ObservableProperty] private string? errorMessage;

    public ObservableCollection<SiteGroupItem> SiteGroups { get; } = new();

    public SiteGroupsViewModel(LocalCacheDb cache, SyncService sync, VaultSession session, AuthService auth)
    {
        _cache = cache;
        _sync = sync;
        _session = session;
        _auth = auth;
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
            var groups = await _cache.GetSiteGroupsAsync();
            SiteGroups.Clear();
            foreach (var g in groups.OrderBy(g => g.Name))
            {
                SiteGroups.Add(new SiteGroupItem
                {
                    Id = g.Id,
                    Name = g.Name,
                    RoleLabel = ((PasswordManager.Maui.Models.AccessRole)g.Role) switch
                    {
                        Models.AccessRole.Write => "Lecture/Écriture",
                        Models.AccessRole.Read => "Lecture seule",
                        _ => "Aucun accès"
                    }
                });
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

    [RelayCommand]
    private async Task OpenAsync(SiteGroupItem item)
    {
        await Shell.Current.GoToAsync($"{nameof(Views.SitesPage)}?siteGroupId={item.Id}&siteGroupName={Uri.EscapeDataString(item.Name)}");
    }

    [RelayCommand]
    private async Task OpenAdminAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.AdminSiteGroupsPage));
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _auth.LogoutAsync();
        await Shell.Current.GoToAsync($"//{nameof(Views.LoginPage)}");
    }
}
