using PasswordManager.Maui.Services;
using PasswordManager.Maui.Views;

namespace PasswordManager.Maui;

public partial class AppShell : Shell
{
    public AppShell(VaultSession session)
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(VaultUnlockPage), typeof(VaultUnlockPage));
        Routing.RegisterRoute(nameof(CredentialEditPage), typeof(CredentialEditPage));

        // The admin flyout entry only makes sense once we know who's logged in - re-evaluate on
        // every navigation so login/logout/switching accounts keeps the menu in sync.
        Navigated += (_, _) => UsersFlyoutItem.IsVisible = session.IsAdmin;
    }
}
