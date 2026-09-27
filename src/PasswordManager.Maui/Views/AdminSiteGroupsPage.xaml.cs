using PasswordManager.Maui.ViewModels;

namespace PasswordManager.Maui.Views;

public partial class AdminSiteGroupsPage : ContentPage
{
    private readonly AdminSiteGroupsViewModel _vm;

    public AdminSiteGroupsPage(AdminSiteGroupsViewModel vm)
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
