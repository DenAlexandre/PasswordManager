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
}

public partial class AdminUsersViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly VaultSession _session;

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public ObservableCollection<AdminUserItem> Users { get; } = new();

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
                Users.Add(new AdminUserItem { Id = u.Id, Email = u.Email, StatusLabel = status });
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
    private async Task AddUserAsync()
    {
        var page = Application.Current?.Windows[0].Page;
        if (page is null) return;

        var email = await page.DisplayPromptAsync("Nouvel utilisateur", "Adresse email");
        if (string.IsNullOrWhiteSpace(email)) return;
        var tempPassword = await page.DisplayPromptAsync("Nouvel utilisateur", "Mot de passe temporaire de connexion");
        if (string.IsNullOrWhiteSpace(tempPassword)) return;
        var isAdmin = await page.DisplayAlert("Rôle", "Cet utilisateur doit-il être administrateur ?", "Oui", "Non");
        var groupName = await page.DisplayPromptAsync("Groupe de sites", "Nom du groupe dédié à cet utilisateur", initialValue: email);
        if (string.IsNullOrWhiteSpace(groupName)) return;

        var created = await _api.CreateUserAsync(new CreateUserRequest(email, tempPassword, isAdmin));
        if (created is null)
        {
            await page.DisplayAlert("Erreur", "Impossible de créer l'utilisateur (email déjà utilisé ?).", "OK");
            return;
        }

        // Create the user's dedicated group right away (wrapped for our own key, as usual - the
        // new user has no public key yet). Their access must be granted once they've logged in
        // for the first time and finished vault setup - only then does a public key exist to wrap for.
        var keyMaterial = await _api.GetKeyMaterialAsync();
        if (keyMaterial is not null)
        {
            var tempId = Guid.NewGuid();
            var (_, groupKey) = _session.CreateNewGroupKey(tempId);
            var wrappedForSelf = Crypto.RsaKeyWrapping.WrapKey(keyMaterial.PublicKey, groupKey);
            var group = await _api.CreateSiteGroupAsync(new CreateSiteGroupRequest(groupName, null, wrappedForSelf));
            if (group is not null)
            {
                _session.GetOrUnwrapGroupKey(group.Id, wrappedForSelf);
                await page.DisplayAlert("Utilisateur créé",
                    $"Groupe « {groupName} » créé. Une fois que {email} se sera connecté et aura créé son coffre, " +
                    "ouvrez ce groupe dans « Groupes de sites » et cliquez sur « Ajouter un accès » pour finaliser.",
                    "OK");
            }
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task GoToGroupsAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.AdminSiteGroupsPage));
    }
}
