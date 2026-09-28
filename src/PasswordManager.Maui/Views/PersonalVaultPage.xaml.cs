using PasswordManager.Maui.ViewModels;

namespace PasswordManager.Maui.Views;

public partial class PersonalVaultPage : ContentPage
{
    private readonly PersonalVaultViewModel _vm;

    public PersonalVaultPage(PersonalVaultViewModel vm)
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
