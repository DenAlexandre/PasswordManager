using PasswordManager.Maui.ViewModels;

namespace PasswordManager.Maui.Views;

public partial class SiteGroupsPage : ContentPage
{
    private readonly SiteGroupsViewModel _vm;

    public SiteGroupsPage(SiteGroupsViewModel vm)
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
