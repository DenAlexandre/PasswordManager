using PasswordManager.Maui.ViewModels;

namespace PasswordManager.Maui.Views;

public partial class VaultUnlockPage : ContentPage
{
    public VaultUnlockPage(VaultUnlockViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
