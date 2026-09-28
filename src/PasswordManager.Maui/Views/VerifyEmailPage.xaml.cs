using PasswordManager.Maui.ViewModels;

namespace PasswordManager.Maui.Views;

public partial class VerifyEmailPage : ContentPage
{
    public VerifyEmailPage(VerifyEmailViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
