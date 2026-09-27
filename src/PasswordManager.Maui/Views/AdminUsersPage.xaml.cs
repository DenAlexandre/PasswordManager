using PasswordManager.Maui.ViewModels;

namespace PasswordManager.Maui.Views;

public partial class AdminUsersPage : ContentPage
{
    private readonly AdminUsersViewModel _vm;

    public AdminUsersPage(AdminUsersViewModel vm)
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
