using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Models;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

public class AdminGroupItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class AdminAccessItem
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
}

public partial class AdminSiteGroupsViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly VaultSession _session;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private Guid? selectedGroupId;
    [ObservableProperty] private string selectedGroupName = string.Empty;

    public ObservableCollection<AdminGroupItem> Groups { get; } = new();
    public ObservableCollection<AdminAccessItem> AccessGrants { get; } = new();

    public AdminSiteGroupsViewModel(ApiClient api, VaultSession session)
    {
        _api = api;
        _session = session;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var groups = await _api.GetSiteGroupsAsync();
            if (groups is null)
            {
                ErrorMessage = "Impossible de charger les groupes (connexion requise).";
                return;
            }
            Groups.Clear();
            foreach (var g in groups) Groups.Add(new AdminGroupItem { Id = g.Id, Name = g.Name });
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
    private async Task GoToUsersAsync() => await Shell.Current.GoToAsync(nameof(Views.AdminUsersPage));

    [RelayCommand]
    private async Task AddGroupAsync()
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null) return;

        var name = await page.DisplayPromptAsync("Nouveau groupe de sites", "Nom du groupe (ex: Client Dupont)");
        if (string.IsNullOrWhiteSpace(name)) return;

        var keyMaterial = await _api.GetKeyMaterialAsync();
        if (keyMaterial is null)
        {
            await page.DisplayAlert("Erreur", "Connexion requise pour créer un groupe.", "OK");
            return;
        }

        var tempId = Guid.NewGuid(); // placeholder used only to seed the in-memory key cache before the server assigns the real id
        var (_, groupKey) = _session.CreateNewGroupKey(tempId);
        var wrappedForSelf = Crypto.RsaKeyWrapping.WrapKey(keyMaterial.PublicKey, groupKey);

        var created = await _api.CreateSiteGroupAsync(new CreateSiteGroupRequest(name, null, wrappedForSelf));
        if (created is null)
        {
            await page.DisplayAlert("Erreur", "Impossible de créer le groupe.", "OK");
            return;
        }

        // Re-key the session cache under the server-assigned id.
        _session.GetOrUnwrapGroupKey(created.Id, wrappedForSelf);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task OpenGroupAsync(AdminGroupItem group)
    {
        SelectedGroupId = group.Id;
        SelectedGroupName = group.Name;
        await LoadAccessAsync();
    }

    private async Task LoadAccessAsync()
    {
        if (SelectedGroupId is null) return;
        var grants = await _api.GetAccessAsync(SelectedGroupId.Value);
        AccessGrants.Clear();
        if (grants is null) return;
        foreach (var g in grants)
        {
            AccessGrants.Add(new AdminAccessItem
            {
                UserId = g.UserId,
                Email = g.UserEmail,
                RoleLabel = g.Role switch { AccessRole.Write => "Lecture/Écriture", AccessRole.Read => "Lecture seule", _ => "Aucun" }
            });
        }
    }

    [RelayCommand]
    private async Task GrantAccessAsync()
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null || SelectedGroupId is null) return;

        var users = await _api.GetUsersAsync();
        if (users is null) { await page.DisplayAlert("Erreur", "Connexion requise.", "OK"); return; }

        var candidates = users.Where(u => u.PublicKey is not null).ToList();
        if (candidates.Count == 0)
        {
            await page.DisplayAlert("Info", "Aucun utilisateur avec un coffre déjà initialisé.", "OK");
            return;
        }

        var email = await page.DisplayActionSheet("Choisir un utilisateur", "Annuler", null, candidates.Select(u => u.Email).ToArray());
        var target = candidates.FirstOrDefault(u => u.Email == email);
        if (target is null) return;

        var roleChoice = await page.DisplayActionSheet("Rôle", "Annuler", null, "Lecture seule", "Lecture/Écriture");
        var role = roleChoice == "Lecture/Écriture" ? AccessRole.Write : AccessRole.Read;

        // Recover our own wrapped copy of this group's key (we're auto-added as a member when we
        // create a group) so we can unwrap it locally and re-wrap it for the target user.
        var mine = await _api.GetMySiteGroupsAsync();
        var ownEntry = mine?.FirstOrDefault(g => g.Id == SelectedGroupId.Value);
        if (ownEntry is null)
        {
            await page.DisplayAlert("Erreur", "Vous n'avez pas la clé de ce groupe.", "OK");
            return;
        }

        try
        {
            var groupKey = _session.GetOrUnwrapGroupKey(SelectedGroupId.Value, ownEntry.EncryptedGroupKey);
            var wrapped = Crypto.RsaKeyWrapping.WrapKey(target.PublicKey!, groupKey);
            var granted = await _api.GrantAccessAsync(SelectedGroupId.Value, new GrantAccessRequest(target.Id, role, wrapped));
            if (!granted)
            {
                await page.DisplayAlert("Erreur", "Le serveur a refusé d'accorder l'accès.", "OK");
                return;
            }
            await LoadAccessAsync();
        }
        catch (Exception ex)
        {
            await page.DisplayAlert("Erreur", $"Impossible d'accorder l'accès : {ex.Message}", "OK");
        }
    }
}
