using PasswordManager.Maui.Services;
using PasswordManager.Maui.ViewModels;

namespace PasswordManager.Maui.Views;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _auth;

    public LoginPage(LoginViewModel vm, AuthService auth)
    {
        InitializeComponent();
        BindingContext = vm;
        _auth = auth;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var result = await _auth.TryRestoreSessionAsync();
        switch (result)
        {
            case SessionRestoreResult.VaultSetupRequired:
                await Shell.Current.GoToAsync($"{nameof(VaultUnlockPage)}?setup=true");
                break;
            case SessionRestoreResult.VaultReady:
                await Shell.Current.GoToAsync($"{nameof(VaultUnlockPage)}?setup=false");
                break;
            case SessionRestoreResult.NoSession:
                break; // stay on the login form
        }
    }
}
