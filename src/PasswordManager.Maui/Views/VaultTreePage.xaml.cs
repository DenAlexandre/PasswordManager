using PasswordManager.Maui.ViewModels;

namespace PasswordManager.Maui.Views;

public partial class VaultTreePage : ContentPage
{
    private readonly VaultTreeViewModel _vm;

    public VaultTreePage(VaultTreeViewModel vm)
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
