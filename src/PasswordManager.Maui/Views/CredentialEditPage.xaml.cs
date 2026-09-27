using PasswordManager.Maui.ViewModels;

namespace PasswordManager.Maui.Views;

public partial class CredentialEditPage : ContentPage
{
    public CredentialEditPage(CredentialEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
