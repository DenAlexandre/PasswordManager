using PasswordManager.Maui.ViewModels;

namespace PasswordManager.Maui.Views;

public partial class SitesPage : ContentPage
{
    private readonly SitesViewModel _vm;

    public SitesPage(SitesViewModel vm)
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
