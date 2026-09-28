using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Models;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

public class RestorePlanItem
{
    public Guid GroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SummaryLabel { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public class RestoreResultItem
{
    public string Name { get; set; } = string.Empty;
    public string SummaryLabel { get; set; } = string.Empty;
    public List<string> Details { get; set; } = new();
    public string DetailsText => string.Join("\n", Details);
}

// Reachable by any unlocked user, not just admins: restoring missing sites/credentials into an
// already-existing group only needs Write role. Recreating a fully-missing group requires admin
// rights, but that split is enforced server-side and reported per group, not gated on this page.
public partial class BackupImportViewModel : ObservableObject
{
    private readonly BackupService _backup;
    private readonly SyncService _sync;

    private BackupFileV1? _loadedBackup;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool hasPlan;
    [ObservableProperty] private bool hasResults;

    public ObservableCollection<RestorePlanItem> Plan { get; } = new();
    public ObservableCollection<RestoreResultItem> Results { get; } = new();

    public BackupImportViewModel(BackupService backup, SyncService sync)
    {
        _backup = backup;
        _sync = sync;
    }

    [RelayCommand]
    private async Task PickFileAsync()
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null) return;
        if (IsBusy) return;

        IsBusy = true;
        StatusMessage = null;
        HasResults = false;
        Results.Clear();
        try
        {
            var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".json" } },
                { DevicePlatform.Android, new[] { "application/json" } },
                { DevicePlatform.iOS, new[] { "public.json" } },
                { DevicePlatform.MacCatalyst, new[] { "public.json" } },
            });
            var pickResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choisir un fichier de sauvegarde",
                FileTypes = fileTypes
            });
            if (pickResult is null) return;

            using var stream = await pickResult.OpenReadAsync();
            var (backup, error) = await _backup.LoadFromFileAsync(stream);
            if (backup is null)
            {
                await page.DisplayAlert("Erreur", error, "OK");
                return;
            }

            await _sync.SyncAsync();
            var plan = await _backup.BuildRestorePreviewAsync(backup);

            _loadedBackup = backup;
            Plan.Clear();
            foreach (var item in plan)
            {
                var summary = item.AlreadyExists
                    ? (item.SitesToCreate == 0 && item.CredentialsToCreate == 0
                        ? "déjà à jour"
                        : $"{item.SitesToCreate} dossier(s) et {item.CredentialsToCreate} identifiant(s) manquants")
                    : $"database absente - {item.SitesToCreate} dossier(s), {item.CredentialsToCreate} identifiant(s) à recréer";

                Plan.Add(new RestorePlanItem
                {
                    GroupId = item.GroupId,
                    Name = item.Name,
                    SummaryLabel = summary,
                    IsSelected = item.SitesToCreate > 0 || item.CredentialsToCreate > 0
                });
            }
            HasPlan = Plan.Count > 0;
            if (!HasPlan) StatusMessage = "Ce fichier ne contient aucune database.";
        }
        catch (Exception ex)
        {
            await page.DisplayAlert("Erreur", $"Impossible de lire le fichier : {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmRestoreAsync()
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null || _loadedBackup is null) return;

        var selectedIds = Plan.Where(p => p.IsSelected).Select(p => p.GroupId).ToList();
        if (selectedIds.Count == 0)
        {
            await page.DisplayAlert("Info", "Sélectionnez au moins une database à restaurer.", "OK");
            return;
        }

        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var results = await _backup.RestoreAsync(_loadedBackup, selectedIds);
            Results.Clear();
            foreach (var r in results)
            {
                var details = new List<string>();
                if (r.Skipped)
                {
                    details.Add($"Ignoré - {r.SkipReason}");
                }
                else
                {
                    details.Add($"{r.SitesCreated} dossier(s) et {r.CredentialsCreated} identifiant(s) créés.");
                    details.AddRange(r.Warnings);
                    details.AddRange(r.Errors);
                }

                Results.Add(new RestoreResultItem
                {
                    Name = r.Name,
                    SummaryLabel = r.Skipped ? "Ignoré" : "Restauré",
                    Details = details
                });
            }
            HasResults = Results.Count > 0;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
