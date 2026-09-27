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
        if (await _auth.TryRestoreSessionAsync())
        {
            var setupRequired = await _auth.IsVaultSetupRequiredAsync();
            await Shell.Current.GoToAsync($"{nameof(VaultUnlockPage)}?setup={(setupRequired ? "true" : "false")}");
        }
    }
}
