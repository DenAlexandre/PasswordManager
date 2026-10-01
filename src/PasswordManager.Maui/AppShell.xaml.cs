using PasswordManager.Maui.Services;
using PasswordManager.Maui.Views;

namespace PasswordManager.Maui;

public partial class AppShell : Shell
{
    public AppShell(VaultSession session, ConnectivityService connectivity)
    {
        InitializeComponent();
        BindingContext = connectivity;

        Routing.RegisterRoute(nameof(VaultUnlockPage), typeof(VaultUnlockPage));
        Routing.RegisterRoute(nameof(CredentialEditPage), typeof(CredentialEditPage));
        Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));
        Routing.RegisterRoute(nameof(VerifyEmailPage), typeof(VerifyEmailPage));
        Routing.RegisterRoute(nameof(PersonalPasswordEditPage), typeof(PersonalPasswordEditPage));

        // The admin flyout entry only makes sense once we know who's logged in - re-evaluate on
        // every navigation so login/logout/switching accounts keeps the menu in sync.
        Navigated += (_, _) =>
        {
            UsersFlyoutItem.IsVisible = session.IsAdmin;
            BackupExportFlyoutItem.IsVisible = session.IsAdmin;
        };
    }
}
