using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Data;
using PasswordManager.Maui.Models;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

public class SiteItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Url { get; set; }
}

[QueryProperty(nameof(SiteGroupId), "siteGroupId")]
[QueryProperty(nameof(SiteGroupName), "siteGroupName")]
public partial class SitesViewModel : ObservableObject
{
    private readonly LocalCacheDb _cache;
    private readonly ApiClient _api;

    [ObservableProperty] private string siteGroupId = string.Empty;
    [ObservableProperty] private string siteGroupName = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool canWrite;
    [ObservableProperty] private string? errorMessage;

    public ObservableCollection<SiteItem> Sites { get; } = new();

    public SitesViewModel(LocalCacheDb cache, ApiClient api)
    {
        _cache = cache;
        _api = api;
    }

    private Guid GroupGuid => Guid.Parse(SiteGroupId);

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrEmpty(SiteGroupId) || IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var groups = await _cache.GetSiteGroupsAsync();
            var group = groups.FirstOrDefault(g => g.Id == GroupGuid);
            CanWrite = group is not null && (AccessRole)group.Role == AccessRole.Write;

            var sites = await _cache.GetSitesAsync(GroupGuid);
            Sites.Clear();
            foreach (var s in sites.OrderBy(s => s.Name))
                Sites.Add(new SiteItem { Id = s.Id, Name = s.Name, Url = s.Url });
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
    private async Task OpenAsync(SiteItem item)
    {
        await Shell.Current.GoToAsync(
            $"{nameof(Views.CredentialsPage)}?siteId={item.Id}&siteName={Uri.EscapeDataString(item.Name)}&siteGroupId={SiteGroupId}");
    }

    [RelayCommand]
    private async Task AddSiteAsync()
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null) return;

        var name = await page.DisplayPromptAsync("Nouveau site", "Nom du site client");
        if (string.IsNullOrWhiteSpace(name)) return;
        var url = await page.DisplayPromptAsync("Nouveau site", "URL (optionnel)");

        var created = await _api.CreateSiteAsync(GroupGuid, new UpsertSiteRequest(name, url, null));
        if (created is null)
        {
            await page.DisplayAlert("Erreur", "Impossible de créer le site (êtes-vous en ligne ?).", "OK");
            return;
        }

        await _cache.UpsertSitesAsync(new[]
        {
            new CachedSite { Id = created.Id, SiteGroupId = GroupGuid, Name = created.Name, Url = created.Url, Notes = created.Notes, UpdatedAt = created.UpdatedAt }
        });
        await LoadAsync();
    }
}
