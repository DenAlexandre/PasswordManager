using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

public class ExportGroupItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSelected { get; set; }
}

// Admin-only screen (gated at the flyout level, see AppShell): lets an admin pick which of their
// accessible SiteGroups to write out as a disaster-recovery backup file.
public partial class BackupExportViewModel : ObservableObject
{
    private readonly BackupService _backup;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? statusMessage;

    public ObservableCollection<ExportGroupItem> Groups { get; } = new();

    public BackupExportViewModel(BackupService backup)
    {
        _backup = backup;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var groups = await _backup.GetExportCandidatesAsync();
            Groups.Clear();
            foreach (var g in groups)
                Groups.Add(new ExportGroupItem { Id = g.Id, Name = g.Name, Description = g.Description });
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null) return;

        var selectedIds = Groups.Where(g => g.IsSelected).Select(g => g.Id).ToList();
        if (selectedIds.Count == 0)
        {
            await page.DisplayAlert("Info", "Sélectionnez au moins une database à exporter.", "OK");
            return;
        }

        if (IsBusy) return;
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var backup = await _backup.BuildExportAsync(selectedIds);
            if (backup is null)
            {
                await page.DisplayAlert("Erreur", "Impossible de préparer la sauvegarde (identité locale introuvable).", "OK");
                return;
            }

            var (success, pathOrError) = await _backup.SaveToFileAsync(backup);
            if (!success)
            {
                await page.DisplayAlert("Erreur", $"L'export a échoué : {pathOrError}", "OK");
                return;
            }

            var siteCount = backup.SiteGroups.Sum(g => g.Sites.Count);
            var credentialCount = backup.SiteGroups.Sum(g => g.Credentials.Count);
            await page.DisplayAlert("Export terminé",
                $"{backup.SiteGroups.Count} database(s), {siteCount} dossier(s), {credentialCount} identifiant(s) exportés.\n\n{pathOrError}",
                "OK");
        }
        catch (Exception ex)
        {
            await page.DisplayAlert("Erreur", $"L'export a échoué : {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
