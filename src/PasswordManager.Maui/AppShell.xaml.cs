using PasswordManager.Maui.Views;

namespace PasswordManager.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(VaultUnlockPage), typeof(VaultUnlockPage));
        Routing.RegisterRoute(nameof(VaultTreePage), typeof(VaultTreePage));
        Routing.RegisterRoute(nameof(CredentialsPage), typeof(CredentialsPage));
        Routing.RegisterRoute(nameof(CredentialEditPage), typeof(CredentialEditPage));
        Routing.RegisterRoute(nameof(AdminUsersPage), typeof(AdminUsersPage));
        Routing.RegisterRoute(nameof(AdminSiteGroupsPage), typeof(AdminSiteGroupsPage));
    }
}
