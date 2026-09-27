using PasswordManager.Maui.ViewModels;

namespace PasswordManager.Maui.Views;

public partial class CredentialsPage : ContentPage
{
    private readonly CredentialsViewModel _vm;

    public CredentialsPage(CredentialsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);
    }
}
