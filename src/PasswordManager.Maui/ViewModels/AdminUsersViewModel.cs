using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PasswordManager.Maui.Models;
using PasswordManager.Maui.Services;

namespace PasswordManager.Maui.ViewModels;

public class AdminUserItem
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string StatusLabel { get; set; } = string.Empty;
    public string? PublicKey { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; }
}

public class UserAccessItem
{
    public Guid SiteGroupId { get; set; }
    public string SiteGroupName { get; set; } = string.Empty;
    public AccessRole Role { get; set; }
    public string RoleLabel { get; set; } = string.Empty;
}

// Master-detail admin screen: master list = users, detail = the selected user's site-group
// access (grant/revoke), plus user creation. Replaces the earlier split Users/SiteGroups pages -
// everything reachable from one "Gestion des utilisateurs" flyout entry.
public partial class AdminUsersViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly VaultSession _session;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedUser))]
    private AdminUserItem? selectedUser;

    public bool HasSelectedUser => SelectedUser is not null;

    public ObservableCollection<AdminUserItem> Users { get; } = new();
    public ObservableCollection<UserAccessItem> SelectedUserAccess { get; } = new();

    public AdminUsersViewModel(ApiClient api, VaultSession session)
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
            var users = await _api.GetUsersAsync();
            if (users is null)
            {
                ErrorMessage = "Impossible de charger les utilisateurs (connexion requise).";
                return;
            }

            Users.Clear();
            foreach (var u in users)
            {
                var status = u.IsAdmin ? "Admin" : "Utilisateur";
                if (!u.IsActive) status += " · désactivé";
                if (!u.VaultSetUp) status += " · coffre non initialisé";
                Users.Add(new AdminUserItem
                {
                    Id = u.Id, Email = u.Email, StatusLabel = status, PublicKey = u.PublicKey,
                    IsAdmin = u.IsAdmin, IsActive = u.IsActive
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
    private async Task SelectUserAsync(AdminUserItem user)
    {
        SelectedUser = user;
        await LoadSelectedUserAccessAsync();
    }

    private async Task LoadSelectedUserAccessAsync()
    {
        if (SelectedUser is null) return;
        var access = await _api.GetUserAccessAsync(SelectedUser.Id);
        SelectedUserAccess.Clear();
        if (access is null) return;
        foreach (var a in access)
        {
            SelectedUserAccess.Add(new UserAccessItem
            {
                SiteGroupId = a.SiteGroupId,
                SiteGroupName = a.SiteGroupName,
                Role = a.Role,
                RoleLabel = a.Role switch { AccessRole.Write => "Lecture/Écriture", AccessRole.Read => "Lecture seule", _ => "Aucun" }
            });
        }
    }

    [RelayCommand]
    private async Task GrantAccessAsync()
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null) return;
        if (SelectedUser is null)
        {
            await page.DisplayAlert("Info", "Sélectionnez d'abord un utilisateur dans la liste.", "OK");
            return;
        }
        if (SelectedUser.PublicKey is null)
        {
            await page.DisplayAlert("Info",
                "Cet utilisateur doit d'abord se connecter et créer son coffre avant de recevoir un accès.", "OK");
            return;
        }

        var allGroups = await _api.GetSiteGroupsAsync();
        if (allGroups is null) { await page.DisplayAlert("Erreur", "Connexion requise.", "OK"); return; }

        var alreadyGrantedIds = SelectedUserAccess.Select(a => a.SiteGroupId).ToHashSet();
        var available = allGroups.Where(g => !alreadyGrantedIds.Contains(g.Id)).ToList();

        const string newGroupOption = "➕ Nouvelle database";
        var options = available.Select(g => g.Name).Append(newGroupOption).ToArray();
        var choice = await page.DisplayActionSheet("Choisir une database", "Annuler", null, options);
        if (choice is null || choice == "Annuler") return;

        Guid groupId;
        byte[] groupKey;
        if (choice == newGroupOption)
        {
            var name = await page.DisplayPromptAsync("Nouvelle database", "Nom de la database (ex: Client Dupont)");
            if (string.IsNullOrWhiteSpace(name)) return;

            var keyMaterial = await _api.GetKeyMaterialAsync();
            if (keyMaterial is null)
            {
                await page.DisplayAlert("Erreur", "Connexion requise pour créer une database.", "OK");
                return;
            }

            var tempId = Guid.NewGuid(); // placeholder to seed the in-memory key cache before the server assigns the real id
            var (_, newKey) = _session.CreateNewGroupKey(tempId);
            var wrappedForSelf = Crypto.RsaKeyWrapping.WrapKey(keyMaterial.PublicKey, newKey);
            var createdGroup = await _api.CreateSiteGroupAsync(new CreateSiteGroupRequest(name, null, wrappedForSelf));
            if (createdGroup is null)
            {
                await page.DisplayAlert("Erreur", "Impossible de créer la database.", "OK");
                return;
            }

            groupId = createdGroup.Id;
            groupKey = _session.GetOrUnwrapGroupKey(groupId, wrappedForSelf);
        }
        else
        {
            var group = available.First(g => g.Name == choice);
            groupId = group.Id;

            // Recover our own wrapped copy of this group's key (we're auto-added as a member when
            // we create a group) so we can unwrap it locally and re-wrap it for the target user.
            var mine = await _api.GetMySiteGroupsAsync();
            var ownEntry = mine?.FirstOrDefault(g => g.Id == groupId);
            if (ownEntry is null)
            {
                await page.DisplayAlert("Erreur", "Vous n'avez pas la clé de cette database.", "OK");
                return;
            }
            groupKey = _session.GetOrUnwrapGroupKey(groupId, ownEntry.EncryptedGroupKey);
        }

        var roleChoice = await page.DisplayActionSheet("Rôle", "Annuler", null, "Lecture seule", "Lecture/Écriture");
        if (roleChoice is null || roleChoice == "Annuler") return;
        var role = roleChoice == "Lecture/Écriture" ? AccessRole.Write : AccessRole.Read;

        try
        {
            var wrapped = Crypto.RsaKeyWrapping.WrapKey(SelectedUser.PublicKey!, groupKey);
            var granted = await _api.GrantAccessAsync(groupId, new GrantAccessRequest(SelectedUser.Id, role, wrapped));
            if (!granted)
            {
                await page.DisplayAlert("Erreur", "Le serveur a refusé d'accorder l'accès.", "OK");
                return;
            }
            await LoadSelectedUserAccessAsync();
        }
        catch (Exception ex)
        {
            await page.DisplayAlert("Erreur", $"Impossible d'accorder l'accès : {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task RevokeAccessAsync(UserAccessItem item)
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null || SelectedUser is null) return;

        var confirm = await page.DisplayAlert("Confirmer",
            $"Retirer l'accès de {SelectedUser.Email} à la database « {item.SiteGroupName} » ?", "Retirer", "Annuler");
        if (!confirm) return;

        var revoked = await _api.RevokeAccessAsync(item.SiteGroupId, SelectedUser.Id);
        if (!revoked)
        {
            await page.DisplayAlert("Erreur", "Le serveur a refusé de retirer l'accès.", "OK");
            return;
        }
        await LoadSelectedUserAccessAsync();
    }

    [RelayCommand]
    private async Task EditAccessRoleAsync(UserAccessItem item)
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null || SelectedUser is null) return;

        var choice = await page.DisplayActionSheet(
            $"Rôle sur « {item.SiteGroupName} »", "Annuler", null, "Lecture seule", "Lecture/Écriture");
        if (choice is null || choice == "Annuler") return;
        var role = choice == "Lecture/Écriture" ? AccessRole.Write : AccessRole.Read;
        if (role == item.Role) return;

        var updated = await _api.UpdateAccessRoleAsync(item.SiteGroupId, SelectedUser.Id, new UpdateAccessRoleRequest(role));
        if (!updated)
        {
            await page.DisplayAlert("Erreur", "Le serveur a refusé la modification du rôle.", "OK");
            return;
        }
        await LoadSelectedUserAccessAsync();
    }

    [RelayCommand]
    private async Task EditUserAsync(AdminUserItem user)
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null) return;

        var adminChoice = await page.DisplayActionSheet(
            $"{user.Email} — statut admin", "Annuler", null,
            user.IsAdmin ? "Retirer les droits admin" : "Donner les droits admin");
        if (adminChoice is not null && adminChoice != "Annuler")
        {
            var makeAdmin = adminChoice == "Donner les droits admin";
            if (!await _api.UpdateUserAsync(user.Id, new UpdateUserRequest(IsAdmin: makeAdmin, IsActive: null)))
            {
                await page.DisplayAlert("Erreur", "Le serveur a refusé la modification.", "OK");
                return;
            }
        }

        var activeChoice = await page.DisplayActionSheet(
            $"{user.Email} — statut du compte", "Annuler", null,
            user.IsActive ? "Désactiver le compte" : "Réactiver le compte");
        if (activeChoice is not null && activeChoice != "Annuler")
        {
            var makeActive = activeChoice == "Réactiver le compte";
            if (!await _api.UpdateUserAsync(user.Id, new UpdateUserRequest(IsAdmin: null, IsActive: makeActive)))
            {
                await page.DisplayAlert("Erreur", "Le serveur a refusé la modification.", "OK");
                return;
            }
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteUserAsync(AdminUserItem user)
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null) return;

        var confirm = await page.DisplayAlert("Confirmer la suppression",
            $"Supprimer définitivement {user.Email} ? Cette action est irréversible et retire tous ses accès.",
            "Supprimer", "Annuler");
        if (!confirm) return;

        var deleted = await _api.DeleteUserAsync(user.Id);
        if (!deleted)
        {
            await page.DisplayAlert("Erreur",
                "Le serveur a refusé la suppression (impossible de supprimer votre propre compte).", "OK");
            return;
        }

        if (SelectedUser?.Id == user.Id)
        {
            SelectedUser = null;
            SelectedUserAccess.Clear();
        }
        await LoadAsync();
    }
}
