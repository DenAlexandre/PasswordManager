using PasswordManager.Maui.Views;

namespace PasswordManager.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(VaultUnlockPage), typeof(VaultUnlockPage));
        Routing.RegisterRoute(nameof(SiteGroupsPage), typeof(SiteGroupsPage));
        Routing.RegisterRoute(nameof(SitesPage), typeof(SitesPage));
        Routing.RegisterRoute(nameof(CredentialsPage), typeof(CredentialsPage));
        Routing.RegisterRoute(nameof(AdminUsersPage), typeof(AdminUsersPage));
        Routing.RegisterRoute(nameof(AdminSiteGroupsPage), typeof(AdminSiteGroupsPage));
    }
}
