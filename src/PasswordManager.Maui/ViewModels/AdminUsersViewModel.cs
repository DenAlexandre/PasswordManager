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

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;

    public ObservableCollection<AdminUserItem> Users { get; } = new();

    public AdminUsersViewModel(ApiClient api)
    {
        _api = api;
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

        var created = await _api.CreateUserAsync(new CreateUserRequest(email, tempPassword, isAdmin));
        if (created is null)
        {
            await page.DisplayAlert("Erreur", "Impossible de créer l'utilisateur (email déjà utilisé ?).", "OK");
            return;
        }

        await LoadAsync();
    }

    [RelayCommand]
    private async Task GoToGroupsAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.AdminSiteGroupsPage));
    }
}
